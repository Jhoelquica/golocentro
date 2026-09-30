using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Helpers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    // Conteo físico por zona: el stock de la zona pasa a ser lo contado y queda la auditoría
    // (ajuste_inventario / detalle_ajuste). Lo usan todos los roles: el trabajador es quien
    // más se encarga del almacén.
    [Authorize]
    public class InventarioController : Controller
    {
        private readonly AppDbContext _context;

        public InventarioController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? sede)
        {
            var sedeUsuario = User.SedeId();
            var sedes = await _context.Sedes
                .Where(s => sedeUsuario == null || s.IdSede == sedeUsuario)
                .Where(s => s.Ubicacions.Any())
                .OrderBy(s => s.Nombre)
                .Select(s => new SedeOpcion(s.IdSede, s.Nombre))
                .ToListAsync();

            var modelo = new ConteoIndexViewModel
            {
                Sedes = sedes,
                PuedeElegirSede = sedeUsuario == null && sedes.Count > 1
            };

            var elegida = sedes.FirstOrDefault(s => s.Id == sede) ?? sedes.FirstOrDefault();
            if (elegida == null)
                return View(modelo);

            modelo.SedeId = elegida.Id;
            modelo.SedeNombre = elegida.Nombre;
            modelo.Zonas = await _context.Ubicaciones
                .Where(u => u.IdSede == elegida.Id)
                .OrderBy(u => u.CodigoEstante)
                .Select(u => new ZonaConteo(
                    u.IdUbicacion,
                    u.CodigoEstante,
                    u.Descripcion,
                    u.Tipo == "recepcion",
                    u.ProductoUbicacions.Count(pu => pu.CantidadActual > 0),
                    u.ProductoUbicacions.Sum(pu => pu.CantidadActual),
                    u.DetalleAjustes.Max(d => (DateTime?)d.IdAjusteNavigation.Fecha)))
                .ToListAsync();

            modelo.Recientes = await _context.AjusteInventarios
                .Where(a => a.IdSede == elegida.Id)
                .OrderByDescending(a => a.Fecha)
                .Take(8)
                .Select(a => new AjusteReciente(
                    a.Fecha,
                    a.DetalleAjustes.Select(d => d.IdUbicacionNavigation.CodigoEstante).FirstOrDefault(),
                    a.Motivo,
                    a.IdUsuarioNavigation.Nombre,
                    a.DetalleAjustes.Count,
                    a.DetalleAjustes.Count(d => d.CantidadAnterior != d.CantidadNueva)))
                .ToListAsync();

            return View(modelo);
        }

        public async Task<IActionResult> Contar(int id)
        {
            var model = new ConteoFormViewModel();
            if (!await LlenarDatos(model, id))
                return NotFound();

            model.Motivo = model.Datos.YaContada ? "conteo" : "conteo_inicial";
            foreach (var productoId in model.Datos.EnSistema.Keys)
                model.Lineas[$"p{productoId}"] = new LineaConteo { ProductoId = productoId };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contar(int id, ConteoFormViewModel model)
        {
            var usuarioIdClaim = User.FindFirst("UsuarioId")?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim))
                return RedirectToAction("Login", "Account");

            if (!await LlenarDatos(model, id))
                return NotFound();

            var datos = model.Datos;
            var nombres = datos.Catalogo.ToDictionary(p => p.Id, p => p.Nombre);
            var lineas = model.Lineas.Values.ToList();

            if (!MotivosAjuste.EsValido(model.Motivo))
                ModelState.AddModelError(nameof(model.Motivo), "Elige el motivo del conteo.");

            foreach (var linea in lineas)
            {
                if (!nombres.ContainsKey(linea.ProductoId))
                    ModelState.AddModelError("", "Uno de los productos ya no existe en el catálogo.");
                else if (linea.Cantidad == null)
                    ModelState.AddModelError($"Lineas[p{linea.ProductoId}].Cantidad", $"Escribe cuánto contaste de {nombres[linea.ProductoId]} (0 si no hay).");
                else if (linea.Cantidad < 0)
                    ModelState.AddModelError($"Lineas[p{linea.ProductoId}].Cantidad", $"La cantidad de {nombres[linea.ProductoId]} no puede ser negativa.");
            }

            // Todo lo que el sistema tiene en la zona tiene que contarse (0 si ya no está)
            foreach (var productoId in datos.EnSistema.Keys.Where(p => lineas.All(l => l.ProductoId != p)))
                ModelState.AddModelError("", $"Falta contar {nombres.GetValueOrDefault(productoId, "un producto")}, que el sistema tiene en esta zona.");

            // Un producto agregado con 0 que tampoco estaba en el sistema no cambia nada
            var aGuardar = lineas
                .Where(l => l.Cantidad.HasValue && nombres.ContainsKey(l.ProductoId))
                .Where(l => l.Cantidad > 0 || datos.EnSistema.ContainsKey(l.ProductoId))
                .ToList();
            if (ModelState.IsValid && aGuardar.Count == 0)
                ModelState.AddModelError("", "No hay nada que guardar: agrega los productos que encontraste en la zona.");

            if (!ModelState.IsValid)
                return View(model);

            var ahora = DateTime.Now;
            var stocks = await _context.ProductoUbicacions
                .Where(pu => pu.IdUbicacion == id)
                .ToDictionaryAsync(pu => pu.IdProducto);

            var ajuste = new AjusteInventario
            {
                Fecha = ahora,
                IdUsuario = int.Parse(usuarioIdClaim),
                IdSede = datos.SedeId,
                Motivo = model.Motivo!,
                Observaciones = string.IsNullOrWhiteSpace(model.Observaciones) ? null : model.Observaciones.Trim()
            };

            var cambios = 0;
            foreach (var linea in aGuardar)
            {
                var nueva = linea.Cantidad!.Value;
                stocks.TryGetValue(linea.ProductoId, out var stock);
                var anterior = stock?.CantidadActual ?? 0;

                ajuste.DetalleAjustes.Add(new DetalleAjuste
                {
                    IdProducto = linea.ProductoId,
                    IdUbicacion = id,
                    CantidadAnterior = anterior,
                    CantidadNueva = nueva
                });

                if (anterior == nueva)
                    continue;

                cambios++;
                if (stock == null)
                {
                    stock = new ProductoUbicacion { IdProducto = linea.ProductoId, IdUbicacion = id };
                    _context.ProductoUbicacions.Add(stock);
                }
                stock.CantidadActual = nueva;
                stock.UltimaActualizacion = ahora;
            }

            _context.AjusteInventarios.Add(ajuste);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Una venta o entrada tocó la zona mientras se contaba: se muestran las cantidades nuevas del sistema
                ModelState.AddModelError("", "Mientras contabas, alguien vendió o recibió mercadería en esta zona. Revisa lo contado y vuelve a guardar.");
                _context.ChangeTracker.Clear();
                await LlenarDatos(model, id);
                return View(model);
            }

            var coinciden = aGuardar.Count - cambios;
            TempData["Exito"] = $"Conteo de {datos.ZonaCodigo} guardado: " +
                (cambios == 0 ? "todo coincide con el sistema." :
                 $"{cambios} {(cambios == 1 ? "producto corregido" : "productos corregidos")}" +
                 (coinciden > 0 ? $", {coinciden} {(coinciden == 1 ? "coincide" : "coinciden")}." : "."));
            return RedirectToAction(nameof(Index), new { sede = datos.SedeId });
        }

        // El conteo lo pide al volver a la pestaña: así aparece un producto recién creado en Productos
        // sin recargar la página (y sin perder lo ya contado).
        [HttpGet]
        public async Task<IActionResult> Catalogo() =>
            Json(await _context.Productos
                .OrderBy(p => p.Nombre)
                .Select(p => new ProductoCatalogo(p.IdProducto, p.Nombre, p.Codigo))
                .ToListAsync());

        // false si la zona no existe o no es de la sede del usuario
        [NonAction]
        private async Task<bool> LlenarDatos(ConteoFormViewModel model, int zonaId)
        {
            var zona = await _context.Ubicaciones
                .Where(u => u.IdUbicacion == zonaId)
                .Select(u => new
                {
                    u.IdUbicacion,
                    u.CodigoEstante,
                    u.Descripcion,
                    u.Tipo,
                    u.IdSede,
                    Sede = u.IdSedeNavigation.Nombre,
                    YaContada = u.DetalleAjustes.Any()
                })
                .FirstOrDefaultAsync();

            var sedeUsuario = User.SedeId();
            if (zona == null || (sedeUsuario.HasValue && zona.IdSede != sedeUsuario.Value))
                return false;

            model.Datos = new ConteoDatos
            {
                ZonaId = zona.IdUbicacion,
                ZonaCodigo = zona.CodigoEstante,
                ZonaDescripcion = zona.Descripcion,
                EsRecepcion = zona.Tipo == "recepcion",
                SedeId = zona.IdSede,
                SedeNombre = zona.Sede,
                YaContada = zona.YaContada,
                Catalogo = await _context.Productos
                    .OrderBy(p => p.Nombre)
                    .Select(p => new ProductoCatalogo(p.IdProducto, p.Nombre, p.Codigo))
                    .ToListAsync(),
                EnSistema = await _context.ProductoUbicacions
                    .Where(pu => pu.IdUbicacion == zonaId && pu.CantidadActual > 0)
                    .ToDictionaryAsync(pu => pu.IdProducto, pu => pu.CantidadActual)
            };
            return true;
        }
    }
}
