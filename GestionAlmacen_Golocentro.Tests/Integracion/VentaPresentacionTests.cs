using System.Security.Claims;
using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Venta por presentación (UAT 06/10). Semilla: 10 caramelos (unidad base "unidad") en A-01; se les agrega la
// presentación bolsa = 4 unidades a S/ 1.00. El stock se descuenta en unidades base y la nota muestra bolsas.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class VentaPresentacionTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;

    public VentaPresentacionTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
        db.ProductoPresentacions.Add(new ProductoPresentacion { IdProducto = _d.Caramelo, Nombre = "bolsa", Factor = 4, Precio = 1.00m });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ClaimsPrincipal Duena => Sesion.Usuario(_d.Duena, "duena", null);

    private VentaItem Caramelo(int cantidad, string? presentacion = null, decimal? precio = null) =>
        new() { ProductoId = _d.Caramelo, UbicacionId = _d.ZonaA01, Cantidad = cantidad, Presentacion = presentacion, Precio = precio };

    private async Task<(IActionResult Resultado, VentaController Controlador)> Vender(params VentaItem[] items)
    {
        var db = _base.NuevoContexto();
        var ventas = Sesion.Preparar(new VentaController(db), Duena);
        var formulario = new VentaFormViewModel { ClienteId = _d.ClienteGeneral, MetodoPago = "efectivo" };
        formulario.Items.AddRange(items);
        return (await ventas.Nueva(formulario), ventas);
    }

    private async Task<int> StockA01()
    {
        await using var db = _base.NuevoContexto();
        return await db.ProductoUbicacions.Where(pu => pu.IdProducto == _d.Caramelo && pu.IdUbicacion == _d.ZonaA01).Select(pu => pu.CantidadActual).SingleAsync();
    }

    [Fact]
    public async Task Vender_dos_bolsas_descuenta_ocho_unidades_y_cobra_el_precio_de_la_bolsa()
    {
        var (resultado, _) = await Vender(Caramelo(2, "bolsa"), Caramelo(1));
        var idVenta = (int)Assert.IsType<RedirectToActionResult>(resultado).RouteValues!["id"]!;

        Assert.Equal(1, await StockA01());   // 10 - 2 bolsas de 4 - 1 unidad
        await using (var db = _base.NuevoContexto())
        {
            var lineas = await db.DetalleMovimientos.Where(d => d.IdMovimiento == idVenta).OrderBy(d => d.IdDetalle)
                .Select(d => new { d.Cantidad, d.Factor, d.Presentacion, d.PrecioUnitarioSnapshot }).ToListAsync();
            Assert.Equal(new[] { (8, 4, "bolsa", 1.00m), (1, 1, (string?)null, 0.30m) },
                lineas.Select(l => (l.Cantidad, l.Factor, l.Presentacion, l.PrecioUnitarioSnapshot)));
            Assert.Equal(2.30m, await db.NotaVenta.Where(n => n.IdMovimiento == idVenta).Select(n => n.Total).SingleAsync());
        }

        // La nota muestra 2 bolsas a S/ 1.00 (S/ 2.00) y 1 unidad a S/ 0.30
        await using (var db = _base.NuevoContexto())
        {
            var nota = Assert.IsType<NotaVentaViewModel>(Assert.IsType<ViewResult>(
                await Sesion.Preparar(new VentaController(db), Duena).Nota(idVenta)).Model);
            Assert.Equal(new[] { (2, "bolsa", 1.00m, 2.00m), (1, "unidad", 0.30m, 0.30m) },
                nota.Lineas.Select(l => (l.Cantidad, l.Unidad, l.PrecioUnitario, l.Importe)));
            Assert.Contains("bolsa de 4 unidades", nota.Lineas[0].Descripcion);
        }

        // Al anular vuelven las 9 unidades
        await using (var db = _base.NuevoContexto())
            await Sesion.Preparar(new VentaController(db), Duena).Anular(idVenta, "Prueba de anulación");
        Assert.Equal(10, await StockA01());
    }

    [Fact]
    public async Task Bolsas_y_unidades_de_mas_en_la_misma_zona_se_rechazan_con_el_stock_en_bolsas()
    {
        var (resultado, ventas) = await Vender(Caramelo(2, "bolsa"), Caramelo(3));   // 8 + 3 = 11 > 10

        Assert.IsType<ViewResult>(resultado);
        Assert.Contains(ventas.ModelState[""]!.Errors,
            e => e.ErrorMessage == "No alcanza el stock de Caramelo Fresa en esa zona: hay 2 bolsas y 2 unidades, pediste 2 bolsas y 3 unidades.");
        Assert.Equal(10, await StockA01());
    }

    [Fact]
    public async Task Una_presentacion_que_ya_no_existe_se_rechaza()
    {
        var (resultado, ventas) = await Vender(Caramelo(1, "caja"));

        Assert.IsType<ViewResult>(resultado);
        Assert.Contains(ventas.ModelState[""]!.Errors, e => e.ErrorMessage.StartsWith("La presentación «caja» de Caramelo Fresa ya no existe"));
    }

    [Fact]
    public async Task El_reporte_de_productos_suma_el_importe_cobrado_por_bolsa()
    {
        await Vender(Caramelo(2, "bolsa", 0.90m));   // precio cambiado: S/ 0.90 la bolsa

        await using var db = _base.NuevoContexto();
        var reporte = Assert.IsType<ReporteProductosViewModel>(Assert.IsType<ViewResult>(
            await Sesion.Preparar(new ReporteController(db), Duena).Productos(null, null, null, null)).Model);
        var fila = Assert.Single(reporte.Filas);
        Assert.Equal((8, 1.80m), (fila.Cantidad, fila.Importe));
    }
}
