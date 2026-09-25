using System.Text.RegularExpressions;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.Services;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    // Ventas con nota de venta (antes "Salida"). La nota es interna del negocio: se imprime
    // en ticket de 80 mm o se envía por WhatsApp.
    [Authorize]
    public class VentaController : Controller
    {
        private const string Serie = "NV01";
        private readonly AppDbContext _context;

        public VentaController(AppDbContext context)
        {
            _context = context;
        }

        public static string NumeroNota(string serie, int numero) => $"{serie}-{numero:000000}";

        public async Task<IActionResult> Index(DateOnly? desde, DateOnly? hasta, string? q)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var modelo = new VentasIndexViewModel { Desde = desde ?? hoy, Hasta = hasta ?? desde ?? hoy, Busqueda = q };
            if (modelo.Hasta < modelo.Desde)
                (modelo.Desde, modelo.Hasta) = (modelo.Hasta, modelo.Desde);

            var inicio = modelo.Desde.ToDateTime(TimeOnly.MinValue);
            var fin = modelo.Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var sedeId = SedeDelUsuario();

            var query = _context.NotaVenta
                .Where(n => n.IdMovimientoNavigation.Fecha >= inicio && n.IdMovimientoNavigation.Fecha < fin)
                .Where(n => sedeId == null || n.IdMovimientoNavigation.IdSede == sedeId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var texto = q.Trim();
                // "123", "000123" o "NV01-000123" buscan por número (las cifras del final); todo, por cliente
                var cifras = Regex.Match(texto, @"(\d{1,9})$");
                var numero = cifras.Success ? int.Parse(cifras.Value) : (int?)null;
                var patron = $"%{texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(n => n.Numero == numero || EF.Functions.ILike(n.IdMovimientoNavigation.IdClienteNavigation!.Nombre, patron));
            }

            var filas = await query
                .OrderByDescending(n => n.IdMovimientoNavigation.Fecha)
                .Select(n => new
                {
                    n.IdMovimiento,
                    n.Serie,
                    n.Numero,
                    n.IdMovimientoNavigation.Fecha,
                    Cliente = n.IdMovimientoNavigation.IdClienteNavigation!.Nombre,
                    n.Total,
                    n.MetodoPago,
                    Vendedor = n.IdMovimientoNavigation.IdUsuarioNavigation.Nombre
                })
                .ToListAsync();

            modelo.Filas = filas
                .Select(f => new VentaFila(f.IdMovimiento, NumeroNota(f.Serie, f.Numero), f.Fecha, f.Cliente, f.Total, MetodosPago.Texto(f.MetodoPago), f.Vendedor))
                .ToList();
            modelo.Total = filas.Sum(f => f.Total);
            modelo.PorMetodo = filas
                .GroupBy(f => f.MetodoPago)
                .Select(g => new TotalPorMetodo(MetodosPago.Texto(g.Key), g.Count(), g.Sum(f => f.Total)))
                .OrderByDescending(t => t.Total)
                .ToList();

            return View(modelo);
        }

        public async Task<IActionResult> Nueva()
        {
            var model = new VentaFormViewModel { MetodoPago = "efectivo" };
            await LlenarDatos(model);
            model.ClienteId = model.Datos.ClienteGeneralId;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nueva(VentaFormViewModel model)
        {
            var usuarioIdClaim = User.FindFirst("UsuarioId")?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim))
                return RedirectToAction("Login", "Account");

            var items = model.Items.Where(i => i.ProductoId > 0).ToList();
            int? sedeVenta = null;
            var productos = new Dictionary<int, (string Nombre, decimal Precio)>();
            var stocks = new Dictionary<(int, int), ProductoUbicacion>();

            if (items.Count == 0)
            {
                ModelState.AddModelError("", "Agrega al menos un producto a la venta.");
            }
            else
            {
                var idsProducto = items.Select(i => i.ProductoId).Distinct().ToList();
                productos = (await _context.Productos
                        .Where(p => idsProducto.Contains(p.IdProducto))
                        .Select(p => new { p.IdProducto, p.Nombre, p.PrecioUnitario })
                        .ToListAsync())
                    .ToDictionary(p => p.IdProducto, p => (p.Nombre, p.PrecioUnitario));
                if (productos.Count != idsProducto.Count)
                    ModelState.AddModelError("", "Uno de los productos de la venta ya no existe.");

                foreach (var item in items)
                {
                    var nombre = productos.TryGetValue(item.ProductoId, out var p) ? p.Nombre : "un producto";
                    if (item.Cantidad < 1)
                        ModelState.AddModelError("", $"La cantidad de {nombre} debe ser al menos 1.");
                    if (item.Precio is decimal precio && (precio < 0 || decimal.Round(precio, 2) != precio))
                        ModelState.AddModelError("", $"El precio de {nombre} debe ser 0 o más, con máximo 2 decimales.");
                }

                var (sedeResuelta, errorSede) = await OperacionesAlmacen.ResolverSedeMovimiento(_context, items.Select(i => i.UbicacionId), SedeDelUsuario());
                if (errorSede != null)
                    ModelState.AddModelError("", errorSede);
                sedeVenta = sedeResuelta;

                // Se valida la suma por producto+zona: dos filas iguales no deben pasar por separado
                stocks = await OperacionesAlmacen.CargarStocks(_context, items.Select(i => (i.ProductoId, i.UbicacionId)));
                foreach (var pedido in items.GroupBy(i => (i.ProductoId, i.UbicacionId)))
                {
                    var disponible = stocks.TryGetValue(pedido.Key, out var fila) ? fila.CantidadActual : 0;
                    var solicitado = pedido.Sum(i => i.Cantidad);
                    if (solicitado > disponible)
                    {
                        var nombre = productos.TryGetValue(pedido.Key.ProductoId, out var p) ? p.Nombre : "un producto";
                        ModelState.AddModelError("", $"No alcanza el stock de {nombre} en esa zona: hay {disponible}, pediste {solicitado}.");
                    }
                }
            }

            if (model.ClienteId == null || !await _context.Clientes.AnyAsync(c => c.IdCliente == model.ClienteId))
                ModelState.AddModelError(nameof(model.ClienteId), "Elige el cliente.");
            if (!MetodosPago.EsValido(model.MetodoPago))
                ModelState.AddModelError(nameof(model.MetodoPago), "Elige el método de pago.");
            if (OperacionesAlmacen.ValidarEvidencia(model.Evidencia) is string errorFoto)
                ModelState.AddModelError(nameof(model.Evidencia), errorFoto);

            var lineas = items
                .Where(i => productos.ContainsKey(i.ProductoId))
                .Select(i => (Item: i, Precio: i.Precio ?? productos[i.ProductoId].Precio))
                .ToList();
            var subtotal = lineas.Sum(l => decimal.Round(l.Item.Cantidad * l.Precio, 2));
            var descuento = model.Descuento ?? 0;
            if (descuento < 0 || decimal.Round(descuento, 2) != descuento)
                ModelState.AddModelError(nameof(model.Descuento), "El descuento debe ser 0 o más, con máximo 2 decimales.");
            else if (descuento > subtotal)
                ModelState.AddModelError(nameof(model.Descuento), "El descuento no puede ser mayor que el subtotal.");

            if (!ModelState.IsValid)
            {
                await LlenarDatos(model);
                return View(model);
            }

            var ahora = DateTime.Now;
            var movimiento = new Movimiento
            {
                Tipo = "Salida",
                Fecha = ahora,
                IdUsuario = int.Parse(usuarioIdClaim),
                IdSede = sedeVenta!.Value,
                IdCliente = model.ClienteId,
                Observaciones = string.IsNullOrWhiteSpace(model.Observaciones) ? null : model.Observaciones.Trim()
            };

            foreach (var (item, precio) in lineas)
            {
                var stock = stocks[(item.ProductoId, item.UbicacionId)];
                movimiento.DetalleMovimientos.Add(new DetalleMovimiento
                {
                    IdProducto = item.ProductoId,
                    IdUbicacion = item.UbicacionId,
                    Cantidad = item.Cantidad,
                    PrecioUnitarioSnapshot = precio,
                    StockAnterior = stock.CantidadActual
                });
                stock.CantidadActual -= item.Cantidad;
                stock.UltimaActualizacion = ahora;
            }

            var nota = new NotaVentum
            {
                Serie = Serie,
                Subtotal = subtotal,
                Descuento = descuento,
                Total = subtotal - descuento,
                MetodoPago = model.MetodoPago!
            };
            movimiento.NotaVentum = nota;
            _context.Movimientos.Add(movimiento);

            // Dos ventas al mismo tiempo pueden tomar el mismo número: uq_notaventa_numero lo impide y se reintenta
            for (var intento = 1; ; intento++)
            {
                nota.Numero = (await _context.NotaVenta.Where(n => n.Serie == Serie).MaxAsync(n => (int?)n.Numero) ?? 0) + 1;
                try
                {
                    await _context.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException ex) when (intento < 3 && ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
                {
                }
                catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23514" })
                {
                    // chk_productoubicacion_cantidad: otra venta se llevó el stock mientras se armaba esta
                    ModelState.AddModelError("", "El stock cambió mientras registrabas la venta. Revisa las cantidades y vuelve a intentarlo.");
                    _context.ChangeTracker.Clear();
                    await LlenarDatos(model);
                    return View(model);
                }
            }

            if (model.Evidencia is { Length: > 0 })
                await OperacionesAlmacen.GuardarEvidencia(_context, movimiento.IdMovimiento, model.Evidencia, "Salida");

            TempData["Exito"] = $"Venta registrada: {NumeroNota(nota.Serie, nota.Numero)} por S/ {nota.Total:0.00}.";
            return RedirectToAction(nameof(Nota), new { id = movimiento.IdMovimiento });
        }

        // id = IdMovimiento de la venta
        public async Task<IActionResult> Nota(int id)
        {
            var venta = await _context.Movimientos
                .Where(m => m.IdMovimiento == id && m.Tipo == "Salida")
                .Select(m => new
                {
                    m.IdMovimiento,
                    m.IdSede,
                    m.Fecha,
                    m.Observaciones,
                    Nota = m.NotaVentum,
                    Vendedor = m.IdUsuarioNavigation.Nombre,
                    Sede = m.IdSedeNavigation,
                    Cliente = m.IdClienteNavigation,
                    Lineas = m.DetalleMovimientos
                        .OrderBy(d => d.IdDetalle)
                        .Select(d => new { d.Cantidad, d.IdProductoNavigation.Nombre, d.IdProductoNavigation.UnidadMedida, d.PrecioUnitarioSnapshot })
                        .ToList(),
                    Evidencias = m.Evidencia.OrderBy(e => e.Fecha).Select(e => new EvidenciaNota(e.UrlArchivo, e.Fecha)).ToList()
                })
                .FirstOrDefaultAsync();

            if (venta == null)
                return NotFound();
            if (SedeDelUsuario() is int sede && venta.IdSede != sede)
                return RedirectToAction("AccessDenied", "Account");
            // Salidas registradas antes de las notas de venta: se ven en el detalle de movimiento
            if (venta.Nota == null)
                return RedirectToAction("Detalle", "Movimiento", new { id });

            return View(new NotaVentaViewModel
            {
                IdMovimiento = venta.IdMovimiento,
                Numero = NumeroNota(venta.Nota.Serie, venta.Nota.Numero),
                Fecha = venta.Fecha,
                Vendedor = venta.Vendedor,
                SedeNombre = venta.Sede.Nombre,
                SedeDireccion = venta.Sede.Direccion,
                SedeCiudad = venta.Sede.Ciudad,
                EsPublicoGeneral = venta.Cliente?.RucDni == ClienteGeneral.RucDni,
                ClienteNombre = venta.Cliente?.Nombre ?? "Público en general",
                ClienteDocumento = venta.Cliente?.RucDni,
                ClienteCelular = venta.Cliente?.Celular,
                Lineas = venta.Lineas
                    .Select(l => new LineaNota(l.Cantidad, l.Nombre, l.UnidadMedida, l.PrecioUnitarioSnapshot, decimal.Round(l.Cantidad * l.PrecioUnitarioSnapshot, 2)))
                    .ToList(),
                Subtotal = venta.Nota.Subtotal,
                Descuento = venta.Nota.Descuento,
                Total = venta.Nota.Total,
                MetodoPago = MetodosPago.Texto(venta.Nota.MetodoPago),
                Observaciones = venta.Observaciones,
                Evidencias = venta.Evidencias
            });
        }

        // La foto de entrega suele tomarse después de la venta; queda en el sistema, no en la nota
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarEvidencia(int id, IFormFile? foto)
        {
            var venta = await _context.Movimientos
                .Where(m => m.IdMovimiento == id && m.Tipo == "Salida")
                .Select(m => new { m.IdSede })
                .FirstOrDefaultAsync();
            if (venta == null)
                return NotFound();
            if (SedeDelUsuario() is int sede && venta.IdSede != sede)
                return RedirectToAction("AccessDenied", "Account");

            if (foto is not { Length: > 0 })
                TempData["Error"] = "Elige una foto para adjuntar.";
            else if (OperacionesAlmacen.ValidarEvidencia(foto) is string error)
                TempData["Error"] = error;
            else
            {
                await OperacionesAlmacen.GuardarEvidencia(_context, id, foto, "Salida");
                TempData["Exito"] = "Foto de entrega guardada.";
            }
            return RedirectToAction(nameof(Nota), new { id });
        }

        // Alta rápida desde la venta (modal); responde JSON para no perder el carrito
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearClienteRapido(ClienteRapidoViewModel model)
        {
            model.Nombre = model.Nombre?.Trim();
            model.RucDni = model.RucDni?.Trim();
            model.Celular = string.IsNullOrWhiteSpace(model.Celular) ? null : model.Celular.Trim();

            if (ModelState.IsValid)
            {
                var existente = await _context.Clientes.Where(c => c.RucDni == model.RucDni).Select(c => c.Nombre).FirstOrDefaultAsync();
                if (existente != null)
                    ModelState.AddModelError(nameof(model.RucDni), $"Ya existe un cliente con ese DNI o RUC: {existente}. Búscalo en la lista.");
            }

            if (!ModelState.IsValid)
            {
                var errores = ModelState
                    .Where(kv => kv.Value!.Errors.Count > 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors[0].ErrorMessage);
                return Json(new { success = false, errores });
            }

            var cliente = new Cliente { Nombre = model.Nombre!, RucDni = model.RucDni!, Celular = model.Celular };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            return Json(new { success = true, cliente = new ClienteVenta(cliente.IdCliente, cliente.Nombre, cliente.RucDni, cliente.Celular) });
        }

        [NonAction]
        private int? SedeDelUsuario()
        {
            var claim = User.FindFirst("SedeId")?.Value;
            return string.IsNullOrEmpty(claim) ? null : int.Parse(claim);
        }

        [NonAction]
        private async Task LlenarDatos(VentaFormViewModel model)
        {
            var sedeId = SedeDelUsuario();

            // Solo productos con stock en la sede; cada uno con sus zonas, la de más cantidad primero
            var stock = await _context.ProductoUbicacions
                .Where(pu => pu.CantidadActual > 0 && (sedeId == null || pu.IdUbicacionNavigation.IdSede == sedeId))
                .Select(pu => new
                {
                    pu.IdProducto,
                    pu.IdProductoNavigation.Nombre,
                    pu.IdProductoNavigation.Codigo,
                    pu.IdProductoNavigation.UnidadMedida,
                    pu.IdProductoNavigation.PrecioUnitario,
                    pu.IdUbicacion,
                    pu.IdUbicacionNavigation.CodigoEstante,
                    pu.CantidadActual
                })
                .ToListAsync();

            model.Datos.Productos = stock
                .GroupBy(s => s.IdProducto)
                .Select(g => new ProductoVenta(
                    g.Key, g.First().Nombre, g.First().Codigo, g.First().UnidadMedida, g.First().PrecioUnitario,
                    g.OrderByDescending(s => s.CantidadActual).Select(s => new ZonaVenta(s.IdUbicacion, s.CodigoEstante, s.CantidadActual)).ToList()))
                .OrderBy(p => p.Nombre)
                .ToList();

            model.Datos.Clientes = await _context.Clientes
                .OrderBy(c => c.RucDni == ClienteGeneral.RucDni ? 0 : 1)
                .ThenBy(c => c.Nombre)
                .Select(c => new ClienteVenta(c.IdCliente, c.Nombre, c.RucDni, c.Celular))
                .ToListAsync();

            model.Datos.ClienteGeneralId = model.Datos.Clientes.FirstOrDefault(c => c.RucDni == ClienteGeneral.RucDni)?.Id ?? 0;
        }
    }
}
