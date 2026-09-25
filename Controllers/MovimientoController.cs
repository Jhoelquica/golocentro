using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.Services;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    [Authorize]
    public class MovimientoController : Controller
    {
        private readonly AppDbContext _context;

        public MovimientoController(AppDbContext context)
        {
            _context = context;
        }

        // Extrae el UsuarioId del claim de forma segura.
        // Devuelve un IActionResult (redirect a Login) si el claim falta o está vacío;
        // si devuelve null, usuarioId ya quedó seteado y es seguro continuar.
        [NonAction]
        private IActionResult? ObtenerUsuarioIdOFallar(out int usuarioId)
        {
            var usuarioIdClaim = User.FindFirst("UsuarioId")?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim))
            {
                usuarioId = 0;
                return RedirectToAction("Login", "Account");
            }

            usuarioId = int.Parse(usuarioIdClaim);
            return null;
        }

        // Para la dueña (sin sede) se muestra la sede de cada ubicación, porque ve las de todas.
        [NonAction]
        private List<(int Id, string Texto, bool EsRecepcion)> UbicacionesParaUsuario(int? sedeId)
        {
            var query = _context.Ubicaciones.AsQueryable();
            if (sedeId.HasValue)
                query = query.Where(u => u.IdSede == sedeId.Value);

            return query
                .OrderBy(u => u.IdSedeNavigation.Nombre)
                .ThenBy(u => u.CodigoEstante)
                .Select(u => new { u.IdUbicacion, u.CodigoEstante, u.Descripcion, u.Tipo, Sede = u.IdSedeNavigation.Nombre })
                .ToList()
                .Select(u =>
                {
                    var texto = string.IsNullOrEmpty(u.Descripcion) ? u.CodigoEstante : $"{u.CodigoEstante} — {u.Descripcion}";
                    return (u.IdUbicacion, sedeId.HasValue ? texto : $"{texto} · {u.Sede}", u.Tipo == "recepcion");
                })
                .ToList();
        }

        // Zonas de Entrada con el stock actual de cada producto, para sugerir recibir donde ya está
        // (reponer ahí) o en Recepción si el producto no está en ninguna zona.
        [NonAction]
        private void CargarZonasEntrada(int? sedeId)
        {
            ViewBag.Ubicaciones = UbicacionesParaUsuario(sedeId)
                .Select(u => new { value = u.Id.ToString(), text = u.Texto, recepcion = u.EsRecepcion })
                .ToList();

            ViewBag.StockPorProducto = _context.ProductoUbicacions
                .Where(pu => pu.CantidadActual > 0 && (!sedeId.HasValue || pu.IdUbicacionNavigation.IdSede == sedeId.Value))
                .Select(pu => new { pu.IdProducto, pu.IdUbicacion, pu.CantidadActual })
                .ToList()
                .GroupBy(pu => pu.IdProducto)
                .ToDictionary(g => g.Key, g => g.Select(pu => new { u = pu.IdUbicacion, c = pu.CantidadActual }).ToList());
        }

        // GET: Movimiento/Entrada
        public IActionResult Entrada()
        {
            string? sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? null : int.Parse(sedeIdClaim);

            ViewBag.Productos = _context.Productos
                .OrderBy(p => p.Nombre)
                .Select(p => new SelectListItem { Value = p.IdProducto.ToString(), Text = p.Nombre })
                .ToList();

            CargarZonasEntrada(sedeId);

            ViewBag.Proveedores = _context.Proveedores.Select(p => new SelectListItem
            {
                Value = p.IdProveedor.ToString(),
                Text = p.Nombre
            }).ToList();

            return View(new MovimientoEntradaViewModel());
        }

        // POST: Movimiento/Entrada
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Entrada(MovimientoEntradaViewModel model)
        {
            string? sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? null : int.Parse(sedeIdClaim);

            var redirectSiFalla = ObtenerUsuarioIdOFallar(out int usuarioId);
            if (redirectSiFalla != null)
                return redirectSiFalla;

            // 1. Validar que haya al menos un detalle, que los productos existan
            //    y que las ubicaciones sean de una sola sede
            int? sedeMovimiento = null;
            Dictionary<int, decimal> precios = new();
            if (model.Detalles == null || !model.Detalles.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un producto.");
            }
            else
            {
                var (sedeResuelta, errorSede) = await OperacionesAlmacen.ResolverSedeMovimiento(_context, model.Detalles.Select(d => d.UbicacionId), sedeId);
                if (errorSede != null)
                    ModelState.AddModelError("", errorSede);
                sedeMovimiento = sedeResuelta;

                var idsProducto = model.Detalles.Select(d => d.ProductoId).Distinct().ToList();
                precios = await _context.Productos
                    .Where(p => idsProducto.Contains(p.IdProducto))
                    .ToDictionaryAsync(p => p.IdProducto, p => p.PrecioUnitario);
                if (precios.Count != idsProducto.Count)
                    ModelState.AddModelError("", "Selecciona un producto válido en cada fila.");
            }

            if (OperacionesAlmacen.ValidarEvidencia(model.Evidencia) is string errorFoto)
                ModelState.AddModelError(nameof(model.Evidencia), errorFoto);

            if (!ModelState.IsValid)
            {
                CargarListasParaVista(sedeId);
                return View(model);
            }

            // 2. Crear el movimiento con sus detalles y actualizar el stock en un solo guardado
            var movimiento = new Movimiento
            {
                Tipo = "Entrada",
                Fecha = DateTime.Now,
                IdUsuario = usuarioId,
                IdSede = sedeMovimiento!.Value,
                IdProveedor = model.ProveedorId,
                ComprobanteEmitido = !string.IsNullOrEmpty(model.NumeroFactura),
                Observaciones = model.NumeroFactura
            };
            _context.Movimientos.Add(movimiento);

            // El diccionario evita duplicar la fila de stock si el mismo producto+ubicación viene dos veces
            var stocks = await OperacionesAlmacen.CargarStocks(_context, model.Detalles!.Select(d => (d.ProductoId, d.UbicacionId)));
            foreach (var detalleVM in model.Detalles!)
            {
                var clave = (detalleVM.ProductoId, detalleVM.UbicacionId);
                if (!stocks.TryGetValue(clave, out var stock))
                {
                    stock = new ProductoUbicacion { IdProducto = detalleVM.ProductoId, IdUbicacion = detalleVM.UbicacionId };
                    _context.ProductoUbicacions.Add(stock);
                    stocks[clave] = stock;
                }

                movimiento.DetalleMovimientos.Add(new DetalleMovimiento
                {
                    IdProducto = detalleVM.ProductoId,
                    IdUbicacion = detalleVM.UbicacionId,
                    Cantidad = detalleVM.Cantidad,
                    PrecioUnitarioSnapshot = precios[detalleVM.ProductoId],
                    StockAnterior = stock.CantidadActual
                });

                stock.CantidadActual += detalleVM.Cantidad;
                stock.UltimaActualizacion = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            if (model.Evidencia is { Length: > 0 })
                await OperacionesAlmacen.GuardarEvidencia(_context, movimiento.IdMovimiento, model.Evidencia, "Entrada");

            TempData["Mensaje"] = "Entrada registrada correctamente.";
            return RedirectToAction("Index");
        }

        // GET: Movimiento/Index (lista de movimientos)
        public async Task<IActionResult> Index(int? pagina, int tamanoPagina = 10)
        {
            string? sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? null : int.Parse(sedeIdClaim);

            var query = _context.Movimientos
                .Include(m => m.IdUsuarioNavigation)
                .Include(m => m.IdSedeNavigation)
                .Include(m => m.IdProveedorNavigation)
                .Include(m => m.Evidencia)
                .AsQueryable();

            if (sedeId.HasValue)
                query = query.Where(m => m.IdSede == sedeId.Value);

            int total = await query.CountAsync();
            int paginaActual = pagina ?? 1;
            int totalPaginas = (int)Math.Ceiling(total / (double)tamanoPagina);

            var movimientos = await query
                .OrderByDescending(m => m.Fecha)
                .Skip((paginaActual - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .ToListAsync();

            ViewBag.PaginaActual = paginaActual;
            ViewBag.TotalPaginas = totalPaginas;
            return View(movimientos);
        }

        // Método auxiliar para recargar los ViewBag en caso de error
        [NonAction]
        private IActionResult CargarListasParaVista(int? sedeId)
        {
            ViewBag.Productos = new SelectList(_context.Productos.OrderBy(p => p.Nombre), "IdProducto", "Nombre");
            CargarZonasEntrada(sedeId);

            ViewBag.Proveedores = new SelectList(_context.Proveedores, "IdProveedor", "Nombre");
            return View("Entrada");
        }

        // Las salidas ahora son ventas con nota de venta
        public IActionResult Salida() => RedirectToAction("Nueva", "Venta");

        // GET: Movimiento/Detalle/5
        public async Task<IActionResult> Detalle(int id)
        {
            var movimiento = await _context.Movimientos
                .Include(m => m.IdUsuarioNavigation)
                .Include(m => m.IdSedeNavigation)
                .Include(m => m.IdProveedorNavigation)
                .Include(m => m.IdClienteNavigation)
                .Include(m => m.DetalleMovimientos)
                    .ThenInclude(d => d.IdProductoNavigation)
                .Include(m => m.DetalleMovimientos)
                    .ThenInclude(d => d.IdUbicacionNavigation)
                .Include(m => m.Evidencia)
                .FirstOrDefaultAsync(m => m.IdMovimiento == id);

            if (movimiento == null)
                return NotFound();

            // Verificar que el usuario tenga acceso a la sede del movimiento
            string? sedeIdClaim = User.FindFirst("SedeId")?.Value;
            if (!string.IsNullOrEmpty(sedeIdClaim))
            {
                int sedeId = int.Parse(sedeIdClaim);
                if (movimiento.IdSede != sedeId)
                    return RedirectToAction("AccessDenied", "Account");
            }

            return View(movimiento);
        }
    }
}
