using GestionAlmacen_Golocentro.Helpers;
using F = GestionAlmacen_Golocentro.Helpers.Presentaciones.Fila;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// Presentaciones (UAT 06/10): unidad base + presentaciones con su cantidad y su precio
public class PresentacionesTests
{
    [Theory]
    [InlineData(16, "tira", "2 bolsas")]
    [InlineData(19, "tira", "2 bolsas y 3 tiras")]
    [InlineData(5, "tira", "5 tiras")]
    [InlineData(1, "tira", "1 tira")]
    [InlineData(9, "tira", "1 bolsa y 1 tira")]
    public void Describe_el_stock_con_la_presentacion_mas_grande(int cantidad, string unidad, string esperado)
    {
        Assert.Equal(esperado, Presentaciones.Describir(cantidad, unidad, new[] { ("bolsa", 8) }));
    }

    [Fact]
    public void Sin_presentaciones_se_describe_en_la_unidad_base()
    {
        Assert.Equal("40 unidades", Presentaciones.Describir(40, "unidad", Array.Empty<(string, int)>()));
    }

    [Fact]
    public void Usa_la_presentacion_mas_grande_cuando_hay_varias()
    {
        Assert.Equal("2 cajas y 5 packs", Presentaciones.Describir(29, "pack", new[] { ("media caja", 6), ("caja", 12) }));
    }

    [Theory]
    [InlineData("bolsa", "bolsas")]
    [InlineData("unidad", "unidades")]
    [InlineData("pack", "packs")]
    [InlineData("caja", "cajas")]
    [InlineData("kg", "kg")]
    [InlineData("lápiz", "lápices")]
    public void Plural_de_la_unidad(string unidad, string plural)
    {
        Assert.Equal(plural, Presentaciones.Plural(unidad));
    }

    [Theory]
    [InlineData(16, 8, 7.00, 14.00)]    // 2 bolsas a S/ 7.00
    [InlineData(3, 1, 1.25, 3.75)]      // 3 tiras a S/ 1.25 (unidad base)
    [InlineData(24, 12, 30.50, 61.00)]  // 2 cajas a S/ 30.50
    public void Importe_de_la_linea_es_presentaciones_por_su_precio(int cantidadBase, int factor, decimal precio, decimal importe)
    {
        Assert.Equal(importe, Presentaciones.Importe(cantidadBase, factor, precio));
    }

    [Fact]
    public void Una_presentacion_valida_no_tiene_errores_y_las_filas_vacias_se_ignoran()
    {
        Assert.Empty(Presentaciones.Validar("tira", new[] { new F("bolsa", 8, 7.00m), new F(" ", null, null) }));
    }

    [Fact]
    public void Detecta_cada_dato_mal_escrito()
    {
        var errores = Presentaciones.Validar("tira", new[]
        {
            new F("", 8, 7m),          // sin nombre
            new F("Tira", 8, 7m),      // igual a la unidad base
            new F("bolsa", 1, 7m),     // trae menos de 2
            new F("caja", 12, -1m),    // precio negativo
            new F("pack", 6, 1.234m),  // 3 decimales
            new F("BOLSA", 10, 9m),    // repetida (sin importar mayúsculas)
            new F("saco", null, null)  // sin cantidad ni precio
        });

        Assert.Equal(new[]
        {
            (0, "Nombre"), (1, "Nombre"), (2, "Factor"), (3, "Precio"), (4, "Precio"), (5, "Nombre"), (6, "Factor"), (6, "Precio")
        }, errores.Select(e => (e.Indice, e.Campo)));
    }
}
