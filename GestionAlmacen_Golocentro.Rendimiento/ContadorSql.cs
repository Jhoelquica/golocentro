using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace GestionAlmacen_Golocentro.Rendimiento;

// Registra cada comando SQL que EF Core envía a la base: texto, parámetros y duración. En los SELECT la
// duración es la ejecución (hasta que llegan las primeras filas) más la lectura de todas las filas, que EF
// informa aparte al cerrar el lector.
public sealed class ContadorSql : DbCommandInterceptor
{
    public sealed record Parametro(string Nombre, object? Valor, NpgsqlDbType Tipo, string? TipoDato);
    public sealed record Comando(string Texto, TimeSpan Duracion, IReadOnlyList<Parametro> Parametros)
    {
        public bool EsLectura => Texto.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
    }

    private readonly List<Comando> _comandos = new();
    private readonly Dictionary<Guid, int> _lecturasAbiertas = new();
    public IReadOnlyList<Comando> Comandos { get { lock (_comandos) return _comandos.ToList(); } }
    public void Reiniciar() { lock (_comandos) { _comandos.Clear(); _lecturasAbiertas.Clear(); } }

    private void Registrar(DbCommand cmd, CommandExecutedEventData e, bool lector = false)
    {
        var ps = cmd.Parameters.Cast<NpgsqlParameter>()
            .Select(p => new Parametro(p.ParameterName, p.Value, p.NpgsqlDbType, p.DataTypeName)).ToList();
        lock (_comandos)
        {
            if (lector) _lecturasAbiertas[e.CommandId] = _comandos.Count;
            _comandos.Add(new Comando(cmd.CommandText, e.Duration, ps));
        }
    }

    public override DbDataReader ReaderExecuted(DbCommand c, CommandExecutedEventData e, DbDataReader r) { Registrar(c, e, true); return r; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand c, CommandExecutedEventData e, DbDataReader r, CancellationToken t = default) { Registrar(c, e, true); return new(r); }
    public override InterceptionResult DataReaderDisposing(DbCommand c, DataReaderDisposingEventData e, InterceptionResult r)
    {
        lock (_comandos)
            if (_lecturasAbiertas.Remove(e.CommandId, out var i)) _comandos[i] = _comandos[i] with { Duracion = _comandos[i].Duracion + e.Duration };
        return r;
    }
    public override int NonQueryExecuted(DbCommand c, CommandExecutedEventData e, int r) { Registrar(c, e); return r; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand c, CommandExecutedEventData e, int r, CancellationToken t = default) { Registrar(c, e); return new(r); }
    public override object? ScalarExecuted(DbCommand c, CommandExecutedEventData e, object? r) { Registrar(c, e); return r; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand c, CommandExecutedEventData e, object? r, CancellationToken t = default) { Registrar(c, e); return new(r); }
}
