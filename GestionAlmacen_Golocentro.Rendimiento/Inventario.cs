using GestionAlmacen_Golocentro.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestionAlmacen_Golocentro.Rendimiento;

// Paso a: tablas, índices (de la base y declarados en el modelo de EF) y claves foráneas, marcando cuáles
// no tienen un índice que empiece por sus columnas (PostgreSQL no crea índices para las claves foráneas).
public static class Inventario
{
    public static async Task EjecutarAsync(BaseDesechable bd)
    {
        await using var c = await bd.AbrirAsync();

        var tablas = await Lista(c, "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY 1");
        Console.WriteLine($"## Tablas: {tablas.Count}");
        Console.WriteLine(string.Join(", ", tablas));

        Console.WriteLine();
        Console.WriteLine("## Índices de la base (pg_indexes)");
        Console.WriteLine("| Tabla | Índice | Tipo | Columnas |");
        Console.WriteLine("|---|---|---|---|");
        var indices = new List<(string Tabla, string Nombre, string Tipo, string[] Columnas)>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT t.relname, i.relname,
                   CASE WHEN ix.indisprimary THEN 'PRIMARY KEY' WHEN ix.indisunique THEN 'UNIQUE' ELSE 'normal' END,
                   array_agg(a.attname ORDER BY k.ord)
            FROM pg_index ix
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_class t ON t.oid = ix.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace AND n.nspname = 'public'
            CROSS JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            GROUP BY t.relname, i.relname, ix.indisprimary, ix.indisunique
            ORDER BY 1, 2
            """, c))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
            {
                var cols = (string[])r.GetValue(3);
                indices.Add((r.GetString(0), r.GetString(1), r.GetString(2), cols));
                Console.WriteLine($"| {r.GetString(0)} | {r.GetString(1)} | {r.GetString(2)} | {string.Join(", ", cols)} |");
            }
        var porTipo = indices.GroupBy(i => i.Tipo).OrderBy(g => g.Key).Select(g => $"{g.Key}: {g.Count()}");
        Console.WriteLine($"Total: {indices.Count} índices ({string.Join("; ", porTipo)})");

        Console.WriteLine();
        Console.WriteLine("## Índices declarados en el modelo de EF Core (AppDbContext)");
        await using (var db = bd.NuevoContexto())
        {
            var delModelo = db.Model.GetEntityTypes()
                .SelectMany(e => e.GetIndexes().Select(ix => (Tabla: e.GetTableName()!, Nombre: ix.GetDatabaseName() ?? "(sin nombre)", ix.IsUnique,
                    Columnas: string.Join(", ", ix.Properties.Select(p => p.GetColumnName())))))
                .OrderBy(x => x.Tabla).ThenBy(x => x.Nombre).ToList();
            Console.WriteLine("| Tabla | Índice | Único | Columnas | ¿Existe en la base? |");
            Console.WriteLine("|---|---|---|---|---|");
            foreach (var ix in delModelo)
                Console.WriteLine($"| {ix.Tabla} | {ix.Nombre} | {(ix.IsUnique ? "sí" : "no")} | {ix.Columnas} | {(indices.Any(i => i.Nombre == ix.Nombre) ? "sí" : "NO")} |");
            var porConvencion = delModelo.Count(x => x.Nombre.StartsWith("IX_"));
            Console.WriteLine($"Total en el modelo: {delModelo.Count}. {delModelo.Count - porConvencion} están escritos en Data/AppDbContext.cs (HasIndex) y existen en la base; " +
                $"{porConvencion} (IX_…) los agrega EF Core por convención, uno por clave foránea, solo en su modelo en memoria: la base no se creó con migraciones de EF, así que no existen.");
        }

        Console.WriteLine();
        Console.WriteLine("## Claves foráneas y si tienen índice");
        Console.WriteLine("| N.º | Tabla | Clave foránea | Columnas | Referencia | ¿Índice que la cubre? |");
        Console.WriteLine("|---|---|---|---|---|---|");
        var n = 0;
        var sinIndice = 0;
        await using (var cmd = new NpgsqlCommand("""
            SELECT t.relname, con.conname, array_agg(a.attname ORDER BY k.ord), rt.relname
            FROM pg_constraint con
            JOIN pg_class t ON t.oid = con.conrelid
            JOIN pg_class rt ON rt.oid = con.confrelid
            JOIN pg_namespace ns ON ns.oid = t.relnamespace AND ns.nspname = 'public'
            CROSS JOIN LATERAL unnest(con.conkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE con.contype = 'f'
            GROUP BY t.relname, con.conname, rt.relname
            ORDER BY 1, 2
            """, c))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
            {
                n++;
                var tabla = r.GetString(0);
                var cols = (string[])r.GetValue(2);
                // Cubierta si algún índice de la tabla empieza exactamente por esas columnas
                var cubre = indices.FirstOrDefault(i => i.Tabla == tabla && i.Columnas.Length >= cols.Length && i.Columnas.Take(cols.Length).SequenceEqual(cols));
                if (cubre.Nombre == null) sinIndice++;
                Console.WriteLine($"| {n} | {tabla} | {r.GetString(1)} | {string.Join(", ", cols)} | {r.GetString(3)} | {(cubre.Nombre == null ? "**NO**" : "sí: " + cubre.Nombre)} |");
            }
        Console.WriteLine($"Total: {n} claves foráneas; {n - sinIndice} con índice, {sinIndice} sin índice.");
    }

    private static async Task<List<string>> Lista(NpgsqlConnection c, string sql)
    {
        var lista = new List<string>();
        await using var cmd = new NpgsqlCommand(sql, c);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lista.Add(r.GetString(0));
        return lista;
    }
}
