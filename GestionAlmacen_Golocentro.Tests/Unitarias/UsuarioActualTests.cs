using System.Security.Claims;
using GestionAlmacen_Golocentro.Helpers;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// Sede del usuario que inició sesión: define qué datos ve (la dueña ve todas las sedes)
public class UsuarioActualTests
{
    private static ClaimsPrincipal Usuario(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Cookies"));

    [Fact]
    public void Encargada_o_trabajador_tienen_su_sede()
    {
        Assert.Equal(2, Usuario(new Claim("SedeId", "2")).SedeId());
    }

    [Fact]
    public void Duena_con_sede_vacia_ve_todas()
    {
        Assert.Null(Usuario(new Claim("SedeId", "")).SedeId());
    }

    [Fact]
    public void Sin_el_dato_de_sede_no_se_asume_ninguna()
    {
        Assert.Null(Usuario(new Claim(ClaimTypes.Name, "maria")).SedeId());
    }

    [Fact]
    public void Un_valor_que_no_es_numero_no_rompe_la_pagina()
    {
        Assert.Null(Usuario(new Claim("SedeId", "norte")).SedeId());
    }

    [Fact]
    public void Visitante_sin_sesion_no_tiene_sede()
    {
        Assert.Null(new ClaimsPrincipal(new ClaimsIdentity()).SedeId());
    }
}
