using GestionAlmacen_Golocentro.Services;
using E = GestionAlmacen_Golocentro.Services.VencimientoProducto.Entrada;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// Vencimiento del producto a partir de sus entradas (UAT 06/10): el más próximo entre lo que todavía queda en stock,
// suponiendo que sale primero lo que llegó antes. Las entradas van de la más reciente a la más antigua.
public class VencimientoProductoTests
{
    private static readonly DateOnly Oct = new(2026, 10, 31), Nov = new(2026, 11, 30), Dic = new(2026, 12, 31);
    private static readonly E[] Entradas = { new(20, Dic, "L3"), new(15, Nov, "L2"), new(10, Oct, "L1") };

    [Fact]
    public void Sin_stock_no_hay_vencimiento()
    {
        Assert.Equal((null, null), VencimientoProducto.Calcular(0, Entradas));
    }

    [Theory]
    [InlineData(20, 2026, 12, 31, "L3")]   // queda solo lo último que llegó
    [InlineData(30, 2026, 11, 30, "L2")]   // lo último (20) y parte de la entrada anterior
    [InlineData(45, 2026, 10, 31, "L1")]   // queda todo
    [InlineData(60, 2026, 10, 31, "L1")]   // más stock que entradas (conteos): cuenta todo lo que llegó
    public void Toma_el_vencimiento_mas_proximo_de_lo_que_queda(int stock, int anio, int mes, int dia, string lote)
    {
        Assert.Equal((new DateOnly(anio, mes, dia), lote), VencimientoProducto.Calcular(stock, Entradas));
    }

    [Fact]
    public void Si_lo_que_queda_llego_sin_fecha_no_hay_vencimiento()
    {
        Assert.Equal((null, null), VencimientoProducto.Calcular(10, new E[] { new(20, null, null), new(15, Nov, "L2") }));
    }
}
