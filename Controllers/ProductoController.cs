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
        private readonly AppDbContext _context;

        public ProductoController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Producto/Index (lista)
        public async Task<IActionResult> Index(int? pagina, int tamanoPagina = 10)
        {
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            var query = _context.Productos.OrderBy(p => p.Nombre).AsQueryable();
            int total = await query.CountAsync();
            int paginaActual = pagina ?? 1;
            int totalPaginas = (int)Math.Ceiling(total / (double)tamanoPagina);

            var productos = await query
                .Skip((paginaActual - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(p => new ProductoListViewModel
                {
                    IdProducto = p.IdProducto,
                    Nombre = p.Nombre,
                    Tipo = p.Tipo,
                    Codigo = p.Codigo,
                    UnidadMedida = p.UnidadMedida,
                    PrecioUnitario = p.PrecioUnitario,
                    StockMinimo = p.StockMinimo,
                    StockActual = p.ProductoUbicacions
                        .Where(pu => !sedeId.HasValue || pu.IdUbicacionNavigation.IdSede == sedeId.Value)
                        .Sum(pu => (int?)pu.CantidadActual) ?? 0
                })
                .ToListAsync();

            ViewBag.PaginaActual = paginaActual;
            ViewBag.TotalPaginas = totalPaginas;
            return View(productos);
        }

        // GET: Producto/Create
        [Authorize(Roles = "duena,encargada")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Producto/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Create(Producto producto)
        {
            if (ModelState.IsValid)
            {
                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"Producto '{producto.Nombre}' creado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        // GET: Producto/Edit/5
        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();
            return View(producto);
        }

        // POST: Producto/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Edit(int id, Producto producto)
        {
            if (id != producto.IdProducto) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // El formulario de Edit no envía StockActual (fuente de verdad real:
                    // ProductoUbicacion.CantidadActual). Sin esto, el binder lo deja en 0
                    // y _context.Update(producto) lo sobrescribiría en la BD.
                    producto.StockActual = await _context.Productos
                        .Where(p => p.IdProducto == id)
                        .Select(p => p.StockActual)
                        .FirstOrDefaultAsync();

                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                    TempData["Mensaje"] = $"Producto '{producto.Nombre}' actualizado.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Productos.Any(p => p.IdProducto == id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        // GET: Producto/Delete/5
        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.IdProducto == id);
            if (producto == null) return NotFound();
            return View(producto);
        }

        // POST: Producto/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.DetalleMovimientos)
                .Include(p => p.Alerta)
                .FirstOrDefaultAsync(p => p.IdProducto == id);

            if (producto == null) return NotFound();

            if (producto.DetalleMovimientos.Any() || producto.Alerta.Any())
            {
                TempData["Error"] = "No se puede eliminar el producto porque tiene movimientos o alertas asociados.";
                return RedirectToAction(nameof(Index));
            }

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            TempData["Mensaje"] = $"Producto '{producto.Nombre}' eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // Acción Stock existente (no la modifiques)
        // ...
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
    }
}