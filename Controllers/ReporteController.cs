using System.Globalization;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Helpers;
using GestionAlmacen_Golocentro.Services;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    // Cada reporte se ve en pantalla, se imprime desde el navegador o se descarga en Excel (formato=excel).
    [Authorize(Roles = "duena,encargada")]
    public class ReporteController : Controller
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly AppDbContext _context;

        public ReporteController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var filtro = await Filtro(null, null, null, nameof(Index));
            var ventas = await VentasDelPeriodo(filtro).Where(n => n.Estado == EstadoNota.Emitida).Select(n => n.Total).ToListAsync();
            var stock = await StockPorProducto(filtro.SedeId);

            return View(new ReporteIndexViewModel
            {
                SedeNombre = filtro.SedeNombre,
                VentasMes = ventas.Sum(),
                NumeroVentasMes = ventas.Count,
                EntradasMes = await EntradasDelPeriodo(filtro).CountAsync(),
                ProductosEnAlerta = stock.Count(s => s.Estado != EstadoStock.Normal),
                ValorStock = stock.Sum(s => s.Valor)
            });
        }

        // ===== Ventas =====
        public async Task<IActionResult> Ventas(DateOnly? desde, DateOnly? hasta, int? sede, string? formato)
        {
            var filtro = await Filtro(desde, hasta, sede, nameof(Ventas));
            var datos = await VentasDelPeriodo(filtro)
                .OrderBy(n => n.IdMovimientoNavigation.Fecha)
                .Select(n => new
                {
                    n.IdMovimiento,
                    n.Serie,
                    n.Numero,
                    n.IdMovimientoNavigation.Fecha,
                    Cliente = n.IdMovimientoNavigation.IdClienteNavigation!.Nombre,
                    Vendedor = n.IdMovimientoNavigation.IdUsuarioNavigation.Nombre,
                    Sede = n.IdMovimientoNavigation.IdSedeNavigation.Nombre,
                    n.MetodoPago,
                    n.Subtotal,
                    n.Descuento,
                    n.Total,
                    Anulada = n.Estado == EstadoNota.Anulada
                })
                .ToListAsync();

            var filas = datos
                .Select(d => new VentaReporteFila(d.IdMovimiento, VentaController.NumeroNota(d.Serie, d.Numero), d.Fecha, d.Cliente,
                    d.Vendedor, d.Sede, MetodosPago.Texto(d.MetodoPago), d.Subtotal, d.Descuento, d.Total, d.Anulada))
                .ToList();
            var validas = filas.Where(f => !f.Anulada).ToList();

            var modelo = new ReporteVentasViewModel
            {
                Filtro = filtro,
                Filas = filas,
                Ventas = validas.Count,
                Anuladas = filas.Count - validas.Count,
                Total = validas.Sum(f => f.Total),
                Descuentos = validas.Sum(f => f.Descuento),
                TicketPromedio = validas.Count == 0 ? 0 : decimal.Round(validas.Sum(f => f.Total) / validas.Count, 2),
                PorMetodo = validas
                    .GroupBy(f => f.MetodoPago)
                    .Select(g => new TotalPorMetodo(g.Key, g.Count(), g.Sum(f => f.Total)))
                    .OrderByDescending(t => t.Total)
                    .ToList(),
                PorVendedor = validas
                    .GroupBy(f => f.Vendedor)
                    .Select(g => new TotalPorVendedor(g.Key, g.Count(), g.Sum(f => f.Total)))
                    .OrderByDescending(t => t.Total)
                    .ToList()
            };

            // Por día si el periodo es corto; por mes si es largo (más de 2 meses no entra en un gráfico por día)
            modelo.PorMes = filtro.Hasta.DayNumber - filtro.Desde.DayNumber > 62;
            modelo.PorPeriodo = modelo.PorMes ? AgruparPorMes(filtro, validas) : AgruparPorDia(filtro, validas);

            if (formato != "excel")
                return View(modelo);

            var hojas = new List<HojaExcel>
            {
                new()
                {
                    Nombre = "Ventas",
                    Columnas = new()
                    {
                        new("Nº nota"), new("Fecha", FormatoColumna.FechaHora), new("Cliente"), new("Atendió"), new("Sede"),
                        new("Pago"), new("Subtotal", FormatoColumna.Moneda), new("Descuento", FormatoColumna.Moneda),
                        new("Total", FormatoColumna.Moneda), new("Estado")
                    },
                    Filas = filas.Select(f => new object?[]
                    {
                        f.Numero, f.Fecha, f.Cliente, f.Vendedor, f.Sede, f.MetodoPago, f.Subtotal, f.Descuento, f.Total,
                        f.Anulada ? "Anulada (no suma)" : "Emitida"
                    }).ToList(),
                    Totales = new object?[] { "Total válido", null, null, null, null, null, validas.Sum(f => f.Subtotal), modelo.Descuentos, modelo.Total, $"{modelo.Ventas} ventas" }
                },
                new()
                {
                    Nombre = modelo.PorMes ? "Por mes" : "Por día",
                    Columnas = new() { new(modelo.PorMes ? "Mes" : "Día"), new("Ventas", FormatoColumna.Entero), new("Total", FormatoColumna.Moneda) },
                    Filas = modelo.PorPeriodo.Select(p => new object?[] { p.EtiquetaLarga, p.Ventas, p.Total }).ToList()
                },
                new()
                {
                    Nombre = "Por método de pago",
                    Columnas = new() { new("Método"), new("Ventas", FormatoColumna.Entero), new("Total", FormatoColumna.Moneda) },
                    Filas = modelo.PorMetodo.Select(m => new object?[] { m.Metodo, m.Ventas, m.Total }).ToList()
                },
                new()
                {
                    Nombre = "Por vendedor",
                    Columnas = new() { new("Atendió"), new("Ventas", FormatoColumna.Entero), new("Total", FormatoColumna.Moneda) },
                    Filas = modelo.PorVendedor.Select(v => new object?[] { v.Vendedor, v.Ventas, v.Total }).ToList()
                }
            };
            return Excel("Reporte de ventas", filtro, hojas, "Ventas");
        }

        // ===== Productos más vendidos =====
        public async Task<IActionResult> Productos(DateOnly? desde, DateOnly? hasta, int? sede, string? formato)
        {
            var filtro = await Filtro(desde, hasta, sede, nameof(Productos));

            // Importe a precio cobrado por línea; los descuentos se aplican a la nota completa, no a cada producto
            var lineas = await _context.DetalleMovimientos
                .Where(d => d.IdMovimientoNavigation.NotaVentum != null && d.IdMovimientoNavigation.NotaVentum.Estado == EstadoNota.Emitida)
                .Where(d => d.IdMovimientoNavigation.Fecha >= filtro.Inicio && d.IdMovimientoNavigation.Fecha < filtro.Fin)
                .Where(d => filtro.SedeId == null || d.IdMovimientoNavigation.IdSede == filtro.SedeId)
                .Select(d => new
                {
                    d.IdProducto,
                    d.IdMovimiento,
                    d.IdProductoNavigation.Codigo,
                    d.IdProductoNavigation.Nombre,
                    d.IdProductoNavigation.UnidadMedida,
                    d.Cantidad,
                    d.Factor,
                    d.PrecioUnitarioSnapshot
                })
                .ToListAsync();

            // La cantidad está en unidades base; el precio, en la presentación vendida (bolsa, caja...)
            var importeTotal = lineas.Sum(l => Presentaciones.Importe(l.Cantidad, l.Factor, l.PrecioUnitarioSnapshot));
            var filas = lineas
                .GroupBy(l => l.IdProducto)
                .Select(g =>
                {
                    var importe = g.Sum(l => Presentaciones.Importe(l.Cantidad, l.Factor, l.PrecioUnitarioSnapshot));
                    var primero = g.First();
                    return new ProductoVendidoFila(primero.Codigo, primero.Nombre, primero.UnidadMedida, g.Sum(l => l.Cantidad), importe,
                        g.Select(l => l.IdMovimiento).Distinct().Count(), importeTotal == 0 ? 0 : importe / importeTotal);
                })
                .OrderByDescending(f => f.Importe)
                .ThenByDescending(f => f.Cantidad)
                .ToList();

            var modelo = new ReporteProductosViewModel
            {
                Filtro = filtro,
                Filas = filas,
                Importe = importeTotal,
                Unidades = filas.Sum(f => f.Cantidad)
            };
            if (formato != "excel")
                return View(modelo);

            var hoja = new HojaExcel
            {
                Nombre = "Productos vendidos",
                Columnas = new()
                {
                    new("Puesto", FormatoColumna.Entero), new("Código"), new("Producto"), new("Unidad"),
                    new("Cantidad", FormatoColumna.Entero), new("Importe", FormatoColumna.Moneda),
                    new("% del importe", FormatoColumna.Porcentaje), new("En ventas", FormatoColumna.Entero)
                },
                Filas = filas.Select((f, i) => new object?[] { i + 1, f.Codigo, f.Nombre, f.Unidad, f.Cantidad, f.Importe, f.Porcentaje, f.Ventas }).ToList(),
                Totales = new object?[] { null, null, "Total", null, modelo.Unidades, modelo.Importe, filas.Count == 0 ? null : 1m, null }
            };
            return Excel("Productos más vendidos (importe antes de descuentos de la nota)", filtro, new[] { hoja }, "ProductosVendidos");
        }

        // ===== Comparativo (UAT 06/10): el periodo contra el anterior, ventas por mes, productos y sedes =====
        private sealed record NotaComparada(int IdSede, DateTime Fecha, decimal Total);
        private sealed record LineaVendida(int IdProducto, string Codigo, string Nombre, string Unidad, int Cantidad, int Factor, decimal Precio, int IdSede);

        [NonAction]
        private Task<List<NotaComparada>> NotasEmitidas(FiltroReporte filtro) =>
            VentasDelPeriodo(filtro)
                .Where(n => n.Estado == EstadoNota.Emitida)
                .Select(n => new NotaComparada(n.IdMovimientoNavigation.IdSede, n.IdMovimientoNavigation.Fecha, n.Total))
                .ToListAsync();

        [NonAction]
        private Task<List<LineaVendida>> LineasVendidas(FiltroReporte filtro) =>
            _context.DetalleMovimientos
                .Where(d => d.IdMovimientoNavigation.NotaVentum != null && d.IdMovimientoNavigation.NotaVentum.Estado == EstadoNota.Emitida)
                .Where(d => d.IdMovimientoNavigation.Fecha >= filtro.Inicio && d.IdMovimientoNavigation.Fecha < filtro.Fin)
                .Where(d => filtro.SedeId == null || d.IdMovimientoNavigation.IdSede == filtro.SedeId)
                .Select(d => new LineaVendida(d.IdProducto, d.IdProductoNavigation.Codigo, d.IdProductoNavigation.Nombre,
                    d.IdProductoNavigation.UnidadMedida, d.Cantidad, d.Factor, d.PrecioUnitarioSnapshot, d.IdMovimientoNavigation.IdSede))
                .ToListAsync();

        public async Task<IActionResult> Comparativo(DateOnly? desde, DateOnly? hasta, int? sede, string? formato)
        {
            var filtro = await Filtro(desde, hasta, sede, nameof(Comparativo));
            var anterior = filtro.PeriodoAnterior();

            var notas = await NotasEmitidas(filtro);
            var notasAntes = await NotasEmitidas(anterior);
            var lineas = await LineasVendidas(filtro);
            var lineasAntes = await LineasVendidas(anterior);

            static decimal Ticket(IReadOnlyCollection<NotaComparada> n) => n.Count == 0 ? 0 : decimal.Round(n.Sum(x => x.Total) / n.Count, 2);
            // Importe a precio cobrado (la cantidad va en unidades base y el precio en la presentación vendida)
            static List<(int Id, string Codigo, string Nombre, string Unidad, int Cantidad, decimal Importe)> Ranking(IEnumerable<LineaVendida> ls) =>
                ls.GroupBy(l => l.IdProducto)
                    .Select(g => (g.Key, g.First().Codigo, g.First().Nombre, g.First().Unidad, g.Sum(l => l.Cantidad),
                        g.Sum(l => Presentaciones.Importe(l.Cantidad, l.Factor, l.Precio))))
                    .OrderByDescending(x => x.Item6).ThenByDescending(x => x.Item5)
                    .ToList();

            var ranking = Ranking(lineas);
            var rankingAntes = Ranking(lineasAntes);
            var puestoAntes = rankingAntes.Select((x, i) => (x.Id, Puesto: i + 1)).ToDictionary(x => x.Id, x => x.Puesto);

            var modelo = new ReporteComparativoViewModel
            {
                Filtro = filtro,
                Anterior = anterior,
                Indicadores = new()
                {
                    new("Total vendido", new(notas.Sum(n => n.Total), notasAntes.Sum(n => n.Total)), true),
                    new("Ventas", new(notas.Count, notasAntes.Count), false),
                    new("Ticket promedio", new(Ticket(notas), Ticket(notasAntes)), true),
                    new("Unidades vendidas", new(lineas.Sum(l => l.Cantidad), lineasAntes.Sum(l => l.Cantidad)), false)
                },
                Productos = ranking.Take(10).Select((x, i) =>
                {
                    var antes = rankingAntes.FirstOrDefault(a => a.Id == x.Id);
                    return new ProductoComparado(i + 1, puestoAntes.TryGetValue(x.Id, out var p) ? p : null, x.Codigo, x.Nombre, x.Unidad,
                        new(x.Cantidad, antes.Cantidad), new(x.Importe, antes.Importe));
                }).ToList()
            };

            // Ventas de los 12 meses que terminan en el mes de "hasta"
            var es = Cultura.Peru;
            var inicioMeses = new DateOnly(filtro.Hasta.Year, filtro.Hasta.Month, 1).AddMonths(-11);
            var porMes = (await NotasEmitidas(new FiltroReporte { Desde = inicioMeses, Hasta = filtro.Hasta, SedeId = filtro.SedeId }))
                .GroupBy(n => (n.Fecha.Year, n.Fecha.Month))
                .ToDictionary(g => g.Key, g => (Ventas: g.Count(), Total: g.Sum(n => n.Total)));
            for (var mes = inicioMeses; mes <= filtro.Hasta; mes = mes.AddMonths(1))
            {
                var v = porMes.GetValueOrDefault((mes.Year, mes.Month));
                modelo.PorMes.Add(new VentaPorPeriodo(mes.ToString("MMM yy", es), mes.ToString("MMMM yyyy", es), v.Ventas, v.Total));
            }

            // Sede contra sede: solo cuando la dueña mira todas las sedes
            if (filtro.SedeId == null && filtro.Sedes.Count > 1)
                modelo.Sedes = filtro.Sedes.Select(s =>
                {
                    var deLaSede = notas.Where(n => n.IdSede == s.Id).ToList();
                    return new SedeComparada(s.Nombre, new(deLaSede.Sum(n => n.Total), notasAntes.Where(n => n.IdSede == s.Id).Sum(n => n.Total)),
                        deLaSede.Count, Ticket(deLaSede), lineas.Where(l => l.IdSede == s.Id).Sum(l => l.Cantidad));
                }).ToList();

            if (formato != "excel")
                return View(modelo);

            static object? Porcentaje(decimal? v) => v is decimal d ? decimal.Round(d * 100, 1) : "—";
            var hojas = new List<HojaExcel>
            {
                new()
                {
                    Nombre = "Resumen",
                    Columnas = new() { new("Indicador"), new(filtro.Periodo), new(anterior.Periodo), new("Variación %") },
                    Filas = modelo.Indicadores.Select(i => new object?[] { i.Nombre, i.Valor.Actual, i.Valor.Anterior, Porcentaje(i.Valor.Variacion) }).ToList()
                },
                new()
                {
                    Nombre = "Por mes",
                    Columnas = new() { new("Mes"), new("Ventas", FormatoColumna.Entero), new("Total", FormatoColumna.Moneda) },
                    Filas = modelo.PorMes.Select(m => new object?[] { m.EtiquetaLarga, m.Ventas, m.Total }).ToList()
                },
                new()
                {
                    Nombre = "Productos",
                    Columnas = new()
                    {
                        new("Puesto", FormatoColumna.Entero), new("Puesto antes"), new("Código"), new("Producto"), new("Unidad"),
                        new("Cantidad", FormatoColumna.Entero), new("Cantidad antes", FormatoColumna.Entero),
                        new("Importe", FormatoColumna.Moneda), new("Importe antes", FormatoColumna.Moneda), new("Variación %")
                    },
                    Filas = modelo.Productos.Select(p => new object?[]
                    {
                        p.Puesto, p.PuestoAnterior?.ToString() ?? "—", p.Codigo, p.Nombre, p.Unidad, (int)p.Cantidad.Actual, (int)p.Cantidad.Anterior,
                        p.Importe.Actual, p.Importe.Anterior, Porcentaje(p.Importe.Variacion)
                    }).ToList()
                }
            };
            if (modelo.Sedes.Count > 0)
                hojas.Add(new()
                {
                    Nombre = "Por sede",
                    Columnas = new()
                    {
                        new("Sede"), new("Total", FormatoColumna.Moneda), new("Total antes", FormatoColumna.Moneda), new("Variación %"),
                        new("Ventas", FormatoColumna.Entero), new("Ticket promedio", FormatoColumna.Moneda), new("Unidades", FormatoColumna.Entero)
                    },
                    Filas = modelo.Sedes.Select(s => new object?[]
                    {
                        s.Sede, s.Total.Actual, s.Total.Anterior, Porcentaje(s.Total.Variacion), s.Ventas, s.Ticket, s.Unidades
                    }).ToList()
                });
            return Excel("Reporte comparativo", filtro, hojas, "Comparativo");
        }

        // ===== Entradas =====
        public async Task<IActionResult> Entradas(DateOnly? desde, DateOnly? hasta, int? sede, string? formato)
        {
            var filtro = await Filtro(desde, hasta, sede, nameof(Entradas));
            var filas = await EntradasDelPeriodo(filtro)
                .OrderBy(m => m.Fecha)
                .Select(m => new EntradaReporteFila(
                    m.IdMovimiento,
                    m.Fecha,
                    m.IdProveedorNavigation != null ? m.IdProveedorNavigation.Nombre : "Sin proveedor",
                    m.Observaciones,
                    m.IdUsuarioNavigation.Nombre,
                    m.IdSedeNavigation.Nombre,
                    m.DetalleMovimientos.Select(d => d.IdProducto).Distinct().Count(),
                    m.DetalleMovimientos.Sum(d => d.Cantidad)))
                .ToListAsync();

            var modelo = new ReporteEntradasViewModel
            {
                Filtro = filtro,
                Filas = filas,
                Unidades = filas.Sum(f => f.Unidades),
                PorProveedor = filas
                    .GroupBy(f => f.Proveedor)
                    .Select(g => new TotalPorProveedor(g.Key, g.Count(), g.Sum(f => f.Unidades)))
                    .OrderByDescending(p => p.Unidades)
                    .ToList()
            };
            if (formato != "excel")
                return View(modelo);

            var idsEntradas = EntradasDelPeriodo(filtro).Select(m => m.IdMovimiento);
            var detalle = await _context.DetalleMovimientos
                .Where(d => idsEntradas.Contains(d.IdMovimiento))
                .OrderBy(d => d.IdMovimientoNavigation.Fecha)
                .Select(d => new object?[]
                {
                    d.IdMovimientoNavigation.Fecha,
                    d.IdMovimientoNavigation.IdProveedorNavigation != null ? d.IdMovimientoNavigation.IdProveedorNavigation.Nombre : "Sin proveedor",
                    d.IdMovimientoNavigation.Observaciones,
                    d.IdProductoNavigation.Codigo,
                    d.IdProductoNavigation.Nombre,
                    d.IdUbicacionNavigation.CodigoEstante,
                    d.Cantidad
                })
                .ToListAsync();

            var hojas = new List<HojaExcel>
            {
                new()
                {
                    Nombre = "Entradas",
                    Columnas = new()
                    {
                        new("Fecha", FormatoColumna.FechaHora), new("Proveedor"), new("Factura / guía"), new("Registró"), new("Sede"),
                        new("Productos", FormatoColumna.Entero), new("Unidades", FormatoColumna.Entero)
                    },
                    Filas = filas.Select(f => new object?[] { f.Fecha, f.Proveedor, f.Comprobante, f.Usuario, f.Sede, f.Productos, f.Unidades }).ToList(),
                    Totales = new object?[] { "Total", null, null, null, null, null, modelo.Unidades }
                },
                new()
                {
                    Nombre = "Detalle por producto",
                    Columnas = new()
                    {
                        new("Fecha", FormatoColumna.FechaHora), new("Proveedor"), new("Factura / guía"), new("Código"),
                        new("Producto"), new("Zona"), new("Cantidad", FormatoColumna.Entero)
                    },
                    Filas = detalle
                },
                new()
                {
                    Nombre = "Por proveedor",
                    Columnas = new() { new("Proveedor"), new("Entradas", FormatoColumna.Entero), new("Unidades", FormatoColumna.Entero) },
                    Filas = modelo.PorProveedor.Select(p => new object?[] { p.Proveedor, p.Entradas, p.Unidades }).ToList()
                }
            };
            return Excel("Reporte de entradas", filtro, hojas, "Entradas");
        }

        // ===== Stock valorizado =====
        public async Task<IActionResult> Stock(int? sede, bool soloAlertas, string? formato)
        {
            var filtro = await Filtro(null, null, sede, nameof(Stock));
            filtro.ConFechas = false;

            var todas = await StockPorProducto(filtro.SedeId);
            var filas = soloAlertas ? todas.Where(f => f.Estado != EstadoStock.Normal).ToList() : todas;

            var modelo = new ReporteStockViewModel
            {
                Filtro = filtro,
                SoloAlertas = soloAlertas,
                Filas = filas,
                Productos = todas.Count,
                SinStock = todas.Count(f => f.Estado == EstadoStock.SinStock),
                Bajos = todas.Count(f => f.Estado == EstadoStock.Bajo),
                Unidades = todas.Sum(f => f.Stock),
                Valor = todas.Sum(f => f.Valor)
            };
            if (formato != "excel")
                return View(modelo);

            var hoja = new HojaExcel
            {
                Nombre = "Stock",
                Columnas = new()
                {
                    new("Código"), new("Producto"), new("Tipo"), new("Unidad"), new("Stock", FormatoColumna.Entero),
                    new("Mínimo", FormatoColumna.Entero), new("Estado"), new("Precio", FormatoColumna.Moneda),
                    new("Valor", FormatoColumna.Moneda), new("Zonas"), new("Vence", FormatoColumna.Fecha)
                },
                Filas = filas.Select(f => new object?[]
                {
                    f.Codigo, f.Nombre, f.Tipo, f.Unidad, f.Stock, f.Minimo, EstadoStock.Texto(f.Estado), f.Precio, f.Valor,
                    f.Zonas, f.Vencimiento
                }).ToList(),
                Totales = new object?[] { null, "Total", null, null, filas.Sum(f => f.Stock), null, null, null, filas.Sum(f => f.Valor), null, null }
            };
            return Excel(soloAlertas ? "Stock bajo o sin stock" : "Stock valorizado (a precio de venta)", filtro, new[] { hoja }, soloAlertas ? "StockBajo" : "Stock");
        }

        // ===== Kardex por producto =====
        // Todo lo que movió el stock de un producto en la sede, en orden, con el saldo después de cada movimiento.
        public async Task<IActionResult> Kardex(int? producto, DateOnly? desde, DateOnly? hasta, int? sede, string? formato)
        {
            var filtro = await Filtro(desde, hasta, sede, nameof(Kardex));
            var sedeId = filtro.SedeId;

            var stockPorProducto = (await _context.ProductoUbicacions
                    .Where(pu => sedeId == null || pu.IdUbicacionNavigation.IdSede == sedeId)
                    .Select(pu => new { pu.IdProducto, pu.CantidadActual, pu.IdUbicacionNavigation.CodigoEstante })
                    .ToListAsync())
                .GroupBy(pu => pu.IdProducto)
                .ToDictionary(g => g.Key, g => g.ToList());

            var modelo = new ReporteKardexViewModel
            {
                Filtro = filtro,
                Productos = (await _context.Productos
                        .OrderBy(p => p.Nombre)
                        .Select(p => new { p.IdProducto, p.Codigo, p.Nombre })
                        .ToListAsync())
                    .Select(p => new KardexProductoOpcion(p.IdProducto, p.Codigo, p.Nombre,
                        stockPorProducto.GetValueOrDefault(p.IdProducto)?.Sum(pu => pu.CantidadActual) ?? 0))
                    .ToList()
            };

            var elegido = await _context.Productos
                .Where(p => p.IdProducto == producto)
                .Select(p => new { p.IdProducto, p.Codigo, p.Nombre, p.UnidadMedida })
                .FirstOrDefaultAsync();
            if (elegido == null)
                return View(modelo);

            modelo.ProductoId = elegido.IdProducto;
            modelo.ProductoNombre = elegido.Nombre;
            modelo.ProductoCodigo = elegido.Codigo;
            modelo.Unidad = elegido.UnidadMedida;
            var zonasAhora = stockPorProducto.GetValueOrDefault(elegido.IdProducto)?.Where(z => z.CantidadActual > 0).OrderBy(z => z.CodigoEstante).ToList() ?? new();
            modelo.StockActual = zonasAhora.Sum(z => z.CantidadActual);
            modelo.ZonasActuales = string.Join(", ", zonasAhora.Select(z => $"{z.CodigoEstante} ({z.CantidadActual})"));

            // Se cargan los movimientos desde el inicio del periodo HASTA HOY: los posteriores al periodo sirven
            // para reconstruir el saldo inicial a partir del stock actual.
            var eventos = await EventosKardex(elegido.IdProducto, filtro.Inicio, sedeId);
            var cambioDesdeInicio = eventos.Sum(e => (e.Entra ?? 0) - (e.Sale ?? 0));
            modelo.SaldoInicial = modelo.StockActual - cambioDesdeInicio;
            modelo.Descuadre = modelo.SaldoInicial < 0;

            var saldo = modelo.SaldoInicial;
            foreach (var e in eventos.Where(e => e.Fecha < filtro.Fin))
            {
                saldo += (e.Entra ?? 0) - (e.Sale ?? 0);
                modelo.Filas.Add(e with { Saldo = saldo });
                switch (e.Tipo)
                {
                    case TipoKardex.Entrada: modelo.Entradas += e.Entra ?? 0; break;
                    case TipoKardex.Venta or TipoKardex.Salida: modelo.Ventas += e.Sale ?? 0; break;
                    case TipoKardex.Anulacion: modelo.Devoluciones += e.Entra ?? 0; break;
                    case TipoKardex.Conteo: modelo.Ajustes += (e.Entra ?? 0) - (e.Sale ?? 0); break;
                }
            }
            modelo.SaldoFinal = saldo;

            if (formato != "excel")
                return View(modelo);

            var todasLasSedes = sedeId == null && filtro.Sedes.Count > 1;
            var columnas = new List<ColumnaExcel>
            {
                new("Fecha", FormatoColumna.FechaHora), new("Movimiento"), new("Documento"), new("Detalle"), new("Zona"),
                new("Entra", FormatoColumna.Entero), new("Sale", FormatoColumna.Entero), new("Saldo", FormatoColumna.Entero), new("Usuario")
            };
            if (todasLasSedes)
                columnas.Add(new("Sede"));

            var filas = new List<object?[]> { new object?[] { filtro.Inicio, "Saldo inicial", null, null, null, null, null, modelo.SaldoInicial, null } };
            filas.AddRange(modelo.Filas.Select(f => new object?[]
            {
                f.Fecha, f.TipoTexto, f.Documento, f.Detalle, f.Zona, f.Entra, f.Sale, f.Saldo, f.Usuario, todasLasSedes ? f.Sede : null
            }));
            var hoja = new HojaExcel
            {
                Nombre = "Kardex",
                Columnas = columnas,
                Filas = filas,
                Totales = new object?[] { null, "Saldo final", null, null, null, modelo.Filas.Sum(f => f.Entra ?? 0), modelo.Filas.Sum(f => f.Sale ?? 0), modelo.SaldoFinal, null }
            };
            return Excel($"Kardex de {elegido.Nombre} ({elegido.Codigo}, en {elegido.UnidadMedida})", filtro, new[] { hoja }, $"Kardex_{elegido.Codigo}");
        }

        // Movimientos de stock de un producto desde una fecha (sin tope), ya ordenados; el saldo se calcula después
        [NonAction]
        private async Task<List<MovimientoKardex>> EventosKardex(int idProducto, DateTime inicio, int? sedeId)
        {
            var eventos = new List<(MovimientoKardex Evento, int Orden)>();

            // Entradas, ventas y salidas antiguas (antes de las notas de venta)
            var detalles = await _context.DetalleMovimientos
                .Where(d => d.IdProducto == idProducto && d.IdMovimientoNavigation.Fecha >= inicio)
                .Where(d => sedeId == null || d.IdMovimientoNavigation.IdSede == sedeId)
                .Select(d => new
                {
                    d.IdMovimiento,
                    d.IdMovimientoNavigation.Tipo,
                    d.IdMovimientoNavigation.Fecha,
                    d.Cantidad,
                    Zona = d.IdUbicacionNavigation.CodigoEstante,
                    Usuario = d.IdMovimientoNavigation.IdUsuarioNavigation.Nombre,
                    Sede = d.IdMovimientoNavigation.IdSedeNavigation.Nombre,
                    Proveedor = d.IdMovimientoNavigation.IdProveedorNavigation != null ? d.IdMovimientoNavigation.IdProveedorNavigation.Nombre : null,
                    Cliente = d.IdMovimientoNavigation.IdClienteNavigation != null ? d.IdMovimientoNavigation.IdClienteNavigation.Nombre : null,
                    Comprobante = d.IdMovimientoNavigation.Observaciones,
                    Serie = d.IdMovimientoNavigation.NotaVentum != null ? d.IdMovimientoNavigation.NotaVentum.Serie : null,
                    Numero = d.IdMovimientoNavigation.NotaVentum != null ? (int?)d.IdMovimientoNavigation.NotaVentum.Numero : null,
                    d.PrecioUnitarioSnapshot,
                    d.Presentacion
                })
                .ToListAsync();

            foreach (var d in detalles)
            {
                if (d.Tipo == "Entrada")
                {
                    eventos.Add((new MovimientoKardex(d.Fecha, TipoKardex.Entrada, "Entrada",
                        string.IsNullOrWhiteSpace(d.Comprobante) ? "Sin factura/guía" : d.Comprobante, d.Proveedor ?? "Sin proveedor",
                        d.Zona, d.Cantidad, null, 0, d.Usuario, d.Sede, Url.Action("Detalle", "Movimiento", new { id = d.IdMovimiento })), 0));
                }
                else if (d.Serie != null)
                {
                    eventos.Add((new MovimientoKardex(d.Fecha, TipoKardex.Venta, "Venta",
                        VentaController.NumeroNota(d.Serie, d.Numero!.Value), $"{d.Cliente} · S/ {d.PrecioUnitarioSnapshot.ToString("0.00", Inv)} {(d.Presentacion == null ? "c/u" : "por " + d.Presentacion)}",
                        d.Zona, null, d.Cantidad, 0, d.Usuario, d.Sede, Url.Action("Nota", "Venta", new { id = d.IdMovimiento })), 1));
                }
                else
                {
                    eventos.Add((new MovimientoKardex(d.Fecha, TipoKardex.Salida, "Salida (sin nota)",
                        $"Movimiento {d.IdMovimiento}", d.Cliente, d.Zona, null, d.Cantidad, 0, d.Usuario, d.Sede,
                        Url.Action("Detalle", "Movimiento", new { id = d.IdMovimiento })), 1));
                }
            }

            // Ventas anuladas: el stock volvió a su zona el día de la anulación
            var anulaciones = await _context.DetalleMovimientos
                .Where(d => d.IdProducto == idProducto && d.IdMovimientoNavigation.NotaVentum != null)
                .Where(d => d.IdMovimientoNavigation.NotaVentum!.Estado == EstadoNota.Anulada && d.IdMovimientoNavigation.NotaVentum.FechaAnulacion >= inicio)
                .Where(d => sedeId == null || d.IdMovimientoNavigation.IdSede == sedeId)
                .Select(d => new
                {
                    d.IdMovimiento,
                    Fecha = d.IdMovimientoNavigation.NotaVentum!.FechaAnulacion!.Value,
                    d.IdMovimientoNavigation.NotaVentum.Serie,
                    d.IdMovimientoNavigation.NotaVentum.Numero,
                    d.IdMovimientoNavigation.NotaVentum.MotivoAnulacion,
                    Usuario = d.IdMovimientoNavigation.NotaVentum.IdUsuarioAnulacionNavigation!.Nombre,
                    d.Cantidad,
                    Zona = d.IdUbicacionNavigation.CodigoEstante,
                    Sede = d.IdMovimientoNavigation.IdSedeNavigation.Nombre
                })
                .ToListAsync();
            eventos.AddRange(anulaciones.Select(a => (new MovimientoKardex(a.Fecha, TipoKardex.Anulacion, "Venta anulada",
                VentaController.NumeroNota(a.Serie, a.Numero), a.MotivoAnulacion, a.Zona, a.Cantidad, null, 0, a.Usuario, a.Sede,
                Url.Action("Nota", "Venta", new { id = a.IdMovimiento })), 2)));

            // Conteos: el stock de la zona pasó a lo contado
            var ajustes = await _context.DetalleAjustes
                .Where(d => d.IdProducto == idProducto && d.IdAjusteNavigation.Fecha >= inicio)
                .Where(d => sedeId == null || d.IdAjusteNavigation.IdSede == sedeId)
                .Select(d => new
                {
                    d.IdAjusteNavigation.Fecha,
                    d.IdAjusteNavigation.Motivo,
                    d.IdAjusteNavigation.Observaciones,
                    d.CantidadAnterior,
                    d.CantidadNueva,
                    Zona = d.IdUbicacionNavigation.CodigoEstante,
                    Usuario = d.IdAjusteNavigation.IdUsuarioNavigation.Nombre,
                    Sede = d.IdAjusteNavigation.IdSedeNavigation.Nombre
                })
                .ToListAsync();
            foreach (var a in ajustes)
            {
                var diferencia = a.CantidadNueva - a.CantidadAnterior;
                var detalle = $"Había {a.CantidadAnterior}, se contaron {a.CantidadNueva}"
                    + (string.IsNullOrWhiteSpace(a.Observaciones) ? "" : $" · {a.Observaciones}");
                eventos.Add((new MovimientoKardex(a.Fecha, TipoKardex.Conteo, diferencia == 0 ? "Conteo (cuadra)" : "Conteo",
                    MotivoAjuste(a.Motivo), detalle, a.Zona,
                    diferencia > 0 ? diferencia : null, diferencia < 0 ? -diferencia : null, 0, a.Usuario, a.Sede, null), 3));
            }

            // Traslados entre zonas: no cambian el stock de la sede, pero muestran dónde está
            var traslados = await _context.DetalleTraslados
                .Where(d => d.IdProducto == idProducto && d.IdTrasladoNavigation.Fecha >= inicio)
                .Where(d => sedeId == null || d.IdTrasladoNavigation.IdSede == sedeId)
                .Select(d => new
                {
                    d.IdTrasladoNavigation.Fecha,
                    d.IdTrasladoNavigation.Observaciones,
                    d.Cantidad,
                    Origen = d.IdUbicacionOrigenNavigation.CodigoEstante,
                    Destino = d.IdUbicacionDestinoNavigation.CodigoEstante,
                    Usuario = d.IdTrasladoNavigation.IdUsuarioNavigation.Nombre,
                    Sede = d.IdTrasladoNavigation.IdSedeNavigation.Nombre
                })
                .ToListAsync();
            eventos.AddRange(traslados.Select(t => (new MovimientoKardex(t.Fecha, TipoKardex.Traslado, "Traslado",
                $"{t.Cantidad} {(t.Cantidad == 1 ? "unidad" : "unidades")}", t.Observaciones, $"{t.Origen} → {t.Destino}",
                null, null, 0, t.Usuario, t.Sede, null), 4)));

            return eventos
                .OrderBy(e => e.Evento.Fecha)
                .ThenBy(e => e.Orden)
                .Select(e => e.Evento)
                .ToList();
        }

        [NonAction]
        private static string MotivoAjuste(string motivo) => motivo switch
        {
            "conteo_inicial" => "Conteo inicial",
            "conteo" => "Conteo",
            "merma" => "Merma",
            "correccion" => "Corrección",
            _ => motivo
        };

        // ===== Auxiliares =====

        // Periodo por defecto: el mes en curso. La encargada queda fija en su sede.
        [NonAction]
        private async Task<FiltroReporte> Filtro(DateOnly? desde, DateOnly? hasta, int? sede, string accion)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var filtro = new FiltroReporte
            {
                Accion = accion,
                Desde = desde ?? new DateOnly(hoy.Year, hoy.Month, 1),
                Hasta = hasta ?? hoy,
                Sedes = await _context.Sedes.OrderBy(s => s.Nombre).Select(s => new SedeOpcion(s.IdSede, s.Nombre)).ToListAsync()
            };
            if (filtro.Hasta < filtro.Desde)
                (filtro.Desde, filtro.Hasta) = (filtro.Hasta, filtro.Desde);

            var sedeUsuario = User.SedeId();
            filtro.PuedeElegirSede = sedeUsuario == null;
            filtro.SedeId = sedeUsuario ?? (filtro.Sedes.Any(s => s.Id == sede) ? sede : null);
            filtro.SedeNombre = filtro.Sedes.FirstOrDefault(s => s.Id == filtro.SedeId)?.Nombre ?? "Todas las sedes";
            return filtro;
        }

        [NonAction]
        private IQueryable<Models.NotaVentum> VentasDelPeriodo(FiltroReporte filtro) =>
            _context.NotaVenta
                .Where(n => n.IdMovimientoNavigation.Fecha >= filtro.Inicio && n.IdMovimientoNavigation.Fecha < filtro.Fin)
                .Where(n => filtro.SedeId == null || n.IdMovimientoNavigation.IdSede == filtro.SedeId);

        [NonAction]
        private IQueryable<Models.Movimiento> EntradasDelPeriodo(FiltroReporte filtro) =>
            _context.Movimientos
                .Where(m => m.Tipo == "Entrada" && m.Fecha >= filtro.Inicio && m.Fecha < filtro.Fin)
                .Where(m => filtro.SedeId == null || m.IdSede == filtro.SedeId);

        // Todos los productos, también los que no tienen stock (son los que hay que reponer)
        [NonAction]
        private async Task<List<StockReporteFila>> StockPorProducto(int? sedeId)
        {
            var productos = await _context.Productos
                .OrderBy(p => p.Nombre)
                .Select(p => new { p.IdProducto, p.Codigo, p.Nombre, p.Tipo, p.UnidadMedida, p.StockMinimo, p.PrecioUnitario, p.FechaVencimiento })
                .ToListAsync();

            var enZonas = (await _context.ProductoUbicacions
                    .Where(pu => pu.CantidadActual > 0 && (sedeId == null || pu.IdUbicacionNavigation.IdSede == sedeId))
                    .Select(pu => new { pu.IdProducto, pu.IdUbicacionNavigation.CodigoEstante, pu.CantidadActual })
                    .ToListAsync())
                .GroupBy(pu => pu.IdProducto)
                .ToDictionary(g => g.Key, g => g.OrderBy(pu => pu.CodigoEstante).ToList());

            return productos.Select(p =>
            {
                var zonas = enZonas.GetValueOrDefault(p.IdProducto) ?? new();
                var stock = zonas.Sum(z => z.CantidadActual);
                return new StockReporteFila(p.IdProducto, p.Codigo, p.Nombre, p.Tipo, p.UnidadMedida, stock, p.StockMinimo, p.PrecioUnitario,
                    stock * p.PrecioUnitario, string.Join(", ", zonas.Select(z => $"{z.CodigoEstante} ({z.CantidadActual})")),
                    EstadoStock.De(stock, p.StockMinimo), p.FechaVencimiento);
            }).ToList();
        }

        [NonAction]
        private static List<VentaPorPeriodo> AgruparPorDia(FiltroReporte filtro, List<VentaReporteFila> validas)
        {
            var porDia = validas.GroupBy(f => DateOnly.FromDateTime(f.Fecha)).ToDictionary(g => g.Key, g => g.ToList());
            var dias = new List<VentaPorPeriodo>();
            for (var dia = filtro.Desde; dia <= filtro.Hasta; dia = dia.AddDays(1))
            {
                var ventas = porDia.GetValueOrDefault(dia) ?? new();
                dias.Add(new VentaPorPeriodo(dia.ToString("dd/MM", Inv), dia.ToString("dddd dd/MM/yyyy", Cultura.Peru),
                    ventas.Count, ventas.Sum(v => v.Total)));
            }
            return dias;
        }

        [NonAction]
        private static List<VentaPorPeriodo> AgruparPorMes(FiltroReporte filtro, List<VentaReporteFila> validas)
        {
            var es = Cultura.Peru;
            var porMes = validas.GroupBy(f => (f.Fecha.Year, f.Fecha.Month)).ToDictionary(g => g.Key, g => g.ToList());
            var meses = new List<VentaPorPeriodo>();
            for (var mes = new DateOnly(filtro.Desde.Year, filtro.Desde.Month, 1); mes <= filtro.Hasta; mes = mes.AddMonths(1))
            {
                var ventas = porMes.GetValueOrDefault((mes.Year, mes.Month)) ?? new();
                meses.Add(new VentaPorPeriodo(mes.ToString("MMM yy", es), mes.ToString("MMMM yyyy", es),
                    ventas.Count, ventas.Sum(v => v.Total)));
            }
            return meses;
        }

        [NonAction]
        private FileContentResult Excel(string titulo, FiltroReporte filtro, IEnumerable<HojaExcel> hojas, string nombreArchivo)
        {
            var subtitulo = filtro.ConFechas
                ? $"{filtro.SedeNombre} · {filtro.Periodo} · generado el {DateTime.Now:dd/MM/yyyy HH:mm}"
                : $"{filtro.SedeNombre} · al {DateTime.Now:dd/MM/yyyy HH:mm}";
            var sufijo = filtro.ConFechas
                ? $"{filtro.Desde:yyyy-MM-dd}_{filtro.Hasta:yyyy-MM-dd}"
                : DateTime.Today.ToString("yyyy-MM-dd", Inv);
            return File(ExportadorExcel.Generar("Distribuidora Golocentro · " + titulo, subtitulo, hojas),
                ExportadorExcel.TipoContenido, $"{nombreArchivo}_{sufijo}.xlsx");
        }
    }
}
