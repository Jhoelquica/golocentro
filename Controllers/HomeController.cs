using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestionAlmacen_Golocentro.Data;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;

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
            // Obtener datos del usuario
            string nombreCompleto = User.FindFirst("NombreCompleto")?.Value ?? User.Identity.Name;
            string rol = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            // Pasar datos básicos
            ViewBag.NombreCompleto = nombreCompleto;
            ViewBag.Rol = rol;

            // Consultas filtradas por sede
            var movimientosQuery = _context.Movimientos.AsQueryable();
            var alertasQuery = _context.Alerta.Where(a => a.Estado == "pendiente").AsQueryable();

            if (sedeId.HasValue)
            {
                movimientosQuery = movimientosQuery.Where(m => m.IdSede == sedeId.Value);
                alertasQuery = alertasQuery.Where(a => a.IdSede == sedeId.Value);
            }

            // Entradas hoy
            var hoy = DateTime.Today;
            var entradasHoy = await movimientosQuery
                .CountAsync(m => m.Tipo == "Entrada" && m.Fecha >= hoy);

            // Salidas hoy
            var salidasHoy = await movimientosQuery
                .CountAsync(m => m.Tipo == "Salida" && m.Fecha >= hoy);

            // Alertas activas
            var alertasActivas = await alertasQuery.CountAsync();

            ViewBag.EntradasHoy = entradasHoy;
            ViewBag.SalidasHoy = salidasHoy;
            ViewBag.AlertasActivas = alertasActivas;

            //ultimos moviminentos y alertas
            // ... código existente de conteos ...
            ViewBag.EntradasHoy = entradasHoy;
            ViewBag.SalidasHoy = salidasHoy;
            ViewBag.AlertasActivas = alertasActivas;

            // Últimos movimientos
            var ultimosMovimientos = await movimientosQuery
                .Include(m => m.IdUsuarioNavigation)
                .OrderByDescending(m => m.Fecha)
                .Take(5)
                .ToListAsync();
            ViewBag.UltimosMovimientos = ultimosMovimientos;

            // Últimas alertas
            var ultimasAlertas = await alertasQuery
                .Include(a => a.IdProductoNavigation)
                .OrderByDescending(a => a.FechaGenerada)
                .Take(5)
                .ToListAsync();
            ViewBag.UltimasAlertas = ultimasAlertas;

            return View();


        }
    }
}