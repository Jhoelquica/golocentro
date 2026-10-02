using GestionAlmacen_Golocentro.Controllers;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// Número impreso en la nota de venta: serie + correlativo de 6 dígitos
public class NumeroNotaTests
{
    [Theory]
    [InlineData("NV01", 1, "NV01-000001")]
    [InlineData("NV01", 25, "NV01-000025")]
    [InlineData("NV01", 999999, "NV01-999999")]
    [InlineData("NV02", 7, "NV02-000007")]
    public void Arma_serie_y_correlativo_con_seis_digitos(string serie, int numero, string esperado)
    {
        Assert.Equal(esperado, VentaController.NumeroNota(serie, numero));
    }

    [Fact]
    public void Pasado_el_millon_no_corta_digitos()
    {
        Assert.Equal("NV01-1000000", VentaController.NumeroNota("NV01", 1_000_000));
    }
}
