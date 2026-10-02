using GestionAlmacen_Golocentro.Helpers;

namespace GestionAlmacen_Golocentro.Tests.Unitarias;

// La base guarda los roles sin tildes ni eñes; en pantalla se muestran bien escritos
public class RolHelperTests
{
    [Theory]
    [InlineData("duena", "Dueña")]
    [InlineData("encargada", "Encargada")]
    [InlineData("trabajador", "Trabajador")]
    public void Muestra_los_roles_del_sistema_bien_escritos(string rol, string esperado)
    {
        Assert.Equal(esperado, RolHelper.Etiqueta(rol));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sin_rol_no_muestra_nada(string? rol)
    {
        Assert.Equal("", RolHelper.Etiqueta(rol));
    }

    [Fact]
    public void Un_rol_desconocido_se_muestra_con_mayuscula_inicial()
    {
        Assert.Equal("Supervisor", RolHelper.Etiqueta("supervisor"));
    }
}
