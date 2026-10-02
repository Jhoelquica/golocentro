using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace GestionAlmacen_Golocentro.Rendimiento;

// Registra cada comando SQL que EF Core envía a la base: texto, parámetros y duración
public sealed class ContadorSql : DbCommandInterceptor
{
    public sealed record Parametro(string Nombre, object? Valor, NpgsqlDbType Tipo, string? TipoDato);
    public sealed record Comando(string Texto, TimeSpan Duracion, IReadOnlyList<Parametro> Parametros)
    {
        public bool EsLectura => Texto.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
    }

    private readonly List<Comando> _comandos = new();
    public IReadOnlyList<Comando> Comandos { get { lock (_comandos) return _comandos.ToList(); } }
    public void Reiniciar() { lock (_comandos) _comandos.Clear(); }

    private void Registrar(DbCommand cmd, CommandExecutedEventData e)
    {
        var ps = cmd.Parameters.Cast<NpgsqlParameter>()
            .Select(p => new Parametro(p.ParameterName, p.Value, p.NpgsqlDbType, p.DataTypeName)).ToList();
        lock (_comandos) _comandos.Add(new Comando(cmd.CommandText, e.Duration, ps));
    }

    public override DbDataReader ReaderExecuted(DbCommand c, CommandExecutedEventData e, DbDataReader r) { Registrar(c, e); return r; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand c, CommandExecutedEventData e, DbDataReader r, CancellationToken t = default) { Registrar(c, e); return new(r); }
    public override int NonQueryExecuted(DbCommand c, CommandExecutedEventData e, int r) { Registrar(c, e); return r; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand c, CommandExecutedEventData e, int r, CancellationToken t = default) { Registrar(c, e); return new(r); }
    public override object? ScalarExecuted(DbCommand c, CommandExecutedEventData e, object? r) { Registrar(c, e); return r; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand c, CommandExecutedEventData e, object? r, CancellationToken t = default) { Registrar(c, e); return new(r); }
}
