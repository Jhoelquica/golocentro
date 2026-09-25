using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    // Panel del almacén: plano con buscador de productos y "Mover mercadería" entre zonas.
    // Lo usan todos los roles (los trabajadores son quienes ubican y ordenan la mercadería).
    [Authorize]
    public class MapaController : Controller
    {
        private readonly AppDbContext _context;

        public MapaController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? sede) => View(await CargarMapa(sede));

        // Carteles para rotular cada zona en el piso (página de impresión, sin el layout de la app)
        public async Task<IActionResult> Carteles(int? sede) => View(await CargarMapa(sede));

        [NonAction]
        private async Task<MapaViewModel> CargarMapa(int? sede)
        {
            var sedeUsuario = SedeDelUsuario();
            var sedesConPlano = await _context.Sedes
                .Where(s => s.PlanoAncho != null && s.PlanoAlto != null)
                .Where(s => sedeUsuario == null || s.IdSede == sedeUsuario)
                .OrderBy(s => s.Nombre)
                .Select(s => new SedeOpcion(s.IdSede, s.Nombre))
                .ToListAsync();

            var modelo = new MapaViewModel
            {
                Sedes = sedesConPlano,
                PuedeElegirSede = sedeUsuario == null && sedesConPlano.Count > 1
            };

            var elegida = sedesConPlano.FirstOrDefault(s => s.Id == sede) ?? sedesConPlano.FirstOrDefault();
            if (elegida == null)
                return modelo;

            var plano = await _context.Sedes
                .Where(s => s.IdSede == elegida.Id)
                .Select(s => new { s.PlanoAncho, s.PlanoAlto })
                .FirstAsync();

            modelo.SedeId = elegida.Id;
            modelo.SedeNombre = elegida.Nombre;
            modelo.Ancho = plano.PlanoAncho!.Value;
            modelo.Alto = plano.PlanoAlto!.Value;
            modelo.Zonas = await CargarZonas(elegida.Id);
            modelo.Productos = await CargarProductos(elegida.Id);
            modelo.Lineas = await _context.PlanoLineas
                .Where(l => l.IdSede == elegida.Id)
                .Select(l => new LineaMapa(l.Tipo, l.X1, l.Y1, l.X2, l.Y2))
                .ToListAsync();

            return modelo;
        }

        public async Task<IActionResult> Mover(int? producto, int? origen, int? sede)
        {
            var model = new MoverFormViewModel { ProductoId = producto, OrigenId = origen, Sede = sede };
            await LlenarDatosMover(model);
            ModelState.Clear();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Mover(MoverFormViewModel model)
        {
            var usuarioIdClaim = User.FindFirst("UsuarioId")?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim))
                return RedirectToAction("Login", "Account");

            var sedeId = await LlenarDatosMover(model);
            if (sedeId == null)
            {
                ModelState.AddModelError("", "No hay zonas registradas en tu sede.");
                return View(model);
            }

            // Solo se aceptan zonas de la sede resuelta (la del usuario, o la elegida por la dueña)
            var zonas = model.Datos.Zonas;
            var origen = zonas.FirstOrDefault(z => z.Id == model.OrigenId);
            var destino = zonas.FirstOrDefault(z => z.Id == model.DestinoId);
            if (model.OrigenId.HasValue && origen == null)
                ModelState.AddModelError(nameof(model.OrigenId), "Esa zona no pertenece a esta sede.");
            if (model.DestinoId.HasValue && destino == null)
                ModelState.AddModelError(nameof(model.DestinoId), "Esa zona no pertenece a esta sede.");
            if (origen != null && destino != null && origen.Id == destino.Id)
                ModelState.AddModelError(nameof(model.DestinoId), "El destino tiene que ser una zona distinta al origen.");

            var nombreProducto = model.ProductoId.HasValue
                ? await _context.Productos.Where(p => p.IdProducto == model.ProductoId).Select(p => p.Nombre).FirstOrDefaultAsync()
                : null;
            if (model.ProductoId.HasValue && nombreProducto == null)
                ModelState.AddModelError(nameof(model.ProductoId), "Ese producto no existe.");

            List<ProductoUbicacion> stocks = new();
            if (ModelState.IsValid)
            {
                stocks = await _context.ProductoUbicacions
                    .Where(pu => pu.IdProducto == model.ProductoId
                              && (pu.IdUbicacion == model.OrigenId || pu.IdUbicacion == model.DestinoId))
                    .ToListAsync();
                var disponible = stocks.FirstOrDefault(s => s.IdUbicacion == model.OrigenId)?.CantidadActual ?? 0;
                if (model.Cantidad > disponible)
                    ModelState.AddModelError(nameof(model.Cantidad), disponible == 0
                        ? $"En {origen!.Codigo} no hay {nombreProducto}."
                        : $"En {origen!.Codigo} solo hay {disponible} de {nombreProducto}.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var ahora = DateTime.Now;
            var cantidad = model.Cantidad!.Value;
            var stockOrigen = stocks.First(s => s.IdUbicacion == model.OrigenId);
            var stockDestino = stocks.FirstOrDefault(s => s.IdUbicacion == model.DestinoId);
            if (stockDestino == null)
            {
                stockDestino = new ProductoUbicacion { IdProducto = model.ProductoId!.Value, IdUbicacion = model.DestinoId!.Value };
                _context.ProductoUbicacions.Add(stockDestino);
            }

            stockOrigen.CantidadActual -= cantidad;
            stockOrigen.UltimaActualizacion = ahora;
            stockDestino.CantidadActual += cantidad;
            stockDestino.UltimaActualizacion = ahora;

            var traslado = new Traslado
            {
                Fecha = ahora,
                IdUsuario = int.Parse(usuarioIdClaim),
                IdSede = sedeId.Value,
                Observaciones = string.IsNullOrWhiteSpace(model.Observaciones) ? null : model.Observaciones.Trim()
            };
            traslado.DetalleTraslados.Add(new DetalleTraslado
            {
                IdProducto = model.ProductoId!.Value,
                Cantidad = cantidad,
                IdUbicacionOrigen = origen!.Id,
                IdUbicacionDestino = destino!.Id
            });
            _context.Traslados.Add(traslado);
            await _context.SaveChangesAsync();

            TempData["Exito"] = $"Listo: {cantidad} de {nombreProducto} pasaron de {origen.Codigo} a {destino.Codigo}.";
            // Se queda en el mismo origen para seguir ordenando (por ejemplo, vaciando Recepción)
            return RedirectToAction(nameof(Mover), new { origen = origen.Id, sede = sedeId });
        }

        [NonAction]
        private int? SedeDelUsuario()
        {
            var claim = User.FindFirst("SedeId")?.Value;
            return string.IsNullOrEmpty(claim) ? null : int.Parse(claim);
        }

        // Devuelve la sede con la que se trabaja (null si no hay zonas) y deja listos los datos de la vista
        [NonAction]
        private async Task<int?> LlenarDatosMover(MoverFormViewModel model)
        {
            var sedeUsuario = SedeDelUsuario();
            var sedes = await _context.Sedes
                .Where(s => sedeUsuario == null || s.IdSede == sedeUsuario)
                .Where(s => s.Ubicacions.Any())
                .OrderBy(s => s.Nombre)
                .Select(s => new SedeOpcion(s.IdSede, s.Nombre))
                .ToListAsync();

            var elegida = sedes.FirstOrDefault(s => s.Id == model.Sede) ?? sedes.FirstOrDefault();
            model.Datos.Sedes = sedes;
            model.Datos.PuedeElegirSede = sedeUsuario == null && sedes.Count > 1;
            if (elegida == null)
                return null;

            model.Sede = elegida.Id;
            model.Datos.SedeNombre = elegida.Nombre;
            model.Datos.Zonas = await CargarZonas(elegida.Id);
            model.Datos.Productos = (await CargarProductos(elegida.Id)).Where(p => p.StockTotal > 0).ToList();
            model.Datos.Recientes = await _context.DetalleTraslados
                .Where(d => d.IdTrasladoNavigation.IdSede == elegida.Id)
                .OrderByDescending(d => d.IdTrasladoNavigation.Fecha)
                .Take(8)
                .Select(d => new TrasladoReciente(
                    d.IdTrasladoNavigation.Fecha,
                    d.IdProductoNavigation.Nombre,
                    d.Cantidad,
                    d.IdUbicacionOrigenNavigation.CodigoEstante,
                    d.IdUbicacionDestinoNavigation.CodigoEstante,
                    d.IdTrasladoNavigation.IdUsuarioNavigation.Nombre))
                .ToListAsync();
            return elegida.Id;
        }

        [NonAction]
        private Task<List<ZonaMapa>> CargarZonas(int sedeId) =>
            _context.Ubicaciones
                .Where(u => u.IdSede == sedeId)
                .OrderBy(u => u.CodigoEstante)
                .Select(u => new ZonaMapa(
                    u.IdUbicacion,
                    u.CodigoEstante,
                    u.Descripcion,
                    u.Tipo == "recepcion",
                    u.PosX, u.PosY, u.Ancho, u.Alto,
                    u.ProductoUbicacions
                        .Where(pu => pu.CantidadActual > 0)
                        .OrderByDescending(pu => pu.CantidadActual)
                        .Select(pu => new StockEnZona(pu.IdProducto, pu.CantidadActual))
                        .ToList()))
                .ToListAsync();

        [NonAction]
        private Task<List<ProductoMapa>> CargarProductos(int sedeId) =>
            _context.Productos
                .OrderBy(p => p.Nombre)
                .Select(p => new ProductoMapa(
                    p.IdProducto,
                    p.Nombre,
                    p.Codigo,
                    p.ProductoUbicacions
                        .Where(pu => pu.IdUbicacionNavigation.IdSede == sedeId)
                        .Sum(pu => pu.CantidadActual),
                    p.StockMinimo))
                .ToListAsync();
    }
}
