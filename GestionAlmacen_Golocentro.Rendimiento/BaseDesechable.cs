using GestionAlmacen_Golocentro.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GestionAlmacen_Golocentro.Rendimiento;

// PostgreSQL 18 desechable en Docker con el esquema real (Database/esquema.sql). Se crea al empezar y se
// destruye al terminar: nunca toca golocentro_pg ni usa contraseñas de nadie (credenciales temporales del contenedor).
public sealed class BaseDesechable : IAsyncDisposable
{
    private const string NombreBase = "golocentro_rendimiento";
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase(NombreBase)
        .Build();

    public string CadenaConexion => _contenedor.GetConnectionString();

    public static async Task<BaseDesechable> CrearAsync()
    {
        var b = new BaseDesechable();
        await b._contenedor.StartAsync();
        await b.CorrerScriptAsync("esquema.sql");
        return b;
    }

    // Corre con psql un script de Database/ (copiado junto a la herramienta) y devuelve lo que imprime
    public async Task<string> CorrerScriptAsync(string archivo)
    {
        var contenido = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Database", archivo));
        await _contenedor.CopyAsync(contenido, "/tmp/" + archivo);
        var r = await _contenedor.ExecAsync(new[] { "psql", "-v", "ON_ERROR_STOP=1", "-q", "-U", "postgres", "-d", NombreBase, "-f", "/tmp/" + archivo });
        if (r.ExitCode != 0)
            throw new InvalidOperationException($"No se pudo correr Database/{archivo}: " + r.Stderr);
        return r.Stdout;
    }

    public AppDbContext NuevoContexto(params IInterceptor[] interceptores) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(CadenaConexion).AddInterceptors(interceptores).Options);

    public async Task<NpgsqlConnection> AbrirAsync()
    {
        var c = new NpgsqlConnection(CadenaConexion);
        await c.OpenAsync();
        return c;
    }

    public async Task<string> VersionAsync()
    {
        await using var c = await AbrirAsync();
        await using var cmd = new NpgsqlCommand("SELECT version()", c);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    // Deja todas las tablas vacías y los contadores de id en 1
    public async Task LimpiarAsync()
    {
        await using var c = await AbrirAsync();
        await using var cmd = new NpgsqlCommand("""
            DO $$ BEGIN
                EXECUTE (SELECT 'TRUNCATE TABLE ' || string_agg(format('%I.%I', schemaname, tablename), ', ') || ' RESTART IDENTITY CASCADE'
                         FROM pg_tables WHERE schemaname = 'public');
            END $$;
            """, c);
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync() => await _contenedor.DisposeAsync();
}
