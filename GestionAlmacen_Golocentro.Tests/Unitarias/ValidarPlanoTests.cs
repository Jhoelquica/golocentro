using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// Validación del editor del plano (Negocio → Plano de la sede): tamaño, límites, pasos de medio cuadrito,
// zonas encimadas y líneas de referencia. Es lo que decide si el plano se guarda y se ve así en el Mapa.
public class ValidarPlanoTests
{
    // Zonas de la sede Principal tal como están en la base (código por id)
    private static readonly (int Id, string Codigo)[] CodigosPrincipal =
    {
        (1, "A-01"), (2, "A-02"), (3, "A-03"), (4, "A-04"), (5, "A-05"), (6, "C-01"), (7, "C-02"), (8, "C-03"),
        (9, "C-04"), (10, "R-01"), (11, "B-01"), (12, "B-02"), (13, "B-03"), (14, "B-04"), (15, "B-05"),
        (16, "B-06"), (17, "B-07"), (18, "B-08"), (19, "B-09"), (20, "B-10")
    };

    private static List<Ubicacion> ZonasSede() =>
        CodigosPrincipal.Select(z => new Ubicacion { IdUbicacion = z.Id, CodigoEstante = z.Codigo, IdSede = 1 }).ToList();

    private static ZonaEditor Zona(int id, decimal? x, decimal? y, decimal? ancho, decimal? alto) =>
        new(id, CodigosPrincipal.First(z => z.Id == id).Codigo, null, false, 0, x, y, ancho, alto);

    private static PlanoGuardado Plano(decimal ancho, decimal alto, IEnumerable<ZonaEditor>? zonas = null, IEnumerable<LineaEditor>? lineas = null) =>
        new() { Ancho = ancho, Alto = alto, Zonas = zonas?.ToList() ?? new(), Lineas = lineas?.ToList() ?? new() };

    private static List<string> Validar(PlanoGuardado plano) => NegocioController.ValidarPlano(plano, ZonasSede());

    // El plano real de la sede Principal (30 × 20), con el pasillo superior en 3.5
    private static PlanoGuardado PlanoPrincipal(decimal alturaPasillo = 3.5m) => Plano(30, 20,
        new[]
        {
            Zona(1, 5, 0, 4, 2.5m), Zona(2, 9, 0, 9, 2.5m), Zona(3, 18, 0, 9, 2.5m), Zona(4, 27, 0, 3, 6), Zona(5, 27, 6, 3, 4),
            Zona(11, 0, 5, 2, 3), Zona(12, 0, 8, 2, 4), Zona(13, 0, 12, 2, 8), Zona(14, 4, 13, 4, 2), Zona(15, 8, 13, 7, 2),
            Zona(16, 15, 13, 6, 2), Zona(17, 2, 18, 3.5m, 2), Zona(18, 5.5m, 18, 9.5m, 2), Zona(19, 15, 18, 6, 2),
            Zona(20, 21, 13, 9, 7), Zona(6, 4, 5, 4, 3), Zona(7, 4, 8, 4, 5), Zona(8, 8, 10, 12, 3), Zona(9, 20, 10, 10, 3),
            Zona(10, 9, 4.5m, 17, 5)
        },
        new[]
        {
            new LineaEditor("entrada", 0, 1.5m, 0, 5), new LineaEditor("division", 4, 5, 4, 13),
            new LineaEditor("division", 4, 13, 30, 13), new LineaEditor("pasillo", 22, alturaPasillo, 0, alturaPasillo),
            new LineaEditor("pasillo", 3, 5, 3, 16.5m), new LineaEditor("pasillo", 3, 16.5m, 21, 16.5m)
        });

    // ===== Plano completo =====

    [Fact]
    public void El_plano_de_la_sede_Principal_es_valido()
    {
        Assert.Empty(Validar(PlanoPrincipal()));
    }

    [Fact]
    public void Un_pasillo_en_3_7_no_cumple_el_paso_de_medio_cuadrito()
    {
        // Caso encontrado en la verificación manual: el script del 24/09 guardó este pasillo en 3.7
        // y por eso el plano de la sede Principal no se puede volver a guardar tal como está.
        var error = Assert.Single(Validar(PlanoPrincipal(alturaPasillo: 3.7m)));
        Assert.Equal("Una línea se sale del plano.", error);
    }

    // ===== Tamaño del plano =====

    [Theory]
    [InlineData(5, 5)]
    [InlineData(100, 100)]
    [InlineData(20.5, 12)]
    public void Acepta_tamanos_entre_5_y_100_en_medios_cuadritos(double ancho, double alto)
    {
        Assert.Empty(Validar(Plano((decimal)ancho, (decimal)alto)));
    }

    [Theory]
    [InlineData(4.5, 20)]
    [InlineData(30, 100.5)]
    [InlineData(20.3, 12)]
    public void Rechaza_tamanos_fuera_de_rango_o_que_no_son_medios_cuadritos(double ancho, double alto)
    {
        var error = Assert.Single(Validar(Plano((decimal)ancho, (decimal)alto)));
        Assert.Equal("El plano debe medir entre 5 y 100 cuadritos por lado.", error);
    }

    // ===== Zonas dentro del plano =====

    [Fact]
    public void Una_zona_sin_dibujar_no_se_valida()
    {
        Assert.Empty(Validar(Plano(30, 20, new[] { Zona(1, null, null, null, null) })));
    }

    [Fact]
    public void Una_zona_pegada_al_borde_esta_dentro()
    {
        Assert.Empty(Validar(Plano(30, 20, new[] { Zona(20, 21, 13, 9, 7) })));
    }

