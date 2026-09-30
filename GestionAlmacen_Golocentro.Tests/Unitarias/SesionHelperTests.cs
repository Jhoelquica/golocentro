using GestionAlmacen_Golocentro.Helpers;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// Reglas de contraseña (login, perfil y usuarios) y el sello que cierra las sesiones al cambiarla
public class SesionHelperTests
{
    private static List<(string Campo, string Mensaje)> Validar(string? nueva, string? confirmar, string? usuario = "maria") =>
        SesionHelper.ValidarContrasena(nueva, confirmar, usuario, "Nueva", "Confirmar");

    [Fact]
    public void Contrasena_valida_no_tiene_errores()
    {
        Assert.Empty(Validar("Golosinas2026", "Golosinas2026"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Contrasena_vacia_pide_escribirla(string? vacia)
    {
        var error = Assert.Single(Validar(vacia, vacia));
        Assert.Equal(("Nueva", "Escribe la contraseña."), error);
    }

    [Fact]
    public void Contrasena_de_7_caracteres_es_muy_corta()
    {
        var error = Assert.Single(Validar("abc1234", "abc1234"));
        Assert.Equal("Nueva", error.Campo);
        Assert.Contains("al menos 8", error.Mensaje);
    }

    [Fact]
    public void Contrasena_de_8_caracteres_es_el_minimo_aceptado()
    {
        Assert.Empty(Validar("abcd1234", "abcd1234"));
    }

    [Fact]
    public void Contrasena_de_72_bytes_es_el_maximo_aceptado()
    {
        var justa = new string('a', 72);
        Assert.Empty(Validar(justa, justa));
    }

    [Fact]
    public void Contrasena_de_73_bytes_es_demasiado_larga()
    {
        var larga = new string('a', 73);
        var error = Assert.Single(Validar(larga, larga));
        Assert.Equal(("Nueva", "La contraseña es demasiado larga."), error);
    }

    [Fact]
    public void El_limite_cuenta_bytes_y_no_letras()
    {
        // 37 eñes son 37 caracteres pero 74 bytes en UTF-8: BCrypt solo usaría los primeros 72
        var enies = new string('ñ', 37);
        var error = Assert.Single(Validar(enies, enies));
        Assert.Equal("La contraseña es demasiado larga.", error.Mensaje);
    }

    [Theory]
    [InlineData("maria2026", "maria2026")]
    [InlineData("MARIA2026", "maria2026")]
    public void Contrasena_igual_al_usuario_no_se_acepta(string contrasena, string usuario)
    {
        var error = Assert.Single(Validar(contrasena, contrasena, usuario));
        Assert.Equal("La contraseña no puede ser igual al nombre de usuario.", error.Mensaje);
    }

    [Fact]
    public void Confirmacion_distinta_marca_el_campo_confirmar()
    {
        var error = Assert.Single(Validar("Golosinas2026", "Golosinas2025"));
        Assert.Equal(("Confirmar", "Las dos contraseñas no coinciden."), error);
    }

    [Fact]
    public void Contrasena_corta_y_sin_coincidir_muestra_los_dos_errores()
    {
        var errores = Validar("abc", "xyz");
        Assert.Equal(2, errores.Count);
        Assert.Contains(errores, e => e.Campo == "Nueva");
        Assert.Contains(errores, e => e.Campo == "Confirmar");
    }

    [Fact]
    public void Sello_es_siempre_el_mismo_para_el_mismo_hash()
    {
        const string hash = "$2a$11$abcdefghijklmnopqrstuuvwxyz0123456789ABCDEFGHIJKLMNOPQ";
        Assert.Equal(SesionHelper.Sello(hash), SesionHelper.Sello(hash));
    }

    [Fact]
    public void Sello_tiene_16_caracteres_hexadecimales()
    {
        var sello = SesionHelper.Sello("$2a$11$cualquierhash");
        Assert.Matches("^[0-9A-F]{16}$", sello);
    }

    [Fact]
    public void Sello_cambia_si_cambia_la_contrasena()
    {
        Assert.NotEqual(SesionHelper.Sello("$2a$11$hashAnterior"), SesionHelper.Sello("$2a$11$hashNuevo"));
    }

    [Fact]
    public void Sello_no_contiene_el_hash_original()
    {
        const string hash = "$2a$11$hashQueNoDebeAparecer";
        Assert.DoesNotContain("hashQueNoDebeAparecer", SesionHelper.Sello(hash));
    }
}
