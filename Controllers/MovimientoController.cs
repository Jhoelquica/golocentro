using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
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

        // La sede de un movimiento es la de sus ubicaciones (la dueña no tiene sede en el claim).
        // Todas deben ser de una misma sede y, si el usuario tiene sede asignada, de la suya.
        [NonAction]
        private async Task<(int? SedeId, string? Error)> ResolverSedeMovimiento(IEnumerable<int> idsUbicacion, int? sedeUsuario)
        {
            var ids = idsUbicacion.Distinct().ToList();
            var ubicaciones = await _context.Ubicaciones
                .Where(u => ids.Contains(u.IdUbicacion))
                .Select(u => new { u.IdUbicacion, u.IdSede })
                .ToListAsync();

            if (ubicaciones.Count != ids.Count)
                return (null, "Selecciona una ubicación válida para cada producto.");

            var sedes = ubicaciones.Select(u => u.IdSede).Distinct().ToList();
            if (sedes.Count > 1)
                return (null, "Todos los productos de un movimiento deben estar en ubicaciones de la misma sede. Registra un movimiento por cada sede.");

            if (sedeUsuario.HasValue && sedes[0] != sedeUsuario.Value)
                return (null, "Las ubicaciones seleccionadas no pertenecen a tu sede.");

            return (sedes[0], null);
        }

        // Stock actual (entidades rastreadas) de cada par producto+ubicación; los pares sin fila no aparecen.
        [NonAction]
        private async Task<Dictionary<(int, int), ProductoUbicacion>> CargarStocks(IEnumerable<(int ProductoId, int UbicacionId)> pares)
        {
            var lista = pares.Distinct().ToList();
            var idsProducto = lista.Select(p => p.ProductoId).Distinct().ToList();
            var idsUbicacion = lista.Select(p => p.UbicacionId).Distinct().ToList();

            var filas = await _context.ProductoUbicacions
                .Where(pu => idsProducto.Contains(pu.IdProducto) && idsUbicacion.Contains(pu.IdUbicacion))
                .ToListAsync();

            return filas.ToDictionary(pu => (pu.IdProducto, pu.IdUbicacion));
        }

        // Para la dueña (sin sede) se muestra la sede de cada ubicación, porque ve las de todas.
        [NonAction]
        private List<(int Id, string Texto)> UbicacionesParaUsuario(int? sedeId)
        {
            var query = _context.Ubicaciones.AsQueryable();
            if (sedeId.HasValue)
                query = query.Where(u => u.IdSede == sedeId.Value);

            return query
                .OrderBy(u => u.IdSedeNavigation.Nombre)
                .ThenBy(u => u.CodigoEstante)
                .Select(u => new { u.IdUbicacion, u.CodigoEstante, u.Descripcion, Sede = u.IdSedeNavigation.Nombre })
                .ToList()
                .Select(u =>
                {
                    var texto = string.IsNullOrEmpty(u.Descripcion) ? u.CodigoEstante : $"{u.CodigoEstante} — {u.Descripcion}";
                    return (u.IdUbicacion, sedeId.HasValue ? texto : $"{texto} · {u.Sede}");
                })
                .ToList();
        }

        // GET: Movimiento/ObtenerStock
        [HttpGet]
        public async Task<IActionResult> ObtenerStock(int productoId, int ubicacionId)
        {
            var stock = await _context.ProductoUbicacions
                .FirstOrDefaultAsync(pu => pu.IdProducto == productoId && pu.IdUbicacion == ubicacionId);
            return Json(new { stock = stock?.CantidadActual ?? 0 });
        }

        // GET: Movimiento/Entrada
        public IActionResult Entrada()
        {
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            // Productos
            var productos = _context.Productos.ToList();
            ViewBag.Productos = productos.Select(p => new SelectListItem
            {
                Value = p.IdProducto.ToString(),
                Text = p.Nombre
            }).ToList();

            ViewBag.Ubicaciones = UbicacionesParaUsuario(sedeId)
                .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.Texto })
                .ToList();

            // Proveedores
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
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

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
                var (sedeResuelta, errorSede) = await ResolverSedeMovimiento(model.Detalles.Select(d => d.UbicacionId), sedeId);
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
            var stocks = await CargarStocks(model.Detalles!.Select(d => (d.ProductoId, d.UbicacionId)));
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

            // 4. Guardar evidencia (si existe)
            if (model.Evidencia != null)
                await GuardarEvidencia(movimiento.IdMovimiento, model.Evidencia, "Entrada");

            TempData["Mensaje"] = "Entrada registrada correctamente.";
            return RedirectToAction("Index");
        }
       
        //----------------------------------------------------------------------------------------
        [NonAction]
        private async Task GuardarEvidencia(int movimientoId, IFormFile archivo, string tipoMovimiento)
        {
            if (archivo == null || archivo.Length == 0) return;

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "evidencias");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(archivo.FileName)}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            var evidencia = new Evidencium
            {
                IdMovimiento = movimientoId,
                Tipo = tipoMovimiento,            // "Entrada" o "Salida"
                UrlArchivo = $"/evidencias/{uniqueFileName}",
                Fecha = DateTime.Now,
                IdCamara = null,                  // si la columna ya permite NULL; sino asigna 1
                IdDetalle = null                  // puede ser null
            };
            _context.Evidencia.Add(evidencia);
            await _context.SaveChangesAsync();
        }

        // GET: Movimiento/Index (lista de movimientos)
        public async Task<IActionResult> Index(int? pagina, int tamanoPagina = 10)
        {
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

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
            ViewBag.Productos = new SelectList(_context.Productos, "IdProducto", "Nombre");
            ViewBag.Ubicaciones = UbicacionesParaUsuario(sedeId)
                .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.Texto })
                .ToList();

            ViewBag.Proveedores = new SelectList(_context.Proveedores, "IdProveedor", "Nombre");
            return View("Entrada");
        }
        //---------------------------------------------------------------------------------------
        // GET: Movimiento/Salida (NUEVO - Versión carrito)
        public IActionResult Salida()
        {
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            // Clientes ordenados
            ViewBag.Clientes = _context.Clientes
                .OrderBy(c => c.Nombre)
                .Select(c => new SelectListItem
                {
                    Value = c.IdCliente.ToString(),
                    Text = c.Nombre
                }).ToList();

            // Productos ordenados (para búsqueda)
            ViewBag.Productos = _context.Productos
                .OrderBy(p => p.Nombre)
                .Select(p => new
                {
                    id = p.IdProducto,
                    nombre = p.Nombre,
                    codigo = p.Codigo,
                    precio = p.PrecioUnitario
                }).ToList();

            ViewBag.Ubicaciones = UbicacionesParaUsuario(sedeId)
                .Select(u => new { id = u.Id, codigo = u.Texto })
                .ToList();

            return View(new SalidaCarritoViewModel());
        }
        // POST: Movimiento/CrearClienteRapido (AJAX — lo consume el modal de Salida.cshtml)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearClienteRapido(ClienteRapidoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, errors = errores });
            }

            var cliente = new Cliente
            {
                Nombre = model.Nombre,
                RucDni = model.RucDni,
                Contacto = model.Contacto,
                Direccion = model.Direccion
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return Json(new { success = true, clienteId = cliente.IdCliente, nombre = cliente.Nombre });
        }

        // POST: Movimiento/Salida (NUEVO - Versión carrito)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Salida(SalidaCarritoViewModel model)
        {
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            var redirectSiFalla = ObtenerUsuarioIdOFallar(out int usuarioId);
            if (redirectSiFalla != null)
                return redirectSiFalla;

            // Validaciones
            int? sedeMovimiento = null;
            var productos = new Dictionary<int, (string Nombre, decimal Precio)>();
            var stocks = new Dictionary<(int, int), ProductoUbicacion>();
            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un producto.");
            }
            else
            {
                var (sedeResuelta, errorSede) = await ResolverSedeMovimiento(model.Items.Select(i => i.UbicacionId), sedeId);
                if (errorSede != null)
                    ModelState.AddModelError("", errorSede);
                sedeMovimiento = sedeResuelta;

                // El precio sale de la BD: el del formulario viaja en un input oculto y se puede alterar
                var idsProducto = model.Items.Select(i => i.ProductoId).Distinct().ToList();
                productos = (await _context.Productos
                        .Where(p => idsProducto.Contains(p.IdProducto))
                        .Select(p => new { p.IdProducto, p.Nombre, p.PrecioUnitario })
                        .ToListAsync())
                    .ToDictionary(p => p.IdProducto, p => (p.Nombre, p.PrecioUnitario));
                if (productos.Count != idsProducto.Count)
                    ModelState.AddModelError("", "Uno de los productos del carrito ya no existe.");

                // Se valida la suma por producto+ubicación: dos filas iguales no deben pasar por separado
                stocks = await CargarStocks(model.Items.Select(i => (i.ProductoId, i.UbicacionId)));
                foreach (var pedido in model.Items.GroupBy(i => (i.ProductoId, i.UbicacionId)))
                {
                    var disponible = stocks.TryGetValue(pedido.Key, out var fila) ? fila.CantidadActual : 0;
                    var solicitado = pedido.Sum(i => i.Cantidad);
                    if (solicitado > disponible)
                    {
                        var nombre = productos.TryGetValue(pedido.Key.ProductoId, out var p) ? p.Nombre : "un producto";
                        ModelState.AddModelError("", $"Stock insuficiente para '{nombre}' en la ubicación seleccionada. Disponible: {disponible}, solicitado: {solicitado}.");
                    }
                }
            }

            if (!model.ClienteId.HasValue)
                ModelState.AddModelError("ClienteId", "Debe seleccionar un cliente.");

            if (!ModelState.IsValid)
            {
                RecargarListasSalida(sedeId);
                return View(model);
            }

            // Crear el movimiento con sus detalles y descontar el stock en un solo guardado
            var movimiento = new Movimiento
            {
                Tipo = "Salida",
                Fecha = DateTime.Now,
                IdUsuario = usuarioId,
                IdSede = sedeMovimiento!.Value,
                IdCliente = model.ClienteId,
                ComprobanteEmitido = !string.IsNullOrEmpty(model.NumeroComprobante),
                Observaciones = model.NumeroComprobante
            };
            _context.Movimientos.Add(movimiento);

            decimal total = 0;
            foreach (var item in model.Items!)
            {
                var stock = stocks[(item.ProductoId, item.UbicacionId)];
                var precio = productos[item.ProductoId].Precio;

                movimiento.DetalleMovimientos.Add(new DetalleMovimiento
                {
                    IdProducto = item.ProductoId,
                    IdUbicacion = item.UbicacionId,
                    Cantidad = item.Cantidad,
                    PrecioUnitarioSnapshot = precio,
                    StockAnterior = stock.CantidadActual
                });

                stock.CantidadActual -= item.Cantidad;
                stock.UltimaActualizacion = DateTime.Now;
                total += item.Cantidad * precio;
            }

            await _context.SaveChangesAsync();

            // Evidencia
            if (model.Evidencia != null)
                await GuardarEvidencia(movimiento.IdMovimiento, model.Evidencia, "Salida");

            TempData["Mensaje"] = $"Salida registrada correctamente. Total: S/. {total:N2}";
            return RedirectToAction("Index");
        }

        // Método auxiliar para recargar listas
        private void RecargarListasSalida(int? sedeId)
        {
            ViewBag.Clientes = _context.Clientes
                .OrderBy(c => c.Nombre)
                .Select(c => new SelectListItem { Value = c.IdCliente.ToString(), Text = c.Nombre })
                .ToList();

            ViewBag.Productos = _context.Productos
                .OrderBy(p => p.Nombre)
                .Select(p => new { id = p.IdProducto, nombre = p.Nombre, codigo = p.Codigo, precio = p.PrecioUnitario })
                .ToList();

            ViewBag.Ubicaciones = UbicacionesParaUsuario(sedeId)
                .Select(u => new { id = u.Id, codigo = u.Texto })
                .ToList();
        }
        //--------------------detalle--------------------------------------
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
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
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