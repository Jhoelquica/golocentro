using GestionAlmacen_Golocentro.ViewModels;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// UAT 06/10 (T12): los botones de periodo de los reportes (Hoy, Ayer, Este mes...) deben llevar sus fechas,
// la sede elegida y los filtros propios del reporte; antes salían sin fechas y el reporte no cambiaba.
public class RutaPeriodoTests
{
    private static readonly DateOnly Desde = new(2026, 10, 1);
    private static readonly DateOnly Hasta = new(2026, 10, 7);

    [Fact]
    public void Lleva_las_fechas_del_periodo()
    {
        var ruta = new FiltroReporte().RutaPeriodo(Desde, Hasta);

        Assert.Equal("2026-10-01", ruta["desde"]);
        Assert.Equal("2026-10-07", ruta["hasta"]);
    }

    [Fact]
    public void Lleva_la_sede_si_la_duena_eligio_una()
    {
        var filtro = new FiltroReporte { PuedeElegirSede = true, SedeId = 2 };

        Assert.Equal("2", filtro.RutaPeriodo(Desde, Hasta)["sede"]);
    }

    [Theory]
    [InlineData(true, null)]   // la dueña ve todas las sedes
    [InlineData(false, 1)]     // la encargada solo ve la suya: la sede no va en la dirección
    public void No_lleva_sede_si_no_hay_una_elegida_por_la_duena(bool puedeElegir, int? sede)
    {
        var filtro = new FiltroReporte { PuedeElegirSede = puedeElegir, SedeId = sede };

        Assert.False(filtro.RutaPeriodo(Desde, Hasta).ContainsKey("sede"));
    }

    [Fact]
    public void Conserva_los_filtros_propios_sin_modificarlos()
    {
        var extra = new Dictionary<string, string> { ["producto"] = "1" };

        var ruta = new FiltroReporte().RutaPeriodo(Desde, Hasta, extra);

        Assert.Equal("1", ruta["producto"]);
        Assert.Equal("2026-10-01", ruta["desde"]);
        Assert.Single(extra);
    }
}
