using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace GestionAlmacen_Golocentro.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            string? sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? null : int.Parse(sedeIdClaim);

            var movimientosQuery = _context.Movimientos.AsQueryable();
            var alertasQuery = _context.Alerta.Where(a => a.Estado == "pendiente");

            if (sedeId.HasValue)
            {
                movimientosQuery = movimientosQuery.Where(m => m.IdSede == sedeId.Value);
                alertasQuery = alertasQuery.Where(a => a.IdSede == sedeId.Value);
            }

            var hoy = DateTime.Today;
            var desde = hoy.AddDays(-6);

            var conteosPorDia = await movimientosQuery
                .Where(m => m.Fecha >= desde)
                .GroupBy(m => new { Dia = m.Fecha.Date, m.Tipo })
                .Select(g => new { g.Key.Dia, g.Key.Tipo, Total = g.Count() })
                .ToListAsync();

            int Contar(DateTime dia, string tipo) =>
                conteosPorDia.Where(c => c.Dia == dia && c.Tipo == tipo).Sum(c => c.Total);

            var ultimosSieteDias = Enumerable.Range(0, 7)
                .Select(i => desde.AddDays(i))
                .Select(dia => new ActividadDia(dia, Contar(dia, "Entrada"), Contar(dia, "Salida")))
                .ToList();

            var alertasPorTipo = await alertasQuery
                .GroupBy(a => a.Tipo)
                .Select(g => new { Tipo = g.Key, Total = g.Count() })
                .ToListAsync();

            var modelo = new DashboardViewModel
            {
                NombreCompleto = User.FindFirst("NombreCompleto")?.Value ?? User.Identity?.Name ?? "",
                Rol = User.FindFirst(ClaimTypes.Role)?.Value,
                EntradasHoy = Contar(hoy, "Entrada"),
                SalidasHoy = Contar(hoy, "Salida"),
                AlertasActivas = alertasPorTipo.Sum(a => a.Total),
                AlertasStockBajo = alertasPorTipo.Where(a => a.Tipo == "stock_minimo").Sum(a => a.Total),
                AlertasVencimiento = alertasPorTipo.Where(a => a.Tipo == "vencimiento").Sum(a => a.Total),
                UltimosSieteDias = ultimosSieteDias,

                UltimosMovimientos = await movimientosQuery
                    .OrderByDescending(m => m.Fecha)
                    .Take(5)
                    .Select(m => new MovimientoResumen(
                        m.IdMovimiento,
                        m.Tipo,
                        m.Fecha,
                        m.IdUsuarioNavigation.Nombre,
                        m.Tipo == "Entrada" ? m.IdProveedorNavigation!.Nombre : m.IdClienteNavigation!.Nombre,
                        m.DetalleMovimientos.Count))
                    .ToListAsync(),

                UltimasAlertas = await alertasQuery
                    .OrderByDescending(a => a.FechaGenerada)
                    .Take(5)
                    .Select(a => new AlertaResumen(a.Tipo, a.Mensaje, a.FechaGenerada))
                    .ToListAsync()
            };

            return View(modelo);
        }
    }
}
