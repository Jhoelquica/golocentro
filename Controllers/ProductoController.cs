using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    [Authorize]
    public class ProductoController : Controller
    {
        private const int TamanoPagina = 12;
        private readonly AppDbContext _context;

        public ProductoController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? q, string? tipo, int pagina = 1)
        {
            var sedeId = SedeDelUsuario();
            var query = _context.Productos.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var patron = $"%{EscaparLike(q.Trim())}%";
                query = query.Where(p => EF.Functions.ILike(p.Nombre, patron)
                                      || EF.Functions.ILike(p.Codigo, patron)
                                      || EF.Functions.ILike(p.Tipo, patron));
            }
            if (!string.IsNullOrWhiteSpace(tipo))
                query = query.Where(p => p.Tipo == tipo);

            var total = await query.CountAsync();
            var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)TamanoPagina));
            pagina = Math.Clamp(pagina, 1, totalPaginas);

            var filas = await query
                .OrderBy(p => p.Nombre)
                .Skip((pagina - 1) * TamanoPagina)
                .Take(TamanoPagina)
                .Select(p => new ProductoFila(
                    p.IdProducto,
                    p.Nombre,
                    p.Codigo,
                    p.Tipo,
                    p.UnidadMedida,
                    p.PrecioUnitario,
                    p.StockMinimo,
                    p.ProductoUbicacions
                        .Where(pu => sedeId == null || pu.IdUbicacionNavigation.IdSede == sedeId)
                        .Sum(pu => pu.CantidadActual),
                    p.FechaVencimiento,
                    p.ProductoUbicacions.Any(pu => pu.CantidadActual > 0)
                        || p.DetalleMovimientos.Any()
                        || p.DetalleTraslados.Any()
                        || p.DetalleAjustes.Any()
                        || p.Alerta.Any()))
                .ToListAsync();

            return View(new ProductoListaViewModel
            {
                Busqueda = q,
                Tipo = tipo,
                Tipos = await _context.Productos.Select(p => p.Tipo).Distinct().OrderBy(t => t).ToListAsync(),
                Pagina = pagina,
                TotalPaginas = totalPaginas,
                Total = total,
                PuedeEditar = User.IsInRole("duena") || User.IsInRole("encargada"),
                Filas = filas
            });
        }

        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Crear(string? desde, string? tipo, string? unidad)
        {
            // Para cargar varios seguidos: "Guardar y crear otro" vuelve con el mismo tipo y unidad
            var model = new ProductoFormViewModel
            {
                Desde = desde,
                Tipo = tipo,
                UnidadMedida = unidad,
                StockMinimo = 0,
                Codigo = await SiguienteCodigo()
            };
            return await Formulario(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Crear(ProductoFormViewModel model, string? accion)
        {
            await Validar(model, null);
            if (!ModelState.IsValid)
                return await Formulario(model);

            var producto = new Producto();
            Copiar(model, producto);
            _context.Productos.Add(producto);
            if (!await GuardarAsync())
                return await Formulario(model);

            TempData["Exito"] = model.Desde switch
            {
                "conteo" => $"Producto «{producto.Nombre}» registrado. Vuelve a la pestaña del conteo: ya aparece al buscarlo.",
                "entrada" => $"Producto «{producto.Nombre}» registrado. Vuelve a la pestaña de la entrada: ya aparece en la lista.",
                _ => $"Producto «{producto.Nombre}» registrado."
            };

            if (accion == "otro")
                return RedirectToAction(nameof(Crear), new { desde = model.Desde, tipo = producto.Tipo, unidad = producto.UnidadMedida });
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Editar(int id)
        {
            var p = await _context.Productos.FindAsync(id);
            if (p == null)
                return NotFound();

            return await Formulario(new ProductoFormViewModel
            {
                Id = p.IdProducto,
                Nombre = p.Nombre,
                Codigo = p.Codigo,
                Tipo = p.Tipo,
                UnidadMedida = p.UnidadMedida,
                PrecioUnitario = p.PrecioUnitario,
                StockMinimo = p.StockMinimo,
                FechaVencimiento = p.FechaVencimiento,
                Lote = p.Lote
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Editar(int id, ProductoFormViewModel model)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();

            model.Id = id;
            await Validar(model, id);
            if (!ModelState.IsValid)
                return await Formulario(model);

            Copiar(model, producto);
            if (!await GuardarAsync())
                return await Formulario(model);

            TempData["Exito"] = $"Producto «{producto.Nombre}» actualizado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();

            // Borrarlo dejaría huérfano el historial (o lo impediría la BD con un error)
            var motivos = new List<string>();
            var enAlmacen = await _context.ProductoUbicacions.Where(pu => pu.IdProducto == id).SumAsync(pu => pu.CantidadActual);
            if (enAlmacen > 0) motivos.Add($"hay {enAlmacen} en el almacén");
            if (await _context.DetalleMovimientos.AnyAsync(d => d.IdProducto == id)) motivos.Add("tiene entradas o salidas");
            if (await _context.DetalleTraslados.AnyAsync(d => d.IdProducto == id)) motivos.Add("tiene movimientos entre zonas");
            if (await _context.DetalleAjustes.AnyAsync(d => d.IdProducto == id)) motivos.Add("aparece en conteos");
            if (await _context.Alerta.AnyAsync(a => a.IdProducto == id)) motivos.Add("tiene alertas");

            if (motivos.Count > 0)
            {
                TempData["Error"] = $"No se puede eliminar «{producto.Nombre}»: {string.Join(", ", motivos)}.";
                return RedirectToAction(nameof(Index));
            }

            // Filas de stock en 0 que quedan tras mover o contar
            _context.ProductoUbicacions.RemoveRange(await _context.ProductoUbicacions.Where(pu => pu.IdProducto == id).ToListAsync());
            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Producto «{producto.Nombre}» eliminado.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Producto/Stock
        public async Task<IActionResult> Stock(string filtroNombre = "")
        {
            // Obtener sede del usuario
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            // Consulta base
            var query = _context.ProductoUbicacions
                .Include(pu => pu.IdProductoNavigation)
                .Include(pu => pu.IdUbicacionNavigation)
                    .ThenInclude(u => u.IdSedeNavigation)
                .AsQueryable();

            // Filtrar por sede si el usuario no es duena
            if (sedeId.HasValue)
            {
                query = query.Where(pu => pu.IdUbicacionNavigation.IdSede == sedeId.Value);
            }

            // Filtro adicional por nombre de producto (si se envía)
            if (!string.IsNullOrWhiteSpace(filtroNombre))
            {
                query = query.Where(pu => pu.IdProductoNavigation.Nombre.Contains(filtroNombre));
            }

            // Proyectar al ViewModel
            var stockList = await query.Select(pu => new StockViewModels
            {
                ProductoNombre = pu.IdProductoNavigation.Nombre,
                CodigoUbicacion = pu.IdUbicacionNavigation.CodigoEstante,
                SedeNombre = pu.IdUbicacionNavigation.IdSedeNavigation.Nombre,
                StockActual = pu.CantidadActual // o pu.Stock, según tu modelo
            }).ToListAsync();

            ViewBag.FiltroNombre = filtroNombre;
            return View(stockList);
        }

        [NonAction]
        private int? SedeDelUsuario()
        {
            var claim = User.FindFirst("SedeId")?.Value;
            return string.IsNullOrEmpty(claim) ? null : int.Parse(claim);
        }

        [NonAction]
        private async Task<IActionResult> Formulario(ProductoFormViewModel model)
        {
            model.TiposExistentes = await _context.Productos.Select(p => p.Tipo).Distinct().OrderBy(t => t).ToListAsync();
            model.UnidadesExistentes = await _context.Productos.Select(p => p.UnidadMedida).Distinct().OrderBy(u => u).ToListAsync();
            return View("Formulario", model);
        }

        // Sugerencia para el siguiente producto: PRD-001, PRD-002… (se puede cambiar)
        [NonAction]
        private async Task<string> SiguienteCodigo()
        {
            var codigos = await _context.Productos
                .Where(p => EF.Functions.ILike(p.Codigo, "PRD-%"))
                .Select(p => p.Codigo)
                .ToListAsync();
            var mayor = codigos
                .Select(c => int.TryParse(c[4..], out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"PRD-{mayor + 1:000}";
        }

        [NonAction]
        private async Task Validar(ProductoFormViewModel model, int? idActual)
        {
            model.Nombre = model.Nombre?.Trim();
            model.Codigo = model.Codigo?.Trim();
            model.Tipo = model.Tipo?.Trim();
            model.UnidadMedida = model.UnidadMedida?.Trim();
            model.Lote = string.IsNullOrWhiteSpace(model.Lote) ? null : model.Lote.Trim();

            // precio_unitario es numeric(10,2): con más decimales la BD redondearía sin avisar
            if (model.PrecioUnitario is decimal precio && decimal.Round(precio, 2) != precio)
                ModelState.AddModelError(nameof(model.PrecioUnitario), "Usa como máximo 2 decimales en el precio.");

            if (!string.IsNullOrEmpty(model.Codigo))
            {
                var duplicados = _context.Productos.Where(p => p.Codigo == model.Codigo);
                if (idActual.HasValue)
                    duplicados = duplicados.Where(p => p.IdProducto != idActual.Value);
                var existente = await duplicados.Select(p => p.Nombre).FirstOrDefaultAsync();
                if (existente != null)
                    ModelState.AddModelError(nameof(model.Codigo), $"Ese código ya lo usa «{existente}».");
            }
        }

        [NonAction]
        private static void Copiar(ProductoFormViewModel model, Producto producto)
        {
            producto.Nombre = model.Nombre!;
            producto.Codigo = model.Codigo!;
            producto.Tipo = model.Tipo!;
            producto.UnidadMedida = model.UnidadMedida!;
            producto.PrecioUnitario = model.PrecioUnitario!.Value;
            producto.StockMinimo = model.StockMinimo!.Value;
            producto.FechaVencimiento = model.FechaVencimiento;
            producto.Lote = model.Lote;
        }

        // Si otro usuario registró el mismo código entre la validación y el guardado
        [NonAction]
        private async Task<bool> GuardarAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                ModelState.AddModelError(nameof(ProductoFormViewModel.Codigo), "Ese código ya está en uso.");
                return false;
            }
        }

        [NonAction]
        private static string EscaparLike(string texto) =>
            texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
