using System.Text.RegularExpressions;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// UAT 06/10 (T03 y T06): la animación con la que aparece cada página no puede quedar aplicada al terminar.
// Con "both" o "forwards" el navegador le deja un transform a la página, y los modales que van dentro
// (Nuevo cliente en Venta, Nuevo proveedor en Entrada) quedan debajo del fondo oscuro y no responden a clics.
public class AnimacionEntradaTests
{
    private static string Layout() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Vistas", "_Layout.cshtml"));

    [Fact]
    public void La_animacion_de_entrada_no_queda_aplicada_al_terminar()
    {
        var regla = Regex.Match(Layout(), @"\.g-main\s*>\s*\*\s*\{\s*animation:\s*([^;}]+)");

        Assert.True(regla.Success, "No se encontró la animación de entrada de las páginas en _Layout.cshtml");
        Assert.DoesNotMatch(@"\b(both|forwards)\b", regla.Groups[1].Value);
    }
}
