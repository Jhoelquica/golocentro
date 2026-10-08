using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Vencimiento y lote por entrada (UAT 06/10): cada línea de entrada guarda los suyos y el producto muestra el más
// próximo de lo que queda en stock; se recalcula al vender. Semilla: 10 caramelos en A-01, sin vencimiento.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class VencimientoEntradaTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;
    private int _proveedor;
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Today);

    public VencimientoEntradaTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
        var proveedor = new Proveedor { Nombre = "Distribuidora de Prueba", Ruc = "20123456789" };
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync();
        _proveedor = proveedor.IdProveedor;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(IActionResult, MovimientoController)> Entrada(int cantidad, DateOnly? vence, string? lote)
    {
        var db = _base.NuevoContexto();
        var movimientos = Sesion.Preparar(new MovimientoController(db), Sesion.Usuario(_d.Duena, "duena", null));
        var formulario = new MovimientoEntradaViewModel { ProveedorId = _proveedor };
        formulario.Detalles.Add(new DetalleEntradaViewModel { ProductoId = _d.Caramelo, UbicacionId = _d.ZonaA01, Cantidad = cantidad, FechaVencimiento = vence, Lote = lote });
        return (await movimientos.Entrada(formulario), movimientos);
    }

    private async Task<(DateOnly? Fecha, string? Lote)> VencimientoCaramelo()
    {
        await using var db = _base.NuevoContexto();
        var p = await db.Productos.SingleAsync(p => p.IdProducto == _d.Caramelo);
        return (p.FechaVencimiento, p.Lote);
    }

    [Fact]
    public async Task La_entrada_guarda_vencimiento_y_lote_y_el_producto_toma_el_mas_proximo()
    {
        await Entrada(20, Hoy.AddDays(30), " L-001 ");
        await Entrada(5, Hoy.AddDays(90), "L-002");

        await using (var db = _base.NuevoContexto())
        {
            var lineas = await db.DetalleMovimientos.OrderBy(d => d.IdDetalle).Select(d => new { d.FechaVencimiento, d.Lote }).ToListAsync();
            Assert.Equal(new[] { (Hoy.AddDays(30), "L-001"), (Hoy.AddDays(90), "L-002") },
                lineas.Select(l => (l.FechaVencimiento!.Value, l.Lote!)));
        }
        // Quedan 35: 5 del lote 2 + 20 del lote 1 + 10 de antes
        Assert.Equal((Hoy.AddDays(30), "L-001"), await VencimientoCaramelo());

        // Al vender 30 solo quedan los 5 del lote 2
        await using (var db = _base.NuevoContexto())
        {
            var ventas = Sesion.Preparar(new VentaController(db), Sesion.Usuario(_d.Duena, "duena", null));
            var venta = new VentaFormViewModel { ClienteId = _d.ClienteGeneral, MetodoPago = "efectivo" };
            venta.Items.Add(new VentaItem { ProductoId = _d.Caramelo, UbicacionId = _d.ZonaA01, Cantidad = 30 });
            Assert.IsType<RedirectToActionResult>(await ventas.Nueva(venta));
        }
        Assert.Equal((Hoy.AddDays(90), "L-002"), await VencimientoCaramelo());
    }

    [Fact]
    public async Task Sin_entradas_con_fecha_se_respeta_la_fecha_escrita_en_el_producto()
    {
        await using (var db = _base.NuevoContexto())
        {
            var caramelo = await db.Productos.SingleAsync(p => p.IdProducto == _d.Caramelo);
            caramelo.FechaVencimiento = Hoy.AddDays(60);
            await db.SaveChangesAsync();
        }

        await Entrada(5, null, null);

        Assert.Equal((Hoy.AddDays(60), (string?)null), await VencimientoCaramelo());
    }

    [Fact]
    public async Task Una_fecha_de_vencimiento_pasada_se_rechaza_y_no_guarda_nada()
    {
        var (resultado, movimientos) = await Entrada(5, Hoy.AddDays(-1), "L-VIEJO");

        Assert.IsType<ViewResult>(resultado);
        Assert.Contains(movimientos.ModelState[""]!.Errors, e => e.ErrorMessage.StartsWith("La fecha de vencimiento de un producto ya pasó"));
        await using var db = _base.NuevoContexto();
        Assert.Equal(0, await db.Movimientos.CountAsync());
    }
}