    [Theory]
    [InlineData(25, 13, 9, 7)]     // se pasa del borde derecho
    [InlineData(21, 14, 9, 7)]     // se pasa del borde de abajo
    [InlineData(-1, 0, 4, 2)]      // posición negativa
    [InlineData(0, 0, 0, 2)]       // sin ancho
    [InlineData(3.7, 0, 4, 2)]     // no está en medio cuadrito
    public void Una_zona_que_se_sale_del_plano_se_rechaza(double x, double y, double ancho, double alto)
    {
        var error = Assert.Single(Validar(Plano(30, 20, new[] { Zona(20, (decimal)x, (decimal)y, (decimal)ancho, (decimal)alto) })));
        Assert.Equal("La zona B-10 se sale del plano.", error);
    }

    [Fact]
    public void Una_zona_con_posicion_incompleta_se_rechaza()
    {
        var error = Assert.Single(Validar(Plano(30, 20, new[] { Zona(1, 5, null, 4, 2) })));
        Assert.Equal("La zona A-01 se sale del plano.", error);
    }

    [Fact]
    public void Una_zona_que_no_es_de_la_sede_se_rechaza()
    {
        var zonaAjena = new ZonaEditor(99, "Z-99", null, false, 0, 0, 0, 2, 2);
        var error = Assert.Single(Validar(Plano(30, 20, new[] { zonaAjena })));
        Assert.Equal("Una de las zonas ya no existe. Recarga la página.", error);
    }

    // ===== Zonas encimadas =====

    [Fact]
    public void Dos_zonas_encimadas_se_rechazan()
    {
        var error = Assert.Single(Validar(Plano(30, 20, new[] { Zona(1, 5, 0, 4, 2.5m), Zona(2, 5, 0, 4, 2.5m) })));
        Assert.Equal("Las zonas A-01 y A-02 están encimadas.", error);
    }

    [Fact]
    public void Basta_medio_cuadrito_encimado_para_rechazar()
    {
        var error = Assert.Single(Validar(Plano(30, 20, new[] { Zona(1, 5, 0, 4, 2.5m), Zona(2, 8.5m, 0, 9, 2.5m) })));
        Assert.Equal("Las zonas A-01 y A-02 están encimadas.", error);
    }

    [Fact]
    public void Zonas_que_solo_comparten_un_borde_no_estan_encimadas()
    {
        Assert.Empty(Validar(Plano(30, 20, new[] { Zona(1, 5, 0, 4, 2.5m), Zona(2, 9, 0, 9, 2.5m), Zona(6, 5, 2.5m, 4, 2) })));
    }

    [Fact]
    public void Informa_cada_par_de_zonas_encimadas()
    {
        var errores = Validar(Plano(30, 20, new[] { Zona(1, 0, 0, 10, 10), Zona(2, 5, 5, 10, 10), Zona(3, 8, 8, 2, 2) }));
        Assert.Equal(3, errores.Count);
        Assert.Contains("Las zonas A-01 y A-02 están encimadas.", errores);
        Assert.Contains("Las zonas A-01 y A-03 están encimadas.", errores);
        Assert.Contains("Las zonas A-02 y A-03 están encimadas.", errores);
    }

    // ===== Líneas de referencia =====

    [Theory]
    [InlineData("entrada")]
    [InlineData("division")]
    [InlineData("pasillo")]
    public void Acepta_los_tres_tipos_de_linea(string tipo)
    {
        Assert.Empty(Validar(Plano(30, 20, lineas: new[] { new LineaEditor(tipo, 0, 0, 10, 0) })));
    }

    [Fact]
    public void Rechaza_un_tipo_de_linea_que_no_existe()
    {
        var error = Assert.Single(Validar(Plano(30, 20, lineas: new[] { new LineaEditor("puerta", 0, 0, 10, 0) })));
        Assert.Equal("Una línea tiene un tipo que no existe.", error);
    }

    [Fact]
    public void Rechaza_una_linea_fuera_del_plano()
    {
        var error = Assert.Single(Validar(Plano(30, 20, lineas: new[] { new LineaEditor("pasillo", 0, 5, 31, 5) })));
        Assert.Equal("Una línea se sale del plano.", error);
    }

    [Fact]
    public void Rechaza_una_linea_sin_largo()
    {
        var error = Assert.Single(Validar(Plano(30, 20, lineas: new[] { new LineaEditor("division", 4, 4, 4, 4) })));
        Assert.Equal("Una línea no tiene largo.", error);
    }

    [Fact]
    public void Acepta_hasta_100_lineas()
    {
        var lineas = Enumerable.Range(0, 100).Select(_ => new LineaEditor("pasillo", 0, 1, 10, 1));
        Assert.Empty(Validar(Plano(30, 20, lineas: lineas)));
    }

    [Fact]
    public void Rechaza_mas_de_100_lineas()
    {
        var lineas = Enumerable.Range(0, 101).Select(_ => new LineaEditor("pasillo", 0, 1, 10, 1));
        var error = Assert.Single(Validar(Plano(30, 20, lineas: lineas)));
        Assert.Equal("Hay demasiadas líneas en el plano (máximo 100).", error);
    }

    [Fact]
    public void El_mismo_error_en_varias_lineas_se_muestra_una_sola_vez()
    {
        var lineas = new[] { new LineaEditor("puerta", 0, 0, 5, 0), new LineaEditor("ventana", 0, 1, 5, 1) };
        var error = Assert.Single(Validar(Plano(30, 20, lineas: lineas)));
        Assert.Equal("Una línea tiene un tipo que no existe.", error);
    }
}
