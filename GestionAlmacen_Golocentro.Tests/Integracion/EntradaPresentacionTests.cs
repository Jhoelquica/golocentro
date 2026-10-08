using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Entrada por presentación (UAT 06/10): llegan 3 cajas de 12 y el stock sube 36 unidades base.
// Semilla: 10 caramelos en A-01; se les agrega la presentación caja = 12 unidades.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class EntradaPresentacionTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;
    private int _proveedor;

    public EntradaPresentacionTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
        db.ProductoPresentacions.Add(new ProductoPresentacion { IdProducto = _d.Caramelo, Nombre = "caja", Factor = 12, Precio = 3.00m });
        var proveedor = new Proveedor { Nombre = "Distribuidora de Prueba", Ruc = "20123456789" };
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync();
        _proveedor = proveedor.IdProveedor;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(IActionResult, MovimientoController)> Entrada(params DetalleEntradaViewModel[] detalles)
    {
        var db = _base.NuevoContexto();
        var movimientos = Sesion.Preparar(new MovimientoController(db), Sesion.Usuario(_d.Duena, "duena", null));
        var formulario = new MovimientoEntradaViewModel { ProveedorId = _proveedor };
        formulario.Detalles.AddRange(detalles);
        return (await movimientos.Entrada(formulario), movimientos);
    }

    [Fact]
    public async Task Tres_cajas_de_doce_suben_36_unidades_y_la_linea_guarda_la_caja()
    {
        var (resultado, movimientos) = await Entrada(new DetalleEntradaViewModel
        {
            ProductoId = _d.Caramelo, UbicacionId = _d.ZonaA01, Cantidad = 3, Presentacion = "caja"
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Entrada registrada: 1 producto, 36 unidades.", movimientos.TempData["Exito"]);
        await using var db = _base.NuevoContexto();
        Assert.Equal(46, await db.ProductoUbicacions.Where(pu => pu.IdProducto == _d.Caramelo && pu.IdUbicacion == _d.ZonaA01).Select(pu => pu.CantidadActual).SingleAsync());
        var linea = await db.DetalleMovimientos.SingleAsync();
        Assert.Equal((36, 12, "caja", 10), (linea.Cantidad, linea.Factor, linea.Presentacion, linea.StockAnterior));
    }

    [Fact]
    public async Task Mas_de_un_millon_de_unidades_base_por_linea_se_rechaza()
    {
        var (resultado, movimientos) = await Entrada(new DetalleEntradaViewModel
        {
            ProductoId = _d.Caramelo, UbicacionId = _d.ZonaA01, Cantidad = 100_000, Presentacion = "caja"   // 1,200,000 unidades
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Contains(movimientos.ModelState[""]!.Errors, e => e.ErrorMessage.StartsWith("La cantidad de un producto no puede pasar de"));
    }
}
