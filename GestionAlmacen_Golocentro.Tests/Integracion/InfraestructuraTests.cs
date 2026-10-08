using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Prueba de humo de la infraestructura: el contenedor arranca, el esquema real se carga completo
// y el sistema (AppDbContext) lee y escribe en esa base.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class InfraestructuraTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;

    public InfraestructuraTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public Task InitializeAsync() => _base.LimpiarAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task La_base_de_pruebas_es_PostgreSQL_18_y_no_la_base_real()
    {
        await using var db = _base.NuevoContexto();
        var version = await db.Database.SqlQueryRaw<string>("SELECT version() AS \"Value\"").SingleAsync();
        var nombre = await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"").SingleAsync();

        Assert.StartsWith("PostgreSQL 18", version);
        Assert.Equal("golocentro_pruebas", nombre);
    }

    [Fact]
    public async Task El_esquema_trae_las_21_tablas_del_sistema()
    {
        await using var db = _base.NuevoContexto();
        var tablas = await db.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' ORDER BY 1")
            .ToListAsync();

        Assert.Equal(21, tablas.Count);
        Assert.Contains("producto_ubicacion", tablas);
        Assert.Contains("producto_presentacion", tablas);
        Assert.Contains("nota_venta", tablas);
        Assert.Contains("alerta", tablas);
    }

    [Fact]
    public async Task El_esquema_trae_las_restricciones_de_la_base_real()
    {
        await using var db = _base.NuevoContexto();
        var checks = await db.Database
            .SqlQueryRaw<string>("SELECT conname AS \"Value\" FROM pg_constraint WHERE contype = 'c' AND connamespace = 'public'::regnamespace")
            .ToListAsync();

        Assert.Equal(35, checks.Count);
        Assert.Contains("chk_productoubicacion_cantidad", checks);
        Assert.Contains("chk_detallemovimiento_factor", checks);
        Assert.Contains("chk_productopresentacion_factor", checks);
        Assert.Contains("chk_usuario_rol", checks);
        Assert.Contains("chk_notaventa_anulacion", checks);
    }

    [Fact]
    public async Task El_sistema_guarda_y_lee_datos_en_la_base_de_pruebas()
    {
        int idSede;
        await using (var db = _base.NuevoContexto())
        {
            var datos = await Semilla.CrearAsync(db);
            idSede = datos.SedePrincipal;
        }

        await using var lectura = _base.NuevoContexto();
        var sede = await lectura.Sedes.SingleAsync(s => s.IdSede == idSede);
        Assert.Equal("Sede Principal", sede.Nombre);
        Assert.Equal(3, await lectura.Ubicaciones.CountAsync());
    }

    [Fact]
    public async Task Cada_prueba_empieza_con_la_base_vacia()
    {
        await using var db = _base.NuevoContexto();
        Assert.Equal(0, await db.Sedes.CountAsync());
        Assert.Equal(0, await db.Productos.CountAsync());
        Assert.Equal(0, await db.Movimientos.CountAsync());
    }

    [Fact]
    public async Task La_base_rechaza_stock_negativo_como_la_real()
    {
        await using var db = _base.NuevoContexto();
        var datos = await Semilla.CrearAsync(db);
        var fila = await db.ProductoUbicacions.SingleAsync(pu => pu.IdProducto == datos.Caramelo && pu.IdUbicacion == datos.ZonaA01);

        fila.CantidadActual = -1;
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("chk_productoubicacion_cantidad", error.InnerException?.Message);
    }
}
