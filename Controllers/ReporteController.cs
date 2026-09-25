using System.Globalization;
using GestionAlmacen_Golocentro.Data;
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
                    d.PrecioUnitarioSnapshot
                })
                .ToListAsync();

            var importeTotal = lineas.Sum(l => decimal.Round(l.Cantidad * l.PrecioUnitarioSnapshot, 2));
            var filas = lineas
                .GroupBy(l => l.IdProducto)
                .Select(g =>
                {
                    var importe = g.Sum(l => decimal.Round(l.Cantidad * l.PrecioUnitarioSnapshot, 2));
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

        // ===== Auxiliares =====

        [NonAction]
        private int? SedeDelUsuario()
        {
            var claim = User.FindFirst("SedeId")?.Value;
            return string.IsNullOrEmpty(claim) ? null : int.Parse(claim);
        }

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

            var sedeUsuario = SedeDelUsuario();
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
                return new StockReporteFila(p.Codigo, p.Nombre, p.Tipo, p.UnidadMedida, stock, p.StockMinimo, p.PrecioUnitario,
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
                dias.Add(new VentaPorPeriodo(dia.ToString("dd/MM", Inv), dia.ToString("dddd dd/MM/yyyy", new CultureInfo("es-PE")),
                    ventas.Count, ventas.Sum(v => v.Total)));
            }
            return dias;
        }

        [NonAction]
        private static List<VentaPorPeriodo> AgruparPorMes(FiltroReporte filtro, List<VentaReporteFila> validas)
        {
            var es = new CultureInfo("es-PE");
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
