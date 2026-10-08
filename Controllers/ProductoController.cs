using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Helpers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.Services;
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
            var sedeId = User.SedeId();
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

            var ids = filas.Select(f => f.Id).ToList();
            var presentaciones = (await _context.ProductoPresentacions
                    .Where(pp => ids.Contains(pp.IdProducto))
                    .OrderBy(pp => pp.Factor)
                    .Select(pp => new { pp.IdProducto, pp.Nombre, pp.Factor, pp.Precio })
                    .ToListAsync())
                .GroupBy(pp => pp.IdProducto)
                .ToDictionary(g => g.Key, g => g.Select(pp => new PresentacionVenta(pp.Nombre, pp.Factor, pp.Precio)).ToList());

            return View(new ProductoListaViewModel
            {
                Presentaciones = presentaciones,
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
                Lote = p.Lote,
                Presentaciones = await _context.ProductoPresentacions
                    .Where(pp => pp.IdProducto == id)
                    .OrderBy(pp => pp.Factor)
                    .Select(pp => new PresentacionFormViewModel { Id = pp.IdPresentacion, Nombre = pp.Nombre, Factor = pp.Factor, Precio = pp.Precio })
                    .ToListAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "duena,encargada")]
        public async Task<IActionResult> Editar(int id, ProductoFormViewModel model)
        {
            var producto = await _context.Productos.Include(p => p.ProductoPresentacions).FirstOrDefaultAsync(p => p.IdProducto == id);
            if (producto == null)
                return NotFound();

            model.Id = id;
            await Validar(model, id);
            if (!ModelState.IsValid)
                return await Formulario(model);

            Copiar(model, producto, conVencimiento: !await VencimientoProducto.SaleDeEntradasAsync(_context, id));
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

        // Consulta de stock para todos los roles: cuánto hay de cada producto y en qué zonas.
        // La dueña elige la sede (o todas); los demás ven la suya. Buscar y filtrar se hace en la página.
        public async Task<IActionResult> Stock(int? sede, string? q, string? filtro)
        {
            var sedeUsuario = User.SedeId();
            var sedes = await _context.Sedes.OrderBy(s => s.Nombre).Select(s => new SedeOpcion(s.IdSede, s.Nombre)).ToListAsync();
            var sedeId = sedeUsuario ?? (sedes.Any(s => s.Id == sede) ? sede : null);

            var zonas = (await _context.ProductoUbicacions
                    .Where(pu => pu.CantidadActual > 0 && (sedeId == null || pu.IdUbicacionNavigation.IdSede == sedeId))
                    .Select(pu => new
                    {
                        pu.IdProducto,
                        pu.IdUbicacion,
                        pu.IdUbicacionNavigation.CodigoEstante,
                        Sede = pu.IdUbicacionNavigation.IdSedeNavigation.Nombre,
                        pu.CantidadActual
                    })
                    .ToListAsync())
                .GroupBy(z => z.IdProducto)
                .ToDictionary(g => g.Key, g => g
                    .OrderBy(z => z.Sede).ThenBy(z => z.CodigoEstante)
                    .Select(z => new StockZonaItem(z.IdUbicacion, z.CodigoEstante, z.Sede, z.CantidadActual))
                    .ToList());

            var presentaciones = (await _context.ProductoPresentacions
                    .Select(pp => new { pp.IdProducto, pp.Nombre, pp.Factor })
                    .ToListAsync())
                .ToLookup(pp => pp.IdProducto, pp => (pp.Nombre, pp.Factor));

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var productos = (await _context.Productos
                    .OrderBy(p => p.Nombre)
                    .Select(p => new { p.IdProducto, p.Codigo, p.Nombre, p.Tipo, p.UnidadMedida, p.StockMinimo, p.FechaVencimiento })
                    .ToListAsync())
                .Select(p =>
                {
                    var enZonas = zonas.GetValueOrDefault(p.IdProducto) ?? new();
                    var stock = enZonas.Sum(z => z.Cantidad);
                    // Con presentaciones, el stock también se ve como "2 cajas y 6 unidades"
                    var enPresentaciones = presentaciones[p.IdProducto].Any(x => stock >= x.Factor)
                        ? Presentaciones.Describir(stock, p.UnidadMedida, presentaciones[p.IdProducto])
                        : null;
                    return new StockProductoItem(p.IdProducto, p.Codigo, p.Nombre, p.Tipo, p.UnidadMedida, stock, p.StockMinimo,
                        EstadoStock.De(stock, p.StockMinimo), p.FechaVencimiento,
                        p.FechaVencimiento is DateOnly vence ? vence.DayNumber - hoy.DayNumber : null, enZonas, enPresentaciones);
                })
                .ToList();

            return View(new StockConsultaViewModel
            {
                Productos = productos,
                SedeId = sedeId,
                SedeNombre = sedes.FirstOrDefault(s => s.Id == sedeId)?.Nombre ?? "Todas las sedes",
                PuedeElegirSede = sedeUsuario == null,
                Sedes = sedes,
                PuedeVerKardex = User.IsInRole("duena") || User.IsInRole("encargada"),
                Busqueda = q,
                Filtro = filtro is "reponer" or "sin_stock" or "vencer" ? filtro : "todos"
            });
        }

        [NonAction]
        private async Task<IActionResult> Formulario(ProductoFormViewModel model)
        {
            model.TiposExistentes = await _context.Productos.Select(p => p.Tipo).Distinct().OrderBy(t => t).ToListAsync();
            model.UnidadesExistentes = await _context.Productos.Select(p => p.UnidadMedida).Distinct().OrderBy(u => u).ToListAsync();
            if (model.Id is int id && await VencimientoProducto.SaleDeEntradasAsync(_context, id))
            {
                model.VencimientoPorEntradas = true;
                var actual = await _context.Productos.Where(p => p.IdProducto == id).Select(p => new { p.FechaVencimiento, p.Lote }).SingleAsync();
                (model.FechaVencimiento, model.Lote) = (actual.FechaVencimiento, actual.Lote);
            }
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
            model.Presentaciones = model.Presentaciones
                .Where(f => !Presentaciones.EstaVacia(new Presentaciones.Fila(f.Nombre, f.Factor, f.Precio)))
                .ToList();
            foreach (var f in model.Presentaciones)
                f.Nombre = f.Nombre?.Trim();
            var filas = model.Presentaciones.Select(f => new Presentaciones.Fila(f.Nombre, f.Factor, f.Precio)).ToList();
            foreach (var (i, campo, mensaje) in Presentaciones.Validar(model.UnidadMedida, filas))
                ModelState.AddModelError($"Presentaciones[{i}].{campo}", mensaje);

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
        private static void Copiar(ProductoFormViewModel model, Producto producto, bool conVencimiento = true)
        {
            producto.Nombre = model.Nombre!;
            producto.Codigo = model.Codigo!;
            producto.Tipo = model.Tipo!;
            producto.UnidadMedida = model.UnidadMedida!;
            producto.PrecioUnitario = model.PrecioUnitario!.Value;
            producto.StockMinimo = model.StockMinimo!.Value;
            SincronizarPresentaciones(model.Presentaciones, producto);
            // Si el vencimiento sale de las entradas, no se pisa con lo del formulario
            if (!conVencimiento)
                return;
            producto.FechaVencimiento = model.FechaVencimiento;
            producto.Lote = model.Lote;
        }

        // Las presentaciones del formulario reemplazan a las del producto: se actualizan las que siguen (por Id),
        // se agregan las nuevas y se quitan las que ya no están. Las ventas pasadas no cambian: guardan su presentación.
        [NonAction]
        private static void SincronizarPresentaciones(List<PresentacionFormViewModel> filas, Producto producto)
        {
            var quedan = filas.Where(f => f.Id != null).Select(f => f.Id!.Value).ToHashSet();
            foreach (var quitada in producto.ProductoPresentacions.Where(pp => !quedan.Contains(pp.IdPresentacion)).ToList())
                producto.ProductoPresentacions.Remove(quitada);

            foreach (var f in filas)
            {
                var pp = f.Id is int id ? producto.ProductoPresentacions.FirstOrDefault(x => x.IdPresentacion == id) : null;
                if (pp == null)
                {
                    pp = new ProductoPresentacion();
                    producto.ProductoPresentacions.Add(pp);
                }
                pp.Nombre = f.Nombre!;
                pp.Factor = f.Factor!.Value;
                pp.Precio = f.Precio!.Value;
            }
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
