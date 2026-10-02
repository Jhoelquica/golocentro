using System.Diagnostics;
using GestionAlmacen_Golocentro.Services;
using Npgsql;
using NpgsqlTypes;

namespace GestionAlmacen_Golocentro.Rendimiento;

// Volumen de datos sintéticos (inventados) para medir. Reproducible: mismo volumen y misma semilla dan
// exactamente los mismos datos; las fechas se calculan hacia atrás desde el día en que se corre.
public record Volumen(
    int Productos, int Movimientos, int DetallesPorMovimiento, int ZonasPorSede, int Clientes, int Proveedores,
    int Traslados, int Ajustes, int DiasHistorial, int Semilla = 20261002)
{
    // Paso b: dos tamaños chicos para ver si el número de consultas crece con los datos
    public static readonly Volumen AuditoriaChico = new(30, 60, 3, 6, 10, 5, 10, 5, 120);
    public static readonly Volumen AuditoriaGrande = new(300, 600, 3, 6, 50, 10, 100, 50, 120);

    // Pasos c y d: mucho mayor que el uso real
    public static readonly Volumen Medicion = new(2_000, 50_000, 3, 20, 300, 40, 2_000, 500, 365);
}

public static class Generador
{
    private const int Sedes = 2;
    private static readonly string[] Tipos = { "Caramelo", "Chocolate", "Chicle", "Galleta", "Gomita", "Chupetín", "Wafer", "Turrón" };
    private static readonly string[] Unidades = { "unidad", "bolsa", "caja", "paquete" };
    private static readonly string[] Metodos = { "efectivo", "yape", "plin", "transferencia", "tarjeta" };
    private static readonly string[] Motivos = { "conteo_inicial", "conteo", "merma", "correccion" };

