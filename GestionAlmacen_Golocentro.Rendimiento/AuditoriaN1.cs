using GestionAlmacen_Golocentro.Services;
using Npgsql;

namespace GestionAlmacen_Golocentro.Rendimiento;

// Paso b: cuántas consultas SQL genera cada pantalla. Se carga cada una con dos tamaños de datos (el segundo
// 10 veces mayor): si el número de consultas crece con los datos, hay un patrón N+1.
public static class AuditoriaN1
{
    public static async Task EjecutarAsync(BaseDesechable bd)
    {
        var resultados = new Dictionary<string, (int Lect, int Esc, int Filas)[]>();
        var volumenes = new[] { ("chico", Volumen.AuditoriaChico), ("grande", Volumen.AuditoriaGrande) };
        List<Pantalla>? pantallas = null;

        for (var i = 0; i < volumenes.Length; i++)
        {
            var (nombre, v) = volumenes[i];
            await bd.LimpiarAsync();
            Console.WriteLine($"## Datos '{nombre}': {v}");
            Console.WriteLine(await Generador.GenerarAsync(bd, v));
            foreach (var (t, n) in await Generador.ContarFilasAsync(bd))
                if (n > 0) Console.Write($"{t}={n}  ");
            Console.WriteLine();

            // Las alertas quedan al día antes de medir: así el Dashboard no recalcula dentro del minuto
            await using (var db = bd.NuevoContexto())
                await AlertasStock.Sincronizar(db);

            pantallas = Pantallas.Todas(await ProductoConMasMovimientosAsync(bd));
            foreach (var p in pantallas)
            {
                var contador = new ContadorSql();
                await using var db = bd.NuevoContexto(contador);
                await p.Cargar(db);
                var cmds = contador.Comandos;
                if (!resultados.ContainsKey(p.Nombre)) resultados[p.Nombre] = new (int, int, int)[2];
                resultados[p.Nombre][i] = (cmds.Count(c => c.EsLectura), cmds.Count(c => !c.EsLectura), 0);
            }
            Console.WriteLine();
        }

        Console.WriteLine("## Consultas SQL por carga de pantalla (como la dueña, que ve las 2 sedes)");
        Console.WriteLine("| Pantalla | Acción | Lecturas (datos chicos) | Lecturas (datos 10×) | Escrituras (chico / 10×) | ¿Crece con los datos? (N+1) |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var p in pantallas!)
        {
            var r = resultados[p.Nombre];
            Console.WriteLine($"| {p.Nombre} | {p.Accion} | {r[0].Lect} | {r[1].Lect} | {r[0].Esc} / {r[1].Esc} | {(r[1].Lect > r[0].Lect ? "**SÍ**" : "no")} |");
        }
    }

    public static async Task<int> ProductoConMasMovimientosAsync(BaseDesechable bd)
    {
        await using var c = await bd.AbrirAsync();
        await using var cmd = new NpgsqlCommand("SELECT id_producto FROM detalle_movimiento GROUP BY id_producto ORDER BY count(*) DESC, id_producto LIMIT 1", c);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }
}
