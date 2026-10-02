using System.Diagnostics;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace GestionAlmacen_Golocentro.Rendimiento;

// Paso d (y e): con el volumen de medición, tiempo de cada pantalla (1 corrida de calentamiento + 5 medidas)
// y EXPLAIN (ANALYZE, BUFFERS) de su consulta principal (la más lenta de la última corrida). El EXPLAIN se hace
// en las pantallas principales y en cualquier otra cuya mediana pase de 200 ms.
public static class Medicion
{
    public const int Corridas = 5;
    public const int UmbralMs = 200;
    public const long TablaGrande = 10_000;

    public sealed record Resultado(string Pantalla, string Accion, bool Principal, double Calentamiento, double[] Tiempos,
        int Consultas, double SqlMediana, string? ConsultaPrincipal, string? Explain, double? EjecucionExplainMs, List<string> SeqScansGrandes)
    {
        public double Mediana => Tiempos.OrderBy(t => t).ElementAt(Tiempos.Length / 2);
        public double Maximo => Tiempos.Max();
    }

    public static async Task<List<Resultado>> MedirAsync(BaseDesechable bd, string etiqueta)
    {
        var filasPorTabla = (await Generador.ContarFilasAsync(bd)).ToDictionary(x => x.Tabla, x => x.Filas);
        var pantallas = Pantallas.Todas(await AuditoriaN1.ProductoConMasMovimientosAsync(bd));
        var resultados = new List<Resultado>();

        foreach (var p in pantallas)
        {
            Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] {etiqueta} · {p.Nombre}");
            var calentamiento = await UnaCorrida(bd, p);
            var tiempos = new double[Corridas];
            ContadorSql? ultimo = null;
            var sqlTotales = new double[Corridas];
            for (var i = 0; i < Corridas; i++)
            {
                var (ms, contador) = await UnaCorrida(bd, p);
                tiempos[i] = ms.Ms;
                sqlTotales[i] = contador.Comandos.Sum(c => c.Duracion.TotalMilliseconds);
                ultimo = contador;
            }

            string? sql = null, explain = null;
            double? ejecucion = null;
            var seqGrandes = new List<string>();
            var principal = ultimo!.Comandos.Where(c => c.EsLectura).OrderByDescending(c => c.Duracion).FirstOrDefault();
            var mediana = tiempos.OrderBy(t => t).ElementAt(Corridas / 2);
            if ((p.Principal || mediana > UmbralMs) && principal != null)
            {
                sql = principal.Texto;
                try
                {
                    explain = await ExplainAsync(bd, principal);
                }
                catch (Exception ex)
                {
                    explain = $"EXPLAIN no terminó: {ex.GetBaseException().Message}";
                }
                var m = Regex.Match(explain, @"Execution Time: ([\d.]+) ms");
                if (m.Success) ejecucion = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                foreach (Match s in Regex.Matches(explain, @"Seq Scan on (\w+)"))
                {
                    var t = s.Groups[1].Value;
                    if (filasPorTabla.TryGetValue(t, out var n) && n > TablaGrande && !seqGrandes.Contains(t))
                        seqGrandes.Add(t);
                }
            }
            resultados.Add(new Resultado(p.Nombre, p.Accion, p.Principal, calentamiento.Item1.Ms, tiempos, ultimo.Comandos.Count,
                sqlTotales.OrderBy(t => t).ElementAt(Corridas / 2), sql, explain, ejecucion, seqGrandes));
        }

        Imprimir(resultados, etiqueta, filasPorTabla);
        return resultados;
    }

    private sealed record Medida(double Ms);

    private static async Task<(Medida, ContadorSql)> UnaCorrida(BaseDesechable bd, Pantalla p)
    {
        var contador = new ContadorSql();
        var reloj = Stopwatch.StartNew();
        await using (var db = bd.NuevoContexto(contador))
            await p.Cargar(db);
        reloj.Stop();
        return (new Medida(reloj.Elapsed.TotalMilliseconds), contador);
    }

    private static async Task<string> ExplainAsync(BaseDesechable bd, ContadorSql.Comando cmd)
    {
        await using var c = await bd.AbrirAsync();
        await using var e = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) " + cmd.Texto, c) { CommandTimeout = 120 };
        foreach (var p in cmd.Parametros)
        {
            var np = new NpgsqlParameter { ParameterName = p.Nombre, Value = p.Valor ?? DBNull.Value };
            if (p.TipoDato != null) np.DataTypeName = p.TipoDato;
            else if (p.Tipo != NpgsqlDbType.Unknown) np.NpgsqlDbType = p.Tipo;
            e.Parameters.Add(np);
        }
        var lineas = new List<string>();
        await using var r = await e.ExecuteReaderAsync();
        while (await r.ReadAsync()) lineas.Add(r.GetString(0));
        return string.Join(Environment.NewLine, lineas);
    }

    private static void Imprimir(List<Resultado> rs, string etiqueta, Dictionary<string, long> filas)
    {
        Console.WriteLine($"## Tiempos ({etiqueta}) · 1 corrida de calentamiento + {Corridas} medidas · la dueña (2 sedes)");
        Console.WriteLine("| Pantalla | Acción | Calentamiento (ms) | 5 corridas (ms) | Mediana (ms) | Máximo (ms) | Consultas | SQL por carga, mediana (ms) | > 200 ms |");
        Console.WriteLine("|---|---|---:|---|---:|---:|---:|---:|---|");
        foreach (var r in rs)
            Console.WriteLine($"| {r.Pantalla} | {r.Accion} | {r.Calentamiento:0.0} | {string.Join(" · ", r.Tiempos.Select(t => t.ToString("0.0")))} | " +
                              $"{r.Mediana:0.0} | {r.Maximo:0.0} | {r.Consultas} | {r.SqlMediana:0.0} | {(r.Mediana > UmbralMs ? "**SÍ (mediana)**" : r.Maximo > UmbralMs ? "solo el máximo" : "no")} |");

        Console.WriteLine();
        Console.WriteLine($"## Consulta principal (la más lenta de la última corrida) de las pantallas principales y de las que pasan de {UmbralMs} ms, con EXPLAIN (ANALYZE, BUFFERS)");
        Console.WriteLine($"Tablas grandes (más de {TablaGrande:N0} filas): {string.Join(", ", filas.Where(f => f.Value > TablaGrande).Select(f => $"{f.Key} ({f.Value:N0})"))}");
        Console.WriteLine("| Pantalla | Ejecución de la consulta principal en EXPLAIN (ms) | Seq Scan sobre tablas grandes |");
        Console.WriteLine("|---|---:|---|");
        foreach (var r in rs.Where(r => r.Explain != null))
            Console.WriteLine($"| {r.Pantalla} | {r.EjecucionExplainMs:0.000} | {(r.SeqScansGrandes.Count == 0 ? "ninguno" : "**" + string.Join(", ", r.SeqScansGrandes) + "**")} |");

        foreach (var r in rs.Where(r => r.Explain != null))
        {
            Console.WriteLine();
            Console.WriteLine($"### {r.Pantalla} ({r.Accion})");
            Console.WriteLine("Consulta principal (texto que envía EF Core):");
            Console.WriteLine("```sql");
            Console.WriteLine(r.ConsultaPrincipal);
            Console.WriteLine("```");
            Console.WriteLine("EXPLAIN (ANALYZE, BUFFERS):");
            Console.WriteLine("```");
            Console.WriteLine(r.Explain);
            Console.WriteLine("```");
        }
    }
}
