using System.Security.Claims;
using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Reporte comparativo (UAT 06/10): lo que va del mes contra los mismos días del mes pasado, ventas por mes,
// productos más vendidos y sede contra sede. Semilla: caramelo a S/ 0.30 y chocolate a S/ 1.50.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class ReporteComparativoTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;

    public ReporteComparativoTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ClaimsPrincipal Duena => Sesion.Usuario(_d.Duena, "duena", null);

    private async Task<int> Vender(params VentaItem[] items)
    {
        await using var db = _base.NuevoContexto();
        var formulario = new VentaFormViewModel { ClienteId = _d.ClienteGeneral, MetodoPago = "efectivo" };
        formulario.Items.AddRange(items);
        var resultado = await Sesion.Preparar(new VentaController(db), Duena).Nueva(formulario);
        return (int)Assert.IsType<RedirectToActionResult>(resultado).RouteValues!["id"]!;
    }

    private async Task MoverAlMesPasado(int idVenta)
    {
        await using var db = _base.NuevoContexto();
        var venta = await db.Movimientos.SingleAsync(m => m.IdMovimiento == idVenta);
        venta.Fecha = DateTime.Today.AddMonths(-1).AddHours(10);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Compara_lo_que_va_del_mes_con_los_mismos_dias_del_mes_pasado()
    {
        // Este mes: 4 caramelos (S/ 1.20) y 2 chocolates (S/ 3.00). Mes pasado: 2 caramelos (S/ 0.60).
        await Vender(new VentaItem { ProductoId = _d.Caramelo, UbicacionId = _d.ZonaA01, Cantidad = 4 });
        await Vender(new VentaItem { ProductoId = _d.Chocolate, UbicacionId = _d.ZonaA02, Cantidad = 2 });
        await MoverAlMesPasado(await Vender(new VentaItem { ProductoId = _d.Caramelo, UbicacionId = _d.ZonaA01, Cantidad = 2 }));

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        await using var db = _base.NuevoContexto();
        var modelo = Assert.IsType<ReporteComparativoViewModel>(Assert.IsType<ViewResult>(
            await Sesion.Preparar(new ReporteController(db), Duena).Comparativo(inicioMes, hoy, null, null)).Model);

        Assert.Equal((inicioMes.AddMonths(-1), hoy.AddMonths(-1)), (modelo.Anterior.Desde, modelo.Anterior.Hasta));
        var total = modelo.Indicadores.Single(i => i.Nombre == "Total vendido").Valor;
        Assert.Equal((4.20m, 0.60m, 6m), (total.Actual, total.Anterior, total.Variacion!.Value));
        Assert.Equal((2m, 1m), (modelo.Indicadores.Single(i => i.Nombre == "Ventas").Valor.Actual, modelo.Indicadores.Single(i => i.Nombre == "Ventas").Valor.Anterior));

        // El chocolate (S/ 3.00) va primero y es nuevo; el caramelo segundo, antes primero
        Assert.Equal(new[] { ("Chocolate Sublime", (int?)null, 3.00m, 0m), ("Caramelo Fresa", (int?)1, 1.20m, 0.60m) },
            modelo.Productos.Select(p => (p.Nombre, p.PuestoAnterior, p.Importe.Actual, p.Importe.Anterior)));

        // 12 meses, el último es el actual; y la dueña ve una sede contra la otra
        Assert.Equal(12, modelo.PorMes.Count);
        Assert.Equal(4.20m, modelo.PorMes[^1].Total);
        Assert.Equal(0.60m, modelo.PorMes[^2].Total);
        Assert.Equal(2, modelo.Sedes.Count);
        Assert.Equal(4.20m, modelo.Sedes.Single(s => s.Sede == "Sede Principal").Total.Actual);
    }
}
