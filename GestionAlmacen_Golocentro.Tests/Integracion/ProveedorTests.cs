using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Celular del proveedor (UAT 06/10): se guarda al crearlo desde Entrada y al editarlo, y la lista lo muestra
// para los botones de WhatsApp y llamar. El formato (9 dígitos, empieza con 9) lo valida el atributo del modelo.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class ProveedorTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;

    public ProveedorTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ProveedorController Controlador(Data.AppDbContext db) =>
        Sesion.Preparar(new ProveedorController(db), Sesion.Usuario(_d.Duena, "duena", null));

    [Fact]
    public async Task El_proveedor_creado_desde_Entrada_guarda_su_celular()
    {
        await using (var db = _base.NuevoContexto())
        {
            var respuesta = await Controlador(db).CrearRapido(new ContraparteFormViewModel
            {
                Nombre = "Snacks Andinos SAC", Documento = "20611122233", Celular = " 956123456 "
            });
            Assert.Contains("celular = 956123456", Assert.IsType<JsonResult>(respuesta).Value!.ToString());
        }

        await using var lectura = _base.NuevoContexto();
        Assert.Equal("956123456", await lectura.Proveedores.Where(p => p.Ruc == "20611122233").Select(p => p.Celular).SingleAsync());
    }

    [Fact]
    public async Task Al_editar_se_cambia_o_se_quita_el_celular_y_la_lista_lo_muestra()
    {
        int id;
        await using (var db = _base.NuevoContexto())
        {
            var proveedor = new Proveedor { Nombre = "Distribuidora de Prueba", Ruc = "20123456789" };
            db.Proveedores.Add(proveedor);
            await db.SaveChangesAsync();
            id = proveedor.IdProveedor;
        }

        await using (var db = _base.NuevoContexto())
            await Controlador(db).Editar(id, new ContraparteFormViewModel { Nombre = "Distribuidora de Prueba", Documento = "20123456789", Celular = "987654321" });
        await using (var db = _base.NuevoContexto())
        {
            var lista = Assert.IsType<ViewResult>(await Controlador(db).Index(null));
            Assert.Equal("987654321", Assert.Single(Assert.IsType<ContraparteListaViewModel>(lista.Model).Filas).Celular);
        }

        await using (var db = _base.NuevoContexto())
            await Controlador(db).Editar(id, new ContraparteFormViewModel { Nombre = "Distribuidora de Prueba", Documento = "20123456789", Celular = "  " });
        await using var lectura = _base.NuevoContexto();
        Assert.Null(await lectura.Proveedores.Where(p => p.IdProveedor == id).Select(p => p.Celular).SingleAsync());
    }

    [Theory]
    [InlineData("987654321", true)]
    [InlineData("87654321", false)]    // 8 dígitos
    [InlineData("887654321", false)]   // no empieza con 9
    [InlineData("98765432a", false)]
    public void El_celular_debe_tener_9_digitos_y_empezar_con_9(string celular, bool valido)
    {
        var modelo = new ContraparteFormViewModel { Nombre = "X", Documento = "20123456789", Celular = celular };
        var errores = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var esValido = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            modelo, new System.ComponentModel.DataAnnotations.ValidationContext(modelo), errores, validateAllProperties: true);

        Assert.Equal(valido, esValido);
    }
}
