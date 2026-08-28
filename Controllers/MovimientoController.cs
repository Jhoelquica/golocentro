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

            // Ubicaciones filtradas por sede
            var ubicacionesQuery = _context.Ubicaciones.AsQueryable();
            if (sedeId.HasValue)
                ubicacionesQuery = ubicacionesQuery.Where(u => u.IdSede == sedeId.Value);
            var ubicaciones = ubicacionesQuery.ToList();
            ViewBag.Ubicaciones = ubicaciones.Select(u => new SelectListItem
            {
                Value = u.IdUbicacion.ToString(),
                Text = u.CodigoEstante
            }).ToList();

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

            // 1. Validar que haya al menos un detalle
            if (model.Detalles == null || !model.Detalles.Any())
                ModelState.AddModelError("", "Debe agregar al menos un producto.");

            if (!ModelState.IsValid)
            {
                CargarListasParaVista(sedeId);
                return View(model);
            }

            // 2. Crear el movimiento
            var movimiento = new Movimiento
            {
                Tipo = "Entrada",
                Fecha = DateTime.Now,
                IdUsuario = usuarioId,
                IdSede = sedeId,
                IdProveedor = model.ProveedorId,
                ComprobanteEmitido = !string.IsNullOrEmpty(model.NumeroFactura),
                Observaciones = model.NumeroFactura
            };
            _context.Movimientos.Add(movimiento);
            await _context.SaveChangesAsync();

            // 3. Procesar cada detalle
            foreach (var detalleVM in model.Detalles)
            {
                // Validar que la ubicación pertenezca a la sede (si el usuario no es dueña)
                if (sedeId.HasValue)
                {
                    var ubicacion = await _context.Ubicaciones.FindAsync(detalleVM.UbicacionId);
                    if (ubicacion == null || ubicacion.IdSede != sedeId.Value)
                    {
                        ModelState.AddModelError("", $"La ubicación seleccionada en el producto {detalleVM.ProductoId} no pertenece a su sede.");
                        // Eliminar el movimiento recién creado porque no se completará
                        _context.Movimientos.Remove(movimiento);
                        await _context.SaveChangesAsync();
                        CargarListasParaVista(sedeId);
                        return View(model);
                    }
                }

                var detalle = new DetalleMovimiento
                {
                    IdMovimiento = movimiento.IdMovimiento,
                    IdProducto = detalleVM.ProductoId,
                    Cantidad = detalleVM.Cantidad,
                    IdUbicacion = detalleVM.UbicacionId
                };
                _context.DetalleMovimientos.Add(detalle);

                // Actualizar stock en ProductoUbicacion
                var stockActual = await _context.ProductoUbicacions
                    .FirstOrDefaultAsync(pu => pu.IdProducto == detalleVM.ProductoId && pu.IdUbicacion == detalleVM.UbicacionId);

                if (stockActual != null)
                    stockActual.CantidadActual += detalleVM.Cantidad;
                else
                    _context.ProductoUbicacions.Add(new ProductoUbicacion
                    {
                        IdProducto = detalleVM.ProductoId,
                        IdUbicacion = detalleVM.UbicacionId,
                        CantidadActual = detalleVM.Cantidad,
                        UltimaActualizacion = DateTime.Now
                    });
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
            var ubicacionesQuery = _context.Ubicaciones.AsQueryable();
            if (sedeId.HasValue)
                ubicacionesQuery = ubicacionesQuery.Where(u => u.IdSede == sedeId.Value);

            // SelectList no soporta texto combinado directamente, así que proyectamos
            // a un objeto con "Texto" calculado: "CodigoEstante — Descripcion" si hay
            // descripción, o solo "CodigoEstante" si es null/vacía.
            var ubicacionesParaLista = ubicacionesQuery
                .ToList()
                .Select(u => new
                {
                    u.IdUbicacion,
                    Texto = string.IsNullOrEmpty(u.Descripcion)
                        ? u.CodigoEstante
                        : $"{u.CodigoEstante} — {u.Descripcion}"
                });
            ViewBag.Ubicaciones = new SelectList(ubicacionesParaLista, "IdUbicacion", "Texto");

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

            // Ubicaciones filtradas por sede
            var ubicacionesQuery = _context.Ubicaciones.AsQueryable();
            if (sedeId.HasValue)
                ubicacionesQuery = ubicacionesQuery.Where(u => u.IdSede == sedeId.Value);

            ViewBag.Ubicaciones = ubicacionesQuery
                .OrderBy(u => u.CodigoEstante)
                .Select(u => new
                {
                    id = u.IdUbicacion,
                    codigo = u.CodigoEstante
                }).ToList();

            return View(new SalidaCarritoViewModel());
        }
        // POST: Movimiento/CrearClienteRapido (SIN AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearClienteRapido(ClienteRapidoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Datos del cliente inválidos.";
                return RedirectToAction("Salida");
            }

            var cliente = new Cliente
            {
                Nombre = model.Nombre,
                RucDni = model.RucDni ?? "S/D",
                Contacto = model.Contacto,
                Direccion = model.Direccion
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Cliente '{cliente.Nombre}' registrado correctamente.";
            return RedirectToAction("Salida");
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
            if (model.Items == null || !model.Items.Any())
                ModelState.AddModelError("", "Debe agregar al menos un producto.");

            if (!model.ClienteId.HasValue)
                ModelState.AddModelError("ClienteId", "Debe seleccionar un cliente.");

            // Validar stock para cada item
            if (model.Items != null)
            {
                foreach (var item in model.Items)
                {
                    var stock = await _context.ProductoUbicacions
                        .FirstOrDefaultAsync(pu => pu.IdProducto == item.ProductoId && pu.IdUbicacion == item.UbicacionId);

                    if (stock == null || stock.CantidadActual < item.Cantidad)
                    {
                        var producto = await _context.Productos.FindAsync(item.ProductoId);
                        ModelState.AddModelError("", $"Stock insuficiente para '{producto?.Nombre}' en la ubicación seleccionada. Disponible: {stock?.CantidadActual ?? 0}");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                RecargarListasSalida(sedeId);
                return View(model);
            }

            // Crear movimiento
            var movimiento = new Movimiento
            {
                Tipo = "Salida",
                Fecha = DateTime.Now,
                IdUsuario = usuarioId,
                IdSede = sedeId,
                IdCliente = model.ClienteId,
                ComprobanteEmitido = !string.IsNullOrEmpty(model.NumeroComprobante),
                Observaciones = model.NumeroComprobante
            };
            _context.Movimientos.Add(movimiento);
            await _context.SaveChangesAsync();

            // Procesar cada item
            decimal total = 0;
            foreach (var item in model.Items)
            {
                var detalle = new DetalleMovimiento
                {
                    IdMovimiento = movimiento.IdMovimiento,
                    IdProducto = item.ProductoId,
                    Cantidad = item.Cantidad,
                    IdUbicacion = item.UbicacionId
                };
                _context.DetalleMovimientos.Add(detalle);

                // Restar stock
                var stock = await _context.ProductoUbicacions
                    .FirstOrDefaultAsync(pu => pu.IdProducto == item.ProductoId && pu.IdUbicacion == item.UbicacionId);
                if (stock != null)
                    stock.CantidadActual -= item.Cantidad;

                total += item.Cantidad * item.PrecioUnitario;
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

            var ubicacionesQuery = _context.Ubicaciones.AsQueryable();
            if (sedeId.HasValue)
                ubicacionesQuery = ubicacionesQuery.Where(u => u.IdSede == sedeId.Value);

            ViewBag.Ubicaciones = ubicacionesQuery
                .OrderBy(u => u.CodigoEstante)
                .Select(u => new { id = u.IdUbicacion, codigo = u.CodigoEstante })
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