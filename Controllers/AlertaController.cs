using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    [Authorize]
    public class AlertaController : Controller
    {
        private readonly AppDbContext _context;

        public AlertaController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Alerta/Index
        public async Task<IActionResult> Index(int? pagina, int tamanoPagina = 10)
        {
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            var query = _context.Alerta
                .Include(a => a.IdProductoNavigation)
                .Include(a => a.IdSedeNavigation)
                .Where(a => a.Estado == "pendiente")
                .AsQueryable();

            if (sedeId.HasValue)
                query = query.Where(a => a.IdSede == sedeId.Value);

            int total = await query.CountAsync();
            int paginaActual = pagina ?? 1;
            int totalPaginas = (int)Math.Ceiling(total / (double)tamanoPagina);

            var alertas = await query
                .OrderByDescending(a => a.FechaGenerada)
                .Skip((paginaActual - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(a => new AlertaViewModel
                {
                    IdAlerta = a.IdAlerta,
                    Tipo = a.Tipo,
                    Mensaje = a.Mensaje,
                    Fecha = a.FechaGenerada,
                    ProductoNombre = a.IdProductoNavigation.Nombre,
                    SedeNombre = a.IdSedeNavigation.Nombre
                })
                .ToListAsync();

            ViewBag.PaginaActual = paginaActual;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.SedeNombre = sedeId.HasValue ? (await _context.Sedes.FindAsync(sedeId.Value))?.Nombre : "Todas las sedes";
            return View(alertas);
        }

        [HttpGet]
        public IActionResult ConteoActivas()
        {
            var sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? idSede = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);
            var total = _context.Alerta.Count(a =>
                (!idSede.HasValue || a.IdSede == idSede.Value) && a.Estado == "pendiente");
            return Json(new { total });
        }

        // POST: Alerta/GenerarAlertas
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerarAlertas()
        {
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            var productosConUbicaciones = await _context.Productos
                .Include(p => p.ProductoUbicacions)
                    .ThenInclude(pu => pu.IdUbicacionNavigation)
                .ToListAsync();

            foreach (var producto in productosConUbicaciones)
            {
                var sedesDelProducto = producto.ProductoUbicacions
                    .Select(pu => pu.IdUbicacionNavigation.IdSede)
                    .Distinct();

                // Stock real por sede: suma de ProductoUbicacion.CantidadActual
                // agrupado por sede (fuente de verdad; Producto.StockActual ya no se usa aquí).
                var stockPorSede = producto.ProductoUbicacions
                    .GroupBy(pu => pu.IdUbicacionNavigation.IdSede)
                    .ToDictionary(g => g.Key, g => g.Sum(pu => pu.CantidadActual));

                foreach (int sedeProducto in sedesDelProducto)
                {
                    if (sedeId.HasValue && sedeProducto != sedeId.Value)
                        continue;

                    int stockEnSede = stockPorSede.GetValueOrDefault(sedeProducto, 0);

                    // ---- Alerta por stock mínimo ----
                    var alertaStockExistente = _context.Alerta.FirstOrDefault(a =>
                        a.IdProducto == producto.IdProducto &&
                        a.Tipo == "stock_minimo" &&
                        a.Estado == "pendiente" &&
                        a.IdSede == sedeProducto);

                    if (stockEnSede <= producto.StockMinimo)
                    {
                        if (alertaStockExistente == null)
                        {
                            var alerta = new Alertum
                            {
                                Mensaje = $"Stock bajo: {producto.Nombre} ({stockEnSede} unidades, mínimo {producto.StockMinimo})",
                                FechaGenerada = DateTime.Now,
                                Tipo = "stock_minimo",
                                IdProducto = producto.IdProducto,
                                IdSede = sedeProducto,
                                Estado = "pendiente"
                            };
                            _context.Alerta.Add(alerta);
                        }
                        else
                        {
                            // Ya existe pendiente: refrescar el mensaje con el stock actual,
                            // sin tocar FechaGenerada ni duplicar.
                            alertaStockExistente.Mensaje = $"Stock bajo: {producto.Nombre} ({stockEnSede} unidades, mínimo {producto.StockMinimo})";
                        }
                    }
                    else if (alertaStockExistente != null)
                    {
                        // El stock se recuperó: la alerta pendiente ya no aplica.
                        alertaStockExistente.Estado = "atendida";
                        alertaStockExistente.FechaAtendida = DateTime.Now;
                    }

                    // ---- Alerta por vencimiento próximo ----
                    var alertaVencimientoExistente = _context.Alerta.FirstOrDefault(a =>
                        a.IdProducto == producto.IdProducto &&
                        a.Tipo == "vencimiento" &&
                        a.Estado == "pendiente" &&
                        a.IdSede == sedeProducto);

                    bool vencimientoProximo = false;
                    if (producto.FechaVencimiento.HasValue)
                    {
                        var diasRestantes = producto.FechaVencimiento.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today;
                        vencimientoProximo = diasRestantes.TotalDays <= 30;
                    }

                    if (vencimientoProximo)
                    {
                        if (alertaVencimientoExistente == null)
                        {
                            var alerta = new Alertum
                            {
                                Mensaje = $"Próximo a vencer: {producto.Nombre} (lote {producto.Lote}) – {producto.FechaVencimiento:dd/MM/yyyy}",
                                FechaGenerada = DateTime.Now,
                                Tipo = "vencimiento",
                                IdProducto = producto.IdProducto,
                                IdSede = sedeProducto,
                                Estado = "pendiente"
                            };
                            _context.Alerta.Add(alerta);
                        }
                        else
                        {
                            alertaVencimientoExistente.Mensaje = $"Próximo a vencer: {producto.Nombre} (lote {producto.Lote}) – {producto.FechaVencimiento:dd/MM/yyyy}";
                        }
                    }
                    else if (alertaVencimientoExistente != null)
                    {
                        alertaVencimientoExistente.Estado = "atendida";
                        alertaVencimientoExistente.FechaAtendida = DateTime.Now;
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["Mensaje"] = "Alertas actualizadas correctamente.";
            return RedirectToAction("Index");
        }
    }
}