    // Devuelve el resumen de lo generado (filas por tabla y tiempo)
    public static async Task<string> GenerarAsync(BaseDesechable bd, Volumen v)
    {
        var reloj = Stopwatch.StartNew();
        var rnd = new Random(v.Semilla);
        var hoy = DateTime.Today;
        DateTime Sin(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Unspecified);
        // Fecha dentro del historial; el ~2 % cae hoy para que las listas "de hoy" tengan datos
        DateTime Fecha() => Sin((rnd.NextDouble() < 0.02 ? hoy : hoy.AddDays(-rnd.Next(1, v.DiasHistorial + 1)))
            .AddHours(8 + rnd.Next(0, 11)).AddMinutes(rnd.Next(0, 60)));

        await using var c = await bd.AbrirAsync();

        // ----- Sedes, zonas, usuarios, clientes, proveedores -----
        await Copiar(c, "sede (id_sede, nombre, direccion, ciudad, estado, plano_ancho, plano_alto)", imp =>
        {
            Fila(imp, 1, "Sede Principal", "Jr. Prueba 123", "Huamanga", "activo", 40m, 20m);
            Fila(imp, 2, "Sede Norte", "Av. Prueba 456", "Huamanga", "activo", 40m, 20m);
        });

        var zonasDeSede = new Dictionary<int, List<int>> { [1] = new(), [2] = new() };
        await Copiar(c, "ubicacion (id_ubicacion, codigo_estante, descripcion, id_sede, tipo, pos_x, pos_y, ancho, alto)", imp =>
        {
            var id = 0;
            for (var s = 1; s <= Sedes; s++)
                for (var z = 0; z < v.ZonasPorSede; z++)
                {
                    id++;
                    zonasDeSede[s].Add(id);
                    // Grilla de 5 columnas dentro de un plano de 40 × 20 (medios cuadritos, sin encimarse)
                    Fila(imp, id, $"{(char)('A' + z / 5)}-{z % 5 + 1:00}", $"Zona {z + 1}", s, z == 0 ? "recepcion" : "almacenaje",
                        (decimal)(z % 5 * 8), (decimal)(z / 5 % 4 * 5), 6m, 4m);
                }
        });

        await Copiar(c, "usuario (id_usuario, nombre, rol, usuario, contrasena, estado, fecha_creacion, id_sede)", imp =>
        {
            Fila(imp, 1, "Dueña de prueba", "duena", "duena.prueba", "sin-contrasena-real", "activo", Sin(hoy), null);
            Fila(imp, 2, "Trabajador Principal", "trabajador", "trabajador.principal", "sin-contrasena-real", "activo", Sin(hoy), 1);
            Fila(imp, 3, "Trabajador Norte", "trabajador", "trabajador.norte", "sin-contrasena-real", "activo", Sin(hoy), 2);
        });

        await Copiar(c, "cliente (id_cliente, nombre, ruc_dni)", imp =>
        {
            Fila(imp, 1, "Público en general", "00000000");
            for (var i = 1; i <= v.Clientes; i++) Fila(imp, i + 1, $"Cliente {i:0000}", (10_000_000 + i).ToString());
        });
        await Copiar(c, "proveedor (id_proveedor, nombre, ruc)", imp =>
        {
            for (var i = 1; i <= v.Proveedores; i++) Fila(imp, i, $"Proveedor {i:000}", (20_000_000_000L + i).ToString());
        });

        // ----- Productos y su stock por zona -----
        var precios = new decimal[v.Productos + 1];
        await Copiar(c, "producto (id_producto, nombre, tipo, codigo, unidad_medida, precio_unitario, stock_actual, stock_minimo, fecha_vencimiento, lote)", imp =>
        {
            for (var p = 1; p <= v.Productos; p++)
            {
                var tipo = Tipos[rnd.Next(Tipos.Length)];
                precios[p] = Math.Round((decimal)(0.10 + rnd.NextDouble() * 49.90), 2);
                DateOnly? vence = rnd.NextDouble() < 0.3 ? DateOnly.FromDateTime(hoy.AddDays(rnd.Next(-15, 400))) : null;
                Fila(imp, p, $"{tipo} {p:00000}", tipo, $"PRD-{p:00000}", Unidades[rnd.Next(Unidades.Length)], precios[p], 0, rnd.Next(0, 41),
                    vence, vence == null ? null : $"L-{rnd.Next(1000, 9999)}");
            }
        });

        var stockDeSede = new Dictionary<int, List<(int Producto, int Zona)>> { [1] = new(), [2] = new() };
        var filasStock = 0;
        await Copiar(c, "producto_ubicacion (id, id_producto, id_ubicacion, cantidad_actual, ultima_actualizacion)", imp =>
        {
            for (var p = 1; p <= v.Productos; p++)
                foreach (var (sede, prob, max) in new[] { (1, 0.8, 2), (2, 0.5, 1) })
                {
                    if (rnd.NextDouble() >= prob) continue;
                    foreach (var zona in zonasDeSede[sede].OrderBy(_ => rnd.Next()).Take(rnd.Next(1, max + 1)))
                    {
                        stockDeSede[sede].Add((p, zona));
                        Fila(imp, ++filasStock, p, zona, rnd.NextDouble() < 0.1 ? 0 : rnd.Next(1, 301), Sin(hoy));
                    }
                }
        });

        // ----- Movimientos (30 % entradas, 70 % ventas), sus detalles y las notas de venta -----
        var movs = new List<(int Id, string Tipo, DateTime Fecha, int Usuario, int? Cliente, int? Proveedor, int Sede)>(v.Movimientos);
        for (var m = 1; m <= v.Movimientos; m++)
        {
            var sede = rnd.NextDouble() < 0.6 ? 1 : 2;
            var entrada = rnd.NextDouble() < 0.3;
            movs.Add((m, entrada ? "Entrada" : "Salida", Fecha(), rnd.NextDouble() < 0.5 ? 1 : sede + 1,
                entrada ? null : rnd.NextDouble() < 0.5 ? 1 : rnd.Next(2, v.Clientes + 2),
                entrada ? rnd.Next(1, v.Proveedores + 1) : null, sede));
        }
        await Copiar(c, "movimiento (id_movimiento, tipo, fecha, id_usuario, id_cliente, id_proveedor, comprobante_emitido, id_sede)", imp =>
        {
            foreach (var m in movs) Fila(imp, m.Id, m.Tipo, m.Fecha, m.Usuario, m.Cliente, m.Proveedor, false, m.Sede);
        });

        var subtotal = new decimal[v.Movimientos + 1];
        var detalles = 0;
        await Copiar(c, "detalle_movimiento (id_detalle, id_movimiento, id_producto, cantidad, precio_unitario_snapshot, stock_anterior, id_ubicacion)", imp =>
        {
            foreach (var m in movs)
                foreach (var (prod, zona) in Distintos(stockDeSede[m.Sede], v.DetallesPorMovimiento, rnd))
                {
                    var cant = rnd.Next(1, 21);
                    subtotal[m.Id] += cant * precios[prod];
                    Fila(imp, ++detalles, m.Id, prod, cant, precios[prod], rnd.Next(20, 301), zona);
                }
        });

        var notas = 0;
        await Copiar(c, "nota_venta (id_nota, id_movimiento, serie, numero, subtotal, descuento, total, metodo_pago, estado, fecha_anulacion, id_usuario_anulacion, motivo_anulacion)", imp =>
        {
            foreach (var m in movs.Where(x => x.Tipo == "Salida"))
            {
                notas++;
                var sub = Math.Round(subtotal[m.Id], 2);
                var desc = rnd.NextDouble() < 0.1 ? Math.Round(sub * 0.05m, 2) : 0m;
                var anulada = rnd.NextDouble() < 0.05;
                Fila(imp, notas, m.Id, "NV01", notas, sub, desc, sub - desc, Metodos[rnd.Next(Metodos.Length)],
                    anulada ? "anulada" : "emitida", anulada ? m.Fecha.AddHours(1) : null, anulada ? 1 : null, anulada ? "Prueba de volumen" : null);
            }
        });

        // ----- Traslados entre zonas y conteos -----
        var detTraslado = 0;
        var traslados = Enumerable.Range(1, v.Traslados).Select(t => (Id: t, Sede: rnd.NextDouble() < 0.6 ? 1 : 2, Fecha: Fecha())).ToList();
        await Copiar(c, "traslado (id_traslado, fecha, id_usuario, id_sede)", imp =>
        {
            foreach (var t in traslados) Fila(imp, t.Id, t.Fecha, t.Sede + 1, t.Sede);
        });
        await Copiar(c, "detalle_traslado (id_detalle, id_traslado, id_producto, cantidad, id_ubicacion_origen, id_ubicacion_destino)", imp =>
        {
            foreach (var t in traslados)
                foreach (var (prod, origen) in Distintos(stockDeSede[t.Sede], rnd.Next(1, 3), rnd))
                {
                    var destino = zonasDeSede[t.Sede].Where(z => z != origen).ElementAt(rnd.Next(v.ZonasPorSede - 1));
                    Fila(imp, ++detTraslado, t.Id, prod, rnd.Next(1, 11), origen, destino);
                }
        });

        var detAjuste = 0;
        var ajustes = Enumerable.Range(1, v.Ajustes).Select(a => (Id: a, Sede: rnd.NextDouble() < 0.6 ? 1 : 2, Fecha: Fecha())).ToList();
        await Copiar(c, "ajuste_inventario (id_ajuste, fecha, id_usuario, id_sede, motivo)", imp =>
        {
            foreach (var a in ajustes) Fila(imp, a.Id, a.Fecha, a.Sede + 1, a.Sede, Motivos[rnd.Next(Motivos.Length)]);
        });
        await Copiar(c, "detalle_ajuste (id_detalle, id_ajuste, id_producto, id_ubicacion, cantidad_anterior, cantidad_nueva)", imp =>
        {
            foreach (var a in ajustes)
                foreach (var (prod, zona) in Distintos(stockDeSede[a.Sede], rnd.Next(1, 4), rnd))
                    Fila(imp, ++detAjuste, a.Id, prod, zona, rnd.Next(0, 301), rnd.Next(0, 301));
        });

        // Los contadores de id siguen después de lo cargado, y las estadísticas quedan al día (como tras el autovacuum)
        await Ejecutar(c, """
            DO $$ DECLARE r record; BEGIN
              FOR r IN SELECT c.relname AS t, a.attname AS col FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid
                       JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'public' WHERE a.attidentity <> '' LOOP
                EXECUTE format('SELECT setval(pg_get_serial_sequence(%L, %L), GREATEST(COALESCE((SELECT max(%I) FROM %I), 0), 1))', r.t, r.col, r.col, r.t);
              END LOOP; END $$;
            ANALYZE;
            """);

        // Alertas: las calcula el propio sistema, como al abrir cualquier página
        await using (var db = bd.NuevoContexto())
            await AlertasStock.Sincronizar(db);

        reloj.Stop();
        return $"Generado en {reloj.Elapsed.TotalSeconds:0.0} s (semilla {v.Semilla}, fechas hasta {v.DiasHistorial} días atrás desde {hoy:dd/MM/yyyy})";
    }

