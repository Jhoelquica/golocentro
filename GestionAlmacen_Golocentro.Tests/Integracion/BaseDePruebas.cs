using GestionAlmacen_Golocentro.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// PostgreSQL desechable para las pruebas de integración: un contenedor de Docker que se crea al empezar
// y se destruye al terminar. Nunca toca golocentro_pg ni ninguna base real, y no usa contraseñas de nadie
// (el contenedor trae las suyas, temporales). Las tablas salen de Database/esquema.sql, el mismo esquema
// de la base real (con sus CHECK, UNIQUE y llaves foráneas).
public sealed class BaseDePruebas : IAsyncLifetime
{
    private const string NombreBase = "golocentro_pruebas";

    // Misma versión mayor que la base de desarrollo (PostgreSQL 18): el esquema trae comandos de psql 18
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase(NombreBase)
        .Build();

    public string CadenaConexion => _contenedor.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _contenedor.StartAsync();

        var esquema = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Database", "esquema.sql"));
        await _contenedor.CopyAsync(esquema, "/tmp/esquema.sql");
        var resultado = await _contenedor.ExecAsync(new[]
        {
            "psql", "-v", "ON_ERROR_STOP=1", "-q", "-U", "postgres", "-d", NombreBase, "-f", "/tmp/esquema.sql"
        });
        if (resultado.ExitCode != 0)
            throw new InvalidOperationException($"No se pudo cargar Database/esquema.sql: {resultado.Stderr}");
    }

    public async Task DisposeAsync() => await _contenedor.DisposeAsync();

    public AppDbContext NuevoContexto(params IInterceptor[] interceptores) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(CadenaConexion).AddInterceptors(interceptores).Options);

    // Deja todas las tablas vacías (y los contadores de id en 1) antes de cada prueba
    public async Task LimpiarAsync()
    {
        await using var db = NuevoContexto();
        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                EXECUTE (SELECT 'TRUNCATE TABLE ' || string_agg(format('%I.%I', schemaname, tablename), ', ') || ' RESTART IDENTITY CASCADE'
                         FROM pg_tables WHERE schemaname = 'public');
            END $$;
            """);
    }
}

// Todas las pruebas de integración comparten un solo contenedor y corren una tras otra
[CollectionDefinition(Nombre)]
public class ColeccionBaseDeDatos : ICollectionFixture<BaseDePruebas>
{
    public const string Nombre = "Base de datos de pruebas";
}
