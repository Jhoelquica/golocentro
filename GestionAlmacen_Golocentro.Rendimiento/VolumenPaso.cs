using Npgsql;

namespace GestionAlmacen_Golocentro.Rendimiento;

// Paso c: genera el volumen de medición, muestra exactamente qué se cargó y comprueba que es reproducible
// (se genera dos veces con la misma semilla y se compara una huella md5 de las tablas grandes).
public static class VolumenPaso
{
    public static async Task EjecutarAsync(BaseDesechable bd)
    {
        var v = Volumen.Medicion;
        Console.WriteLine($"## Volumen: {v}");
        Console.WriteLine("Reglas del generador: 2 sedes; 30 % entradas y 70 % ventas; ventas del 60 % en la sede Principal; " +
                          "3 productos distintos por movimiento; el 5 % de las notas anuladas; el 10 % con descuento; " +
                          "el 30 % de los productos con vencimiento; el ~2 % de los movimientos con fecha de hoy.");

        string? huellaAnterior = null;
        for (var corrida = 1; corrida <= 2; corrida++)
        {
            await bd.LimpiarAsync();
            Console.WriteLine();
            Console.WriteLine($"## Generación {corrida}");
            Console.WriteLine(await Generador.GenerarAsync(bd, v));
            var huella = await HuellaAsync(bd);
            Console.WriteLine($"Huella md5 (producto, producto_ubicacion, movimiento, detalle_movimiento, nota_venta): {huella}");
            if (huellaAnterior != null)
                Console.WriteLine(huella == huellaAnterior
                    ? "Reproducible: SÍ (la segunda generación dio exactamente los mismos datos)"
                    : "Reproducible: NO (las huellas difieren)");
            huellaAnterior = huella;
        }

        Console.WriteLine();
        Console.WriteLine("## Filas por tabla");
        Console.WriteLine("| Tabla | Filas |");
        Console.WriteLine("|---|---:|");
        foreach (var (t, n) in await Generador.ContarFilasAsync(bd))
            Console.WriteLine($"| {t} | {n:N0} |");

        Console.WriteLine();
        Console.WriteLine("## Reparto");
        await using var c = await bd.AbrirAsync();
        foreach (var (titulo, sql) in new[]
        {
            ("Movimientos por sede y tipo", "SELECT s.nombre || ' · ' || m.tipo, count(*) FROM movimiento m JOIN sede s ON s.id_sede = m.id_sede GROUP BY 1 ORDER BY 1"),
            ("Notas de venta por estado", "SELECT estado, count(*) FROM nota_venta GROUP BY 1 ORDER BY 1"),
            ("Fechas de los movimientos", "SELECT 'desde ' || min(fecha)::date || ' hasta ' || max(fecha)::date, count(*) FROM movimiento"),
            ("Movimientos de los últimos 92 días", "SELECT 'últimos 92 días', count(*) FROM movimiento WHERE fecha >= current_date - 92"),
            ("Movimientos de hoy", "SELECT 'hoy', count(*) FROM movimiento WHERE fecha >= current_date"),
            ("Alertas pendientes por tipo", "SELECT tipo, count(*) FROM alerta WHERE estado = 'pendiente' GROUP BY 1 ORDER BY 1"),
        })
        {
            Console.WriteLine($"- {titulo}:");
            await using var cmd = new NpgsqlCommand(sql, c);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) Console.WriteLine($"    {r.GetString(0)}: {r.GetInt64(1):N0}");
        }
    }

    public static async Task<string> HuellaAsync(BaseDesechable bd)
    {
        await using var c = await bd.AbrirAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT md5(
              (SELECT md5(string_agg(t::text, '|' ORDER BY id_producto)) FROM producto t) ||
              (SELECT md5(string_agg(t::text, '|' ORDER BY id)) FROM (SELECT id, id_producto, id_ubicacion, cantidad_actual FROM producto_ubicacion) t) ||
              (SELECT md5(string_agg(t::text, '|' ORDER BY id_movimiento)) FROM movimiento t) ||
              (SELECT md5(string_agg(t::text, '|' ORDER BY id_detalle)) FROM detalle_movimiento t) ||
              (SELECT md5(string_agg(t::text, '|' ORDER BY id_nota)) FROM nota_venta t))
            """, c);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
}
