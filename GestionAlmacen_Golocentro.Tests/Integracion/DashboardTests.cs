using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Dashboard (HomeController.Index): cuenta las ventas del día y de los últimos 7 días; las anuladas no cuentan
[Collection(ColeccionBaseDeDatos.Nombre)]
public class DashboardTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;

    public DashboardTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> Vender(int cantidad)
    {
        await using var db = _base.NuevoContexto();
        var ventas = Sesion.Preparar(new VentaController(db), Sesion.Usuario(_d.Duena, "duena", null));
        var formulario = new VentaFormViewModel { ClienteId = _d.ClienteGeneral, MetodoPago = "efectivo" };
        formulario.Items.Add(new VentaItem { ProductoId = _d.Caramelo, UbicacionId = _d.ZonaA01, Cantidad = cantidad });
        return (int)Assert.IsType<RedirectToActionResult>(await ventas.Nueva(formulario)).RouteValues!["id"]!;
    }

    private async Task<DashboardViewModel> Dashboard()
    {
        await using var db = _base.NuevoContexto();
        var inicio = Sesion.Preparar(new HomeController(db), Sesion.Usuario(_d.Duena, "duena", null));
        return Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(await inicio.Index()).Model);
    }

    [Fact]
    public async Task Una_venta_anulada_no_cuenta_en_las_ventas_del_dashboard()
    {
        var primera = await Vender(3);
        await Vender(2);

        var antes = await Dashboard();
        Assert.Equal(2, antes.SalidasHoy);
        Assert.Equal(2, antes.UltimosSieteDias.Single(d => d.Fecha == DateTime.Today).Salidas);

        await using (var db = _base.NuevoContexto())
        {
            var ventas = Sesion.Preparar(new VentaController(db), Sesion.Usuario(_d.Duena, "duena", null));
            await ventas.Anular(primera, "Nota hecha por error");
        }

        var despues = await Dashboard();
        Assert.Equal(1, despues.SalidasHoy);
        Assert.Equal(1, despues.UltimosSieteDias.Single(d => d.Fecha == DateTime.Today).Salidas);
        Assert.Equal(1, despues.UltimosSieteDias.Sum(d => d.Salidas));

        // En "últimos movimientos" sigue apareciendo, marcada como anulada
        Assert.Equal(2, despues.UltimosMovimientos.Count);
        Assert.True(despues.UltimosMovimientos.Single(m => m.IdMovimiento == primera).Anulada);
        Assert.Single(despues.UltimosMovimientos, m => !m.Anulada);
    }
}
