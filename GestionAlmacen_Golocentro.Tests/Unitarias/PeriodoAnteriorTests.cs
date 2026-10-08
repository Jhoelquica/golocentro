using GestionAlmacen_Golocentro.ViewModels;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// Reporte comparativo (UAT 06/10): con qué periodo se compara cada rango de fechas
public class PeriodoAnteriorTests
{
    private static DateOnly D(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("2026-10-01", "2026-10-07", "2026-09-01", "2026-09-07")]   // lo que va del mes: los mismos días del mes pasado
    [InlineData("2026-09-01", "2026-09-30", "2026-08-01", "2026-08-31")]   // mes completo: el mes completo anterior
    [InlineData("2026-03-01", "2026-03-31", "2026-02-01", "2026-02-28")]   // aunque el anterior tenga menos días
    [InlineData("2026-08-01", "2026-09-30", "2026-06-01", "2026-07-31")]   // dos meses completos: los dos anteriores
    [InlineData("2026-01-01", "2026-10-07", "2025-01-01", "2025-10-07")]   // lo que va del año: las mismas fechas del año pasado
    [InlineData("2026-01-01", "2026-01-07", "2025-12-01", "2025-12-07")]   // lo que va de enero: los mismos días de diciembre
    [InlineData("2026-10-07", "2026-10-07", "2026-10-06", "2026-10-06")]   // un día: el día anterior
    [InlineData("2026-09-15", "2026-09-20", "2026-09-09", "2026-09-14")]   // otro rango: los mismos días justo antes
    public void Compara_con_el_periodo_que_corresponde(string desde, string hasta, string desdeAnterior, string hastaAnterior)
    {
        var filtro = new FiltroReporte { Desde = D(desde), Hasta = D(hasta), SedeId = 2, PuedeElegirSede = true };

        var anterior = filtro.PeriodoAnterior();

        Assert.Equal((D(desdeAnterior), D(hastaAnterior), 2), (anterior.Desde, anterior.Hasta, anterior.SedeId!.Value));
    }

    [Theory]
    [InlineData(120, 100, 0.2)]
    [InlineData(80, 100, -0.2)]
    public void La_variacion_es_relativa_al_periodo_anterior(decimal actual, decimal anterior, decimal variacion)
    {
        Assert.Equal(variacion, new Comparado(actual, anterior).Variacion);
    }

    [Fact]
    public void Sin_ventas_antes_no_hay_variacion()
    {
        Assert.Null(new Comparado(50, 0).Variacion);
    }
}
