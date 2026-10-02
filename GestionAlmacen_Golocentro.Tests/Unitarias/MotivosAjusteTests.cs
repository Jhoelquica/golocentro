using GestionAlmacen_Golocentro.ViewModels;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// Motivos del conteo de inventario: deben coincidir con el CHECK chk_ajusteinventario_motivo de la base
public class MotivosAjusteTests
{
    [Theory]
    [InlineData("conteo_inicial")]
    [InlineData("conteo")]
    [InlineData("merma")]
    [InlineData("correccion")]
    public void Acepta_los_motivos_de_la_base(string motivo)
    {
        Assert.True(MotivosAjuste.EsValido(motivo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("robo")]
    [InlineData("Conteo")]
    [InlineData("conteo inicial")]
    public void Rechaza_lo_que_la_base_no_aceptaria(string? motivo)
    {
        Assert.False(MotivosAjuste.EsValido(motivo));
    }

    [Theory]
    [InlineData("conteo_inicial", "Conteo inicial")]
    [InlineData("merma", "Merma o producto dañado")]
    public void Muestra_el_texto_para_la_pantalla(string motivo, string texto)
    {
        Assert.Equal(texto, MotivosAjuste.Texto(motivo));
    }

    [Fact]
    public void Un_motivo_viejo_desconocido_se_muestra_tal_cual()
    {
        Assert.Equal("inventario_2024", MotivosAjuste.Texto("inventario_2024"));
    }
}
