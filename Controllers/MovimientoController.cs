using System.Globalization;
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
    public class MovimientoController : Controller
    {
        private const int TamanoPagina = 25;
        private const int MaxFactura = 50;
        private const int MaxLote = 50;
        public const int MaxCantidadEntrada = 1_000_000;
        private const int MaxDiasLista = 92;
        private readonly AppDbContext _context;

        public MovimientoController(AppDbContext context)
        {
            _context = context;
        }

        // ===== Lista: toda la actividad de stock (entradas, ventas, traslados y conteos) =====
        public async Task<IActionResult> Index(string? tipo, DateOnly? desde, DateOnly? hasta, int pagina = 1)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var modelo = new MovimientosIndexViewModel
            {
                Tipo = tipo is TipoActividad.Entrada or TipoActividad.Venta or TipoActividad.Traslado or TipoActividad.Conteo ? tipo : "todos",
                Desde = desde ?? hoy.AddDays(-6),
                Hasta = hasta ?? hoy
            };
            if (modelo.Hasta < modelo.Desde)
                (modelo.Desde, modelo.Hasta) = (modelo.Hasta, modelo.Desde);
            // La lista se arma en memoria: un rango de años saturaría un servidor chico. Para más, están los reportes.
            if (modelo.Hasta.DayNumber - modelo.Desde.DayNumber > MaxDiasLista)
            {
                modelo.Desde = modelo.Hasta.AddDays(-MaxDiasLista);
                modelo.RangoRecortado = true;
            }

            var inicio = modelo.Desde.ToDateTime(TimeOnly.MinValue);
            var fin = modelo.Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var sedeId = User.SedeId();
            modelo.VariasSedes = sedeId == null && await _context.Sedes.CountAsync() > 1;

            var filas = new List<ActividadFila>();

            var movimientos = await _context.Movimientos
                .Where(m => m.Fecha >= inicio && m.Fecha < fin && (sedeId == null || m.IdSede == sedeId))
                .Select(m => new
                {
                    m.IdMovimiento,
                    m.Tipo,
                    m.Fecha,
                    m.Observaciones,
                    Usuario = m.IdUsuarioNavigation.Nombre,
                    Sede = m.IdSedeNavigation.Nombre,
                    Proveedor = m.IdProveedorNavigation != null ? m.IdProveedorNavigation.Nombre : null,
                    Cliente = m.IdClienteNavigation != null ? m.IdClienteNavigation.Nombre : null,
                    Serie = m.NotaVentum != null ? m.NotaVentum.Serie : null,
                    Numero = m.NotaVentum != null ? (int?)m.NotaVentum.Numero : null,
                    Total = m.NotaVentum != null ? (decimal?)m.NotaVentum.Total : null,
                    Anulada = m.NotaVentum != null && m.NotaVentum.Estado == EstadoNota.Anulada,
                    Productos = m.DetalleMovimientos.Select(d => d.IdProducto).Distinct().Count(),
                    Unidades = m.DetalleMovimientos.Sum(d => d.Cantidad),
                    Fotos = m.Evidencia.Count
                })
                .ToListAsync();

            foreach (var m in movimientos)
            {
                var resumen = $"{m.Productos} {(m.Productos == 1 ? "producto" : "productos")} · {m.Unidades} {(m.Unidades == 1 ? "unidad" : "unidades")}";
                if (m.Tipo == "Entrada")
                    filas.Add(new ActividadFila(m.Fecha, TipoActividad.Entrada, "Entrada", m.Proveedor ?? "Sin proveedor",
                        string.IsNullOrWhiteSpace(m.Observaciones) ? null : m.Observaciones, resumen, m.Usuario, m.Sede, null, false, m.Fotos,
                        Url.Action(nameof(Detalle), new { id = m.IdMovimiento })));
                else if (m.Serie != null)
                    filas.Add(new ActividadFila(m.Fecha, TipoActividad.Venta, m.Anulada ? "Venta anulada" : "Venta", m.Cliente ?? "Público en general",
                        VentaController.NumeroNota(m.Serie, m.Numero!.Value), resumen, m.Usuario, m.Sede, m.Total, m.Anulada, m.Fotos,
                        Url.Action("Nota", "Venta", new { id = m.IdMovimiento })));
                else
                    filas.Add(new ActividadFila(m.Fecha, TipoActividad.Salida, "Salida", m.Cliente ?? "Sin cliente", null, resumen,
                        m.Usuario, m.Sede, null, false, m.Fotos, Url.Action(nameof(Detalle), new { id = m.IdMovimiento })));
            }

            // Traslados entre zonas (Mover mercadería)
            var traslados = await _context.Traslados
                .Where(t => t.Fecha >= inicio && t.Fecha < fin && (sedeId == null || t.IdSede == sedeId))
                .Select(t => new
                {
                    t.Fecha,
                    t.Observaciones,
                    Usuario = t.IdUsuarioNavigation.Nombre,
                    Sede = t.IdSedeNavigation.Nombre,
                    Detalles = t.DetalleTraslados.Select(d => new
                    {
                        d.IdProductoNavigation.Nombre,
                        d.Cantidad,
                        Origen = d.IdUbicacionOrigenNavigation.CodigoEstante,
                        Destino = d.IdUbicacionDestinoNavigation.CodigoEstante
                    }).ToList()
                })
                .ToListAsync();
            foreach (var t in traslados)
            {
                var primero = t.Detalles.FirstOrDefault();
                var titulo = primero == null ? "Traslado" : t.Detalles.Count == 1 ? primero.Nombre : $"{primero.Nombre} y {t.Detalles.Count - 1} más";
                var zonas = string.Join(", ", t.Detalles.Select(d => $"{d.Origen} → {d.Destino}").Distinct());
                var unidades = t.Detalles.Sum(d => d.Cantidad);
                filas.Add(new ActividadFila(t.Fecha, TipoActividad.Traslado, "Traslado", titulo, zonas,
                    $"{unidades} {(unidades == 1 ? "unidad" : "unidades")}" + (string.IsNullOrWhiteSpace(t.Observaciones) ? "" : $" · {t.Observaciones}"),
                    t.Usuario, t.Sede, null, false, 0, null));
            }

            // Conteos por zona
            var conteos = await _context.AjusteInventarios
                .Where(a => a.Fecha >= inicio && a.Fecha < fin && (sedeId == null || a.IdSede == sedeId))
                .Select(a => new
                {
                    a.Fecha,
                    a.Motivo,
                    Usuario = a.IdUsuarioNavigation.Nombre,
                    Sede = a.IdSedeNavigation.Nombre,
                    Zonas = a.DetalleAjustes.Select(d => d.IdUbicacionNavigation.CodigoEstante).Distinct().ToList(),
                    Contados = a.DetalleAjustes.Count,
                    Cambios = a.DetalleAjustes.Count(d => d.CantidadNueva != d.CantidadAnterior)
                })
                .ToListAsync();
            foreach (var c in conteos)
            {
                var titulo = c.Zonas.Count == 0 ? "Conteo" : $"Zona {string.Join(", ", c.Zonas)}";
                filas.Add(new ActividadFila(c.Fecha, TipoActividad.Conteo, c.Motivo == "conteo_inicial" ? "Conteo inicial" : "Conteo", titulo, null,
                    $"{c.Contados} {(c.Contados == 1 ? "producto contado" : "productos contados")} · " +
                    (c.Cambios == 0 ? "todo cuadró" : $"{c.Cambios} con diferencia"),
                    c.Usuario, c.Sede, null, false, 0, Url.Action("Index", "Inventario")));
            }

            modelo.PorTipo = new Dictionary<string, int>
            {
                ["todos"] = filas.Count,
                [TipoActividad.Entrada] = filas.Count(f => f.Tipo == TipoActividad.Entrada),
                [TipoActividad.Venta] = filas.Count(f => f.Tipo is TipoActividad.Venta or TipoActividad.Salida),
                [TipoActividad.Traslado] = filas.Count(f => f.Tipo == TipoActividad.Traslado),
                [TipoActividad.Conteo] = filas.Count(f => f.Tipo == TipoActividad.Conteo)
            };

            var elegidas = modelo.Tipo switch
            {
                "todos" => filas,
                TipoActividad.Venta => filas.Where(f => f.Tipo is TipoActividad.Venta or TipoActividad.Salida).ToList(),
                _ => filas.Where(f => f.Tipo == modelo.Tipo).ToList()
            };
            modelo.Total = elegidas.Count;
            modelo.TotalPaginas = Math.Max(1, (int)Math.Ceiling(elegidas.Count / (double)TamanoPagina));
            modelo.Pagina = Math.Clamp(pagina, 1, modelo.TotalPaginas);
            modelo.Filas = elegidas
                .OrderByDescending(f => f.Fecha)
                .Skip((modelo.Pagina - 1) * TamanoPagina)
                .Take(TamanoPagina)
                .ToList();

            return View(modelo);
        }

        // ===== Registrar entrada =====
        public async Task<IActionResult> Entrada()
        {
            var model = new MovimientoEntradaViewModel();
            await CargarDatosEntrada(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Entrada(MovimientoEntradaViewModel model)
        {
            if (!int.TryParse(User.FindFirst("UsuarioId")?.Value, out var usuarioId))
                return RedirectToAction("Login", "Account");

            var sedeUsuario = User.SedeId();
            var detalles = model.Detalles.Where(d => d.ProductoId > 0).ToList();
            model.NumeroFactura = string.IsNullOrWhiteSpace(model.NumeroFactura) ? null : model.NumeroFactura.Trim();
            foreach (var d in detalles)
            {
                d.Lote = string.IsNullOrWhiteSpace(d.Lote) ? null : d.Lote.Trim();
                d.Presentacion = string.IsNullOrWhiteSpace(d.Presentacion) ? null : d.Presentacion.Trim();
            }

            int? sedeMovimiento = null;
            var precios = new Dictionary<int, decimal>();
            var presentaciones = new Dictionary<(int, string), ProductoPresentacion>();
            // Cada línea llega en la unidad base (factor 1) o en una presentación del producto (10 cajas de 12 packs)
            ProductoPresentacion? PresentacionDe(DetalleEntradaViewModel d) =>
                d.Presentacion != null && presentaciones.TryGetValue((d.ProductoId, d.Presentacion.ToLowerInvariant()), out var pp) ? pp : null;
            int FactorDe(DetalleEntradaViewModel d) => PresentacionDe(d)?.Factor ?? 1;
            if (detalles.Count == 0)
            {
                ModelState.AddModelError("", "Agrega al menos un producto que llegó.");
            }
            else
            {
                var (sedeResuelta, errorSede) = await OperacionesAlmacen.ResolverSedeMovimiento(_context, detalles.Select(d => d.UbicacionId), sedeUsuario);
                if (errorSede != null)
                    ModelState.AddModelError("", errorSede);
                sedeMovimiento = sedeResuelta;

                var idsProducto = detalles.Select(d => d.ProductoId).Distinct().ToList();
                precios = await _context.Productos
                    .Where(p => idsProducto.Contains(p.IdProducto))
                    .ToDictionaryAsync(p => p.IdProducto, p => p.PrecioUnitario);
                if (precios.Count != idsProducto.Count)
                    ModelState.AddModelError("", "Uno de los productos ya no existe. Quítalo y vuelve a agregarlo.");
                presentaciones = (await _context.ProductoPresentacions.Where(pp => idsProducto.Contains(pp.IdProducto)).ToListAsync())
                    .GroupBy(pp => (pp.IdProducto, pp.Nombre.ToLowerInvariant()))
                    .ToDictionary(g => g.Key, g => g.First());
                if (detalles.Any(d => d.Presentacion != null && PresentacionDe(d) == null))
                    ModelState.AddModelError("", "Una de las presentaciones elegidas ya no existe. Elige otra.");
                if (detalles.Any(d => d.Cantidad < 1))
                    ModelState.AddModelError("", "Cada producto debe tener una cantidad de 1 o más.");
                else if (detalles.Any(d => (long)d.Cantidad * FactorDe(d) > MaxCantidadEntrada))
                    ModelState.AddModelError("", $"La cantidad de un producto no puede pasar de {MaxCantidadEntrada:N0} unidades por entrada.");
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                if (detalles.Any(d => d.FechaVencimiento < hoy))
                    ModelState.AddModelError("", "La fecha de vencimiento de un producto ya pasó. Revísala o déjala vacía.");
                if (detalles.Any(d => d.Lote?.Length > MaxLote))
                    ModelState.AddModelError("", $"El lote no puede pasar de {MaxLote} caracteres.");
            }

            if (model.ProveedorId == null || !await _context.Proveedores.AnyAsync(p => p.IdProveedor == model.ProveedorId))
                ModelState.AddModelError(nameof(model.ProveedorId), "Elige el proveedor.");
            if (model.NumeroFactura?.Length > MaxFactura)
                ModelState.AddModelError(nameof(model.NumeroFactura), $"La factura o guía no puede pasar de {MaxFactura} caracteres.");
            if (OperacionesAlmacen.ValidarEvidencia(model.Evidencia) is string errorFoto)
                ModelState.AddModelError(nameof(model.Evidencia), errorFoto);

            if (!ModelState.IsValid)
            {
                await CargarDatosEntrada(model);
                return View(model);
            }

            var ahora = DateTime.Now;
            var movimiento = new Movimiento
            {
                Tipo = "Entrada",
                Fecha = ahora,
                IdUsuario = usuarioId,
                IdSede = sedeMovimiento!.Value,
                IdProveedor = model.ProveedorId,
                ComprobanteEmitido = model.NumeroFactura != null,
                Observaciones = model.NumeroFactura
            };
            _context.Movimientos.Add(movimiento);

            // El diccionario evita duplicar la fila de stock si el mismo producto+zona viene dos veces
            var stocks = await OperacionesAlmacen.CargarStocks(_context, detalles.Select(d => (d.ProductoId, d.UbicacionId)));
            foreach (var d in detalles)
            {
                var clave = (d.ProductoId, d.UbicacionId);
                if (!stocks.TryGetValue(clave, out var stock))
                {
                    stock = new ProductoUbicacion { IdProducto = d.ProductoId, IdUbicacion = d.UbicacionId };
                    _context.ProductoUbicacions.Add(stock);
                    stocks[clave] = stock;
                }

                // Cantidad en unidades base (la que suma al stock), con la presentación en que llegó y su factor
                var presentacion = PresentacionDe(d);
                var cantidadBase = d.Cantidad * FactorDe(d);
                movimiento.DetalleMovimientos.Add(new DetalleMovimiento
                {
                    IdProducto = d.ProductoId,
                    IdUbicacion = d.UbicacionId,
                    Cantidad = cantidadBase,
                    Presentacion = presentacion?.Nombre,
                    Factor = FactorDe(d),
                    PrecioUnitarioSnapshot = presentacion?.Precio ?? precios[d.ProductoId],
                    StockAnterior = stock.CantidadActual,
                    FechaVencimiento = d.FechaVencimiento,
                    Lote = d.Lote
                });
                stock.CantidadActual += cantidadBase;
                stock.UltimaActualizacion = ahora;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Otra operación cambió el stock de estas zonas mientras tanto: nada se guardó, se puede reintentar
                ModelState.AddModelError("", "El stock de alguna zona cambió mientras registrabas la entrada. Vuelve a registrarla.");
                _context.ChangeTracker.Clear();
                await CargarDatosEntrada(model);
                return View(model);
            }

            if (model.Evidencia is { Length: > 0 })
                await OperacionesAlmacen.GuardarEvidencia(_context, movimiento.IdMovimiento, model.Evidencia, "Entrada");
            await VencimientoProducto.ActualizarAsync(_context, detalles.Select(d => d.ProductoId));

            var unidades = detalles.Sum(d => d.Cantidad * FactorDe(d));
            TempData["Exito"] = $"Entrada registrada: {detalles.Count} {(detalles.Count == 1 ? "producto" : "productos")}, {unidades} {(unidades == 1 ? "unidad" : "unidades")}.";
            return RedirectToAction(nameof(Detalle), new { id = movimiento.IdMovimiento });
        }

        // ===== Detalle de una entrada (las ventas se ven en su nota) =====
        public async Task<IActionResult> Detalle(int id)
        {
            var mov = await _context.Movimientos
                .Where(m => m.IdMovimiento == id)
                .Select(m => new
                {
                    m.IdMovimiento,
                    m.Tipo,
                    m.Fecha,
                    m.IdSede,
                    m.Observaciones,
                    TieneNota = m.NotaVentum != null,
                    Usuario = m.IdUsuarioNavigation.Nombre,
                    Sede = m.IdSedeNavigation.Nombre,
                    Proveedor = m.IdProveedorNavigation != null ? m.IdProveedorNavigation.Nombre : null,
                    Cliente = m.IdClienteNavigation != null ? m.IdClienteNavigation.Nombre : null,
                    Lineas = m.DetalleMovimientos
                        .OrderBy(d => d.IdDetalle)
                        .Select(d => new
                        {
                            d.IdProductoNavigation.Nombre,
                            d.IdProductoNavigation.Codigo,
                            d.IdProductoNavigation.UnidadMedida,
                            Zona = d.IdUbicacionNavigation.CodigoEstante,
                            d.Cantidad,
                            d.StockAnterior,
                            d.FechaVencimiento,
                            d.Lote,
                            d.Presentacion,
                            d.Factor
                        })
                        .ToList(),
                    Evidencias = m.Evidencia.OrderBy(e => e.Fecha).Select(e => new EvidenciaNota(e.UrlArchivo, e.Fecha)).ToList()
                })
                .FirstOrDefaultAsync();

            if (mov == null)
                return NotFound();
            if (User.SedeId() is int sede && mov.IdSede != sede)
                return RedirectToAction("AccessDenied", "Account");
            if (mov.TieneNota)
                return RedirectToAction("Nota", "Venta", new { id });

            var esEntrada = mov.Tipo == "Entrada";
            return View(new MovimientoDetalleViewModel
            {
                Id = mov.IdMovimiento,
                EsEntrada = esEntrada,
                Fecha = mov.Fecha,
                Usuario = mov.Usuario,
                Sede = mov.Sede,
                Contraparte = esEntrada ? mov.Proveedor : mov.Cliente,
                Documento = string.IsNullOrWhiteSpace(mov.Observaciones) ? null : mov.Observaciones,
                Lineas = mov.Lineas
                    .Select(l => new LineaDetalleMovimiento(l.Nombre, l.Codigo, l.UnidadMedida, l.Zona, l.Cantidad, l.StockAnterior,
                        esEntrada ? l.StockAnterior + l.Cantidad : l.StockAnterior - l.Cantidad, l.FechaVencimiento, l.Lote, l.Presentacion, l.Factor))
                    .ToList(),
                Evidencias = mov.Evidencias
            });
        }

        // Foto de la mercadería recibida o de la guía, después de registrar la entrada
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarEvidencia(int id, IFormFile? foto)
        {
            var mov = await _context.Movimientos
                .Where(m => m.IdMovimiento == id && m.Tipo == "Entrada")
                .Select(m => new { m.IdSede })
                .FirstOrDefaultAsync();
            if (mov == null)
                return NotFound();
            if (User.SedeId() is int sede && mov.IdSede != sede)
                return RedirectToAction("AccessDenied", "Account");

            if (foto is not { Length: > 0 })
                TempData["Error"] = "Elige una foto para adjuntar.";
            else if (OperacionesAlmacen.ValidarEvidencia(foto) is string error)
                TempData["Error"] = error;
            else
            {
                await OperacionesAlmacen.GuardarEvidencia(_context, id, foto, "Entrada");
                TempData["Exito"] = "Foto guardada.";
            }
            return RedirectToAction(nameof(Detalle), new { id });
        }

        // Las salidas ahora son ventas con nota de venta
        public IActionResult Salida() => RedirectToAction("Nueva", "Venta");

        [NonAction]
        private async Task CargarDatosEntrada(MovimientoEntradaViewModel model)
        {
            var sedeId = User.SedeId();
            var datos = model.Datos;

            var presentaciones = (await _context.ProductoPresentacions
                    .OrderBy(pp => pp.Factor)
                    .Select(pp => new { pp.IdProducto, pp.Nombre, pp.Factor, pp.Precio })
                    .ToListAsync())
                .ToLookup(pp => pp.IdProducto, pp => new PresentacionVenta(pp.Nombre, pp.Factor, pp.Precio));
            datos.Productos = (await _context.Productos
                    .OrderBy(p => p.Nombre)
                    .Select(p => new { p.IdProducto, p.Nombre, p.Codigo, p.UnidadMedida })
                    .ToListAsync())
                .Select(p => new ProductoEntrada(p.IdProducto, p.Nombre, p.Codigo, p.UnidadMedida, presentaciones[p.IdProducto].ToList()))
                .ToList();

            datos.Zonas = await _context.Ubicaciones
                .Where(u => sedeId == null || u.IdSede == sedeId)
                .OrderBy(u => u.IdSedeNavigation.Nombre)
                .ThenBy(u => u.CodigoEstante)
                .Select(u => new ZonaEntrada(u.IdUbicacion, u.CodigoEstante, u.Descripcion, u.Tipo == "recepcion", u.IdSedeNavigation.Nombre))
                .ToListAsync();

            datos.Stock = await _context.ProductoUbicacions
                .Where(pu => pu.CantidadActual > 0 && (sedeId == null || pu.IdUbicacionNavigation.IdSede == sedeId))
                .Select(pu => new StockZonaEntrada(pu.IdProducto, pu.IdUbicacion, pu.CantidadActual))
                .ToListAsync();

            datos.Proveedores = await _context.Proveedores
                .OrderBy(p => p.Nombre)
                .Select(p => new ProveedorOpcion(p.IdProveedor, p.Nombre, p.Celular))
                .ToListAsync();

            // Crear productos y proveedores es de dueña y encargada
            datos.PuedeCrear = User.IsInRole("duena") || User.IsInRole("encargada");
            datos.VariasSedes = datos.Zonas.Select(z => z.Sede).Distinct().Count() > 1;
        }
    }
}
