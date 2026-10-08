using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Presentaciones en la ficha del producto (UAT 06/10): se crean, se editan y se quitan junto con el producto.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class PresentacionProductoTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;

    public PresentacionProductoTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ProductoController Controlador(Data.AppDbContext db) =>
        Sesion.Preparar(new ProductoController(db), Sesion.Usuario(_d.Duena, "duena", null));

    private static ProductoFormViewModel Doritos(params PresentacionFormViewModel[] presentaciones) => new()
    {
        Nombre = "Doritos 40 g", Codigo = "SNK-001", Tipo = "Snack", UnidadMedida = "tira",
        PrecioUnitario = 1.00m, StockMinimo = 8, Presentaciones = presentaciones.ToList()
    };

    private async Task<List<(string Nombre, int Factor, decimal Precio)>> Guardadas(string codigo)
    {
        await using var db = _base.NuevoContexto();
        return (await db.ProductoPresentacions
                .Where(pp => pp.IdProductoNavigation.Codigo == codigo)
                .OrderBy(pp => pp.Factor)
                .Select(pp => new { pp.Nombre, pp.Factor, pp.Precio })
                .ToListAsync())
            .Select(x => (x.Nombre, x.Factor, x.Precio)).ToList();
    }

    [Fact]
    public async Task Al_crear_el_producto_se_guardan_sus_presentaciones()
    {
        await using (var db = _base.NuevoContexto())
            Assert.IsType<RedirectToActionResult>(await Controlador(db).Crear(Doritos(
                new PresentacionFormViewModel { Nombre = " bolsa ", Factor = 8, Precio = 7.00m },
                new PresentacionFormViewModel()), null));   // fila vacía: se ignora

        Assert.Equal(new[] { ("bolsa", 8, 7.00m) }, await Guardadas("SNK-001"));
    }

    [Fact]
    public async Task Al_editar_se_cambian_se_quitan_y_se_agregan_presentaciones()
    {
        await using (var db = _base.NuevoContexto())
            await Controlador(db).Crear(Doritos(
                new PresentacionFormViewModel { Nombre = "bolsa", Factor = 8, Precio = 7.00m },
                new PresentacionFormViewModel { Nombre = "display", Factor = 24, Precio = 20.00m }), null);

        int idProducto, idBolsa;
        await using (var db = _base.NuevoContexto())
        {
            idProducto = await db.Productos.Where(p => p.Codigo == "SNK-001").Select(p => p.IdProducto).SingleAsync();
            idBolsa = await db.ProductoPresentacions.Where(pp => pp.Nombre == "bolsa").Select(pp => pp.IdPresentacion).SingleAsync();
        }

        // Se queda la bolsa con otro precio, se quita el display y se agrega la caja
        await using (var db = _base.NuevoContexto())
            Assert.IsType<RedirectToActionResult>(await Controlador(db).Editar(idProducto, Doritos(
                new PresentacionFormViewModel { Id = idBolsa, Nombre = "bolsa", Factor = 8, Precio = 7.50m },
                new PresentacionFormViewModel { Nombre = "caja", Factor = 96, Precio = 85.00m })));

        Assert.Equal(new[] { ("bolsa", 8, 7.50m), ("caja", 96, 85.00m) }, await Guardadas("SNK-001"));
        await using var lectura = _base.NuevoContexto();
        Assert.Equal(idBolsa, await lectura.ProductoPresentacions.Where(pp => pp.Nombre == "bolsa").Select(pp => pp.IdPresentacion).SingleAsync());
    }

    [Fact]
    public async Task Una_presentacion_mal_escrita_no_deja_guardar_nada()
    {
        await using (var db = _base.NuevoContexto())
        {
            var controlador = Controlador(db);
            var resultado = await controlador.Crear(Doritos(new PresentacionFormViewModel { Nombre = "tira", Factor = 1, Precio = 1m }), null);

            Assert.IsType<ViewResult>(resultado);
            Assert.True(controlador.ModelState.ContainsKey("Presentaciones[0].Nombre"));
            Assert.True(controlador.ModelState.ContainsKey("Presentaciones[0].Factor"));
        }

        await using var lectura = _base.NuevoContexto();
        Assert.False(await lectura.Productos.AnyAsync(p => p.Codigo == "SNK-001"));
    }
}