    public static async Task<List<(string Tabla, long Filas)>> ContarFilasAsync(BaseDesechable bd)
    {
        await using var c = await bd.AbrirAsync();
        var tablas = new List<string>();
        await using (var cmd = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY 1", c))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) tablas.Add(r.GetString(0));
        var res = new List<(string, long)>();
        foreach (var t in tablas)
        {
            await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM \"{t}\"", c);
            res.Add((t, (long)(await cmd.ExecuteScalarAsync())!));
        }
        return res;
    }

    // ----- Ayudas -----

    private static IEnumerable<(int Producto, int Zona)> Distintos(List<(int Producto, int Zona)> filas, int cuantos, Random rnd)
    {
        var elegidos = new List<(int, int)>();
        var productos = new HashSet<int>();
        for (var intentos = 0; elegidos.Count < cuantos && intentos < cuantos * 20; intentos++)
        {
            var f = filas[rnd.Next(filas.Count)];
            if (productos.Add(f.Producto)) elegidos.Add(f);
        }
        return elegidos;
    }

    private static async Task Copiar(NpgsqlConnection c, string destino, Action<NpgsqlBinaryImporter> escribir)
    {
        await using var imp = await c.BeginBinaryImportAsync($"COPY {destino} FROM STDIN (FORMAT BINARY)");
        escribir(imp);
        await imp.CompleteAsync();
    }

    private static async Task Ejecutar(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void Fila(NpgsqlBinaryImporter imp, params object?[] valores)
    {
        imp.StartRow();
        foreach (var v in valores)
            switch (v)
            {
                case null: imp.WriteNull(); break;
                case int i: imp.Write(i, NpgsqlDbType.Integer); break;
                case long l: imp.Write(l, NpgsqlDbType.Bigint); break;
                case string s: imp.Write(s, NpgsqlDbType.Varchar); break;
                case decimal d: imp.Write(d, NpgsqlDbType.Numeric); break;
                case bool b: imp.Write(b, NpgsqlDbType.Boolean); break;
                case DateTime t: imp.Write(t, NpgsqlDbType.Timestamp); break;
                case DateOnly f: imp.Write(f, NpgsqlDbType.Date); break;
                default: throw new NotSupportedException(v.GetType().Name);
            }
    }
}
