using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Helpers;
using GestionAlmacen_Golocentro.Services;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    // Las alertas se sincronizan solas (ver AlertasStock); aquí solo se muestran.
    [Authorize]
    public class AlertaController : Controller
    {
        private const int DiasResueltas = 7;
        private readonly AppDbContext _context;

        public AlertaController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            await AlertasStock.Sincronizar(_context);

            var sedeId = User.SedeId();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var pendientes = await _context.Alerta
                .Where(a => a.Estado == AlertasStock.Pendiente && (sedeId == null || a.IdSede == sedeId))
                .Select(a => new AlertaPendiente(
                    a.Tipo,
                    a.IdProducto,
                    a.IdSede,
                    a.FechaGenerada,
                    a.IdSedeNavigation.Nombre,
                    a.IdProductoNavigation.Codigo,
                    a.IdProductoNavigation.Nombre,
                    a.IdProductoNavigation.UnidadMedida,
                    a.IdProductoNavigation.StockMinimo,
                    a.IdProductoNavigation.FechaVencimiento,
                    a.IdProductoNavigation.Lote))
                .ToListAsync();

            // Stock y zonas de hoy de los productos con alerta
            var ids = pendientes.Select(a => a.IdProducto).Distinct().ToList();
            var zonas = await _context.ProductoUbicacions
                .Where(pu => ids.Contains(pu.IdProducto) && pu.CantidadActual > 0)
                .Select(pu => new { pu.IdProducto, pu.IdUbicacionNavigation.IdSede, pu.IdUbicacionNavigation.CodigoEstante, pu.CantidadActual })
                .ToListAsync();

            AlertaItem Item(AlertaPendiente a)
            {
                var enSede = zonas.Where(z => z.IdProducto == a.IdProducto && z.IdSede == a.IdSede).OrderBy(z => z.CodigoEstante).ToList();
                var stock = enSede.Sum(z => z.CantidadActual);
                return new AlertaItem(a.IdProducto, a.Codigo, a.Nombre, a.Unidad, a.Sede, stock, a.StockMinimo,
                    EstadoStock.De(stock, a.StockMinimo), a.FechaVencimiento,
                    a.FechaVencimiento is DateOnly v ? v.DayNumber - hoy.DayNumber : null, a.Lote,
                    string.Join(", ", enSede.Select(z => $"{z.CodigoEstante} ({z.CantidadActual})")), a.FechaGenerada);
            }

            var modelo = new AlertasViewModel
            {
                SedeNombre = sedeId == null ? "Todas las sedes" : pendientes.FirstOrDefault()?.Sede
                    ?? (await _context.Sedes.Where(s => s.IdSede == sedeId).Select(s => s.Nombre).FirstOrDefaultAsync()) ?? "",
                VariasSedes = sedeId == null && await _context.Sedes.CountAsync() > 1,
                PuedeVerKardex = User.IsInRole("duena") || User.IsInRole("encargada"),
                // Lo agotado primero y, entre lo bajo, lo que está más lejos de su mínimo
                Reponer = pendientes.Where(a => a.Tipo == AlertasStock.StockMinimo)
                    .Select(a => Item(a))
                    .OrderBy(i => i.Stock)
                    .ThenBy(i => i.Nombre)
                    .ToList(),
                Vencer = pendientes.Where(a => a.Tipo == AlertasStock.Vencimiento)
                    .Select(a => Item(a))
                    .OrderBy(i => i.Dias)
                    .ToList()
            };

            var desde = DateTime.Now.AddDays(-DiasResueltas);
            modelo.Resueltas = await _context.Alerta
                .Where(a => a.Estado == AlertasStock.Atendida && a.FechaAtendida >= desde && (sedeId == null || a.IdSede == sedeId))
                .OrderByDescending(a => a.FechaAtendida)
                .Take(20)
                .Select(a => new AlertaResuelta(a.Tipo, a.Mensaje, a.FechaGenerada, a.FechaAtendida!.Value, a.IdSedeNavigation.Nombre))
                .ToListAsync();

            return View(modelo);
        }

        // El layout lo pide en cada página para el globito del menú; de paso mantiene las alertas al día
        [HttpGet]
        public async Task<IActionResult> ConteoActivas()
        {
            await AlertasStock.SincronizarSiToca(_context);
            var sedeId = User.SedeId();
            var total = await _context.Alerta.CountAsync(a => a.Estado == AlertasStock.Pendiente && (sedeId == null || a.IdSede == sedeId));
            return Json(new { total });
        }

        private record AlertaPendiente(
            string Tipo, int IdProducto, int IdSede, DateTime FechaGenerada, string Sede, string Codigo, string Nombre,
            string Unidad, int StockMinimo, DateOnly? FechaVencimiento, string? Lote);
    }
}
