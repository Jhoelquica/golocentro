using System.Net.Mail;
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
    // Datos del negocio, sus sedes y las zonas de cada sede. Solo la dueña.
    [Authorize(Roles = Roles.Duena)]
    public class NegocioController : Controller
    {
        private static readonly Regex Telefono = new(@"^[0-9 +]{6,15}$");
        private static readonly Regex Ruc = new(@"^(10|15|17|20)\d{9}$");
        private static readonly Regex CodigoZona = new(@"^[A-Z0-9][A-Z0-9-]{0,19}$");

        // Plano por defecto de una sede nueva (mismo tamaño que el de la Sede Principal)
        private const decimal PlanoAncho = 30, PlanoAlto = 20;
        private readonly AppDbContext _context;

        public NegocioController(AppDbContext context)
        {
            _context = context;
        }

        // ===== Datos del negocio =====
        public async Task<IActionResult> Index()
        {
            var datos = await NegocioInfo.Cargar(_context);
            var model = new NegocioFormViewModel
            {
                NombreComercial = datos.NombreComercial,
                RazonSocial = datos.RazonSocial,
                Ruc = datos.Ruc,
                Telefono = datos.Telefono,
                Correo = datos.Correo,
                MensajeNota = datos.MensajeNota
            };
            await CargarSedes(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(NegocioFormViewModel model)
        {
            model.NombreComercial = Limpiar(model.NombreComercial);
            model.RazonSocial = Limpiar(model.RazonSocial);
            model.Ruc = Limpiar(model.Ruc);
            model.Telefono = Limpiar(model.Telefono);
            model.Correo = Limpiar(model.Correo);
            model.MensajeNota = Limpiar(model.MensajeNota);

            if (model.NombreComercial == null)
                ModelState.AddModelError(nameof(model.NombreComercial), "Escribe el nombre del negocio.");
            else if (model.NombreComercial.Length > 100)
                ModelState.AddModelError(nameof(model.NombreComercial), "El nombre no puede pasar de 100 caracteres.");
            if (model.RazonSocial?.Length > 150)
                ModelState.AddModelError(nameof(model.RazonSocial), "La razón social no puede pasar de 150 caracteres.");
            if (model.Ruc != null && !Ruc.IsMatch(model.Ruc))
                ModelState.AddModelError(nameof(model.Ruc), "El RUC tiene 11 dígitos y empieza con 10, 15, 17 o 20.");
            if (model.Telefono != null && !Telefono.IsMatch(model.Telefono))
                ModelState.AddModelError(nameof(model.Telefono), "Usa solo números y espacios (6 a 15), por ejemplo 966 333 444.");
            if (model.Correo != null && (model.Correo.Length > 100 || !MailAddress.TryCreate(model.Correo, out _)))
                ModelState.AddModelError(nameof(model.Correo), "Escribe un correo válido, por ejemplo ventas@golocentro.pe.");
            if (model.MensajeNota?.Length > 120)
                ModelState.AddModelError(nameof(model.MensajeNota), "El mensaje no puede pasar de 120 caracteres.");

            if (!ModelState.IsValid)
            {
                await CargarSedes(model);
                return View(model);
            }

            var negocio = await _context.Negocios.FirstOrDefaultAsync();
            if (negocio == null)
            {
                negocio = new Negocio { IdNegocio = 1 };
                _context.Negocios.Add(negocio);
            }
            negocio.NombreComercial = model.NombreComercial!;
            negocio.RazonSocial = model.RazonSocial;
            negocio.Ruc = model.Ruc;
            negocio.Telefono = model.Telefono;
            negocio.Correo = model.Correo;
            negocio.MensajeNota = model.MensajeNota;
            await _context.SaveChangesAsync();

            TempData["Exito"] = "Datos del negocio guardados. Ya salen en las notas de venta.";
            return RedirectToAction(nameof(Index));
        }

        // ===== Sedes =====
        public async Task<IActionResult> NuevaSede() =>
            View("Sede", new SedeFormViewModel { Ciudad = "Huamanga", Negocio = await NegocioInfo.Cargar(_context) });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NuevaSede(SedeFormViewModel model)
        {
            model.Id = 0;
            await ValidarSede(model);
            if (!ModelState.IsValid)
            {
                model.Negocio = await NegocioInfo.Cargar(_context);
                return View("Sede", model);
            }

            var sede = new Models.Sede
            {
                Nombre = model.Nombre!,
                Direccion = model.Direccion!,
                Ciudad = model.Ciudad!,
                Telefono = model.Telefono,
                Estado = "activo",
                PlanoAncho = PlanoAncho,
                PlanoAlto = PlanoAlto
            };
            if (model.CrearRecepcion)
                sede.Ubicacions.Add(new Ubicacion
                {
                    CodigoEstante = "R-01",
                    Descripcion = "Recepción: lo que llega, antes de ordenar",
                    Tipo = TipoZona.Recepcion
                });
            _context.Sedes.Add(sede);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                // La regla vieja (código de zona único en toda la base) no deja crear otra R-01
                ModelState.AddModelError(nameof(model.CrearRecepcion),
                    "No se pudo crear la zona R-01: falta correr el script Database/2026-09-28_zonas_por_sede.sql. Crea la sede sin marcar esa opción o corre el script primero.");
                _context.ChangeTracker.Clear();
                model.Negocio = await NegocioInfo.Cargar(_context);
                return View("Sede", model);
            }

            TempData["Exito"] = $"Sede «{sede.Nombre}» creada. Agrega sus zonas para poder registrar entradas y vender desde ahí.";
            return RedirectToAction(nameof(Zonas), new { id = sede.IdSede });
        }

        public async Task<IActionResult> Sede(int id)
        {
            var sede = await _context.Sedes.FindAsync(id);
            if (sede == null)
                return NotFound();

            return View(new SedeFormViewModel
            {
                Id = sede.IdSede,
                Nombre = sede.Nombre,
                Direccion = sede.Direccion,
                Ciudad = sede.Ciudad,
                Telefono = sede.Telefono,
                Negocio = await NegocioInfo.Cargar(_context),
                MotivoNoEliminar = await MotivoNoEliminarSede(id)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sede(int id, SedeFormViewModel model)
        {
            var sede = await _context.Sedes.FindAsync(id);
            if (sede == null)
                return NotFound();

            model.Id = id;
            await ValidarSede(model);
            if (!ModelState.IsValid)
            {
                model.Negocio = await NegocioInfo.Cargar(_context);
                model.MotivoNoEliminar = await MotivoNoEliminarSede(id);
                return View(model);
            }

            sede.Nombre = model.Nombre!;
            sede.Direccion = model.Direccion!;
            sede.Ciudad = model.Ciudad!;
            sede.Telefono = model.Telefono;
            await _context.SaveChangesAsync();

            TempData["Exito"] = $"Sede «{sede.Nombre}» guardada.";
            return RedirectToAction(nameof(Index));
        }

        // Para una sede creada por error: solo si no tiene historial ni personal; se lleva sus zonas vacías y alertas
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSede(int id)
        {
            var sede = await _context.Sedes.FindAsync(id);
            if (sede == null)
                return NotFound();

            if (await MotivoNoEliminarSede(id) is string motivo)
            {
                TempData["Error"] = motivo;
                return RedirectToAction(nameof(Sede), new { id });
            }

            var zonas = await _context.Ubicaciones.Where(u => u.IdSede == id).ToListAsync();
            var idsZonas = zonas.Select(z => z.IdUbicacion).ToList();
            _context.ProductoUbicacions.RemoveRange(await _context.ProductoUbicacions.Where(pu => idsZonas.Contains(pu.IdUbicacion)).ToListAsync());
            _context.Ubicaciones.RemoveRange(zonas);
            _context.PlanoLineas.RemoveRange(await _context.PlanoLineas.Where(l => l.IdSede == id).ToListAsync());
            _context.Alerta.RemoveRange(await _context.Alerta.Where(a => a.IdSede == id).ToListAsync());
            _context.Sedes.Remove(sede);
            await _context.SaveChangesAsync();

            TempData["Exito"] = $"Sede «{sede.Nombre}» eliminada.";
            return RedirectToAction(nameof(Index));
        }

        // ===== Zonas de una sede =====
        public async Task<IActionResult> Zonas(int id)
        {
            var sede = await _context.Sedes.Where(s => s.IdSede == id).Select(s => s.Nombre).FirstOrDefaultAsync();
            if (sede == null)
                return NotFound();
            return View(await ModeloZonas(id, sede, new ZonaFormViewModel()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarZona(int id, ZonaFormViewModel model)
        {
            var sede = await _context.Sedes.Where(s => s.IdSede == id).Select(s => s.Nombre).FirstOrDefaultAsync();
            if (sede == null)
                return NotFound();

            model.Id = 0;
            await ValidarZona(model, id);
            if (ModelState.IsValid)
            {
                _context.Ubicaciones.Add(new Ubicacion
                {
                    IdSede = id,
                    CodigoEstante = model.Codigo!,
                    Descripcion = model.Descripcion,
                    Tipo = model.Tipo
                });
                if (await GuardarZona(model))
                {
                    TempData["Exito"] = $"Zona {model.Codigo} agregada.";
                    return RedirectToAction(nameof(Zonas), new { id });
                }
            }
            return View(nameof(Zonas), await ModeloZonas(id, sede, model));
        }

        public async Task<IActionResult> Zona(int id)
        {
            var zona = await _context.Ubicaciones
                .Where(u => u.IdUbicacion == id)
                .Select(u => new ZonaFormViewModel
                {
                    Id = u.IdUbicacion,
                    Codigo = u.CodigoEstante,
                    Descripcion = u.Descripcion,
                    Tipo = u.Tipo,
                    SedeId = u.IdSede,
                    SedeNombre = u.IdSedeNavigation.Nombre
                })
                .FirstOrDefaultAsync();
            return zona == null ? NotFound() : View(zona);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Zona(int id, ZonaFormViewModel model)
        {
            var zona = await _context.Ubicaciones.Include(u => u.IdSedeNavigation).FirstOrDefaultAsync(u => u.IdUbicacion == id);
            if (zona == null)
                return NotFound();

            model.Id = id;
            model.SedeId = zona.IdSede;
            model.SedeNombre = zona.IdSedeNavigation.Nombre;
            await ValidarZona(model, zona.IdSede);
            if (ModelState.IsValid)
            {
                zona.CodigoEstante = model.Codigo!;
                zona.Descripcion = model.Descripcion;
                zona.Tipo = model.Tipo;
                if (await GuardarZona(model))
                {
                    TempData["Exito"] = $"Zona {zona.CodigoEstante} guardada.";
                    return RedirectToAction(nameof(Zonas), new { id = zona.IdSede });
                }
            }
            return View(model);
        }

        // Solo se borra una zona vacía y sin historial; si tiene historial, se renombra
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarZona(int id)
        {
            var zona = await _context.Ubicaciones.FindAsync(id);
            if (zona == null)
                return NotFound();

            if (await _context.ProductoUbicacions.AnyAsync(pu => pu.IdUbicacion == id && pu.CantidadActual > 0))
                TempData["Error"] = $"La zona {zona.CodigoEstante} tiene mercadería. Muévela a otra zona antes de borrarla.";
            else if (await TieneHistorial(id))
                TempData["Error"] = $"La zona {zona.CodigoEstante} tiene historial (entradas, ventas, conteos o traslados). En vez de borrarla, cámbiale el código o la descripción.";
            else
            {
                _context.ProductoUbicacions.RemoveRange(await _context.ProductoUbicacions.Where(pu => pu.IdUbicacion == id).ToListAsync());
                _context.Ubicaciones.Remove(zona);
                await _context.SaveChangesAsync();
                TempData["Exito"] = $"Zona {zona.CodigoEstante} eliminada.";
            }
            return RedirectToAction(nameof(Zonas), new { id = zona.IdSede });
        }

        // ===== Auxiliares =====
        [NonAction]
        private async Task<string?> MotivoNoEliminarSede(int id)
        {
            if (await _context.Sedes.CountAsync() <= 1)
                return "Es la única sede del negocio.";
            var cuentas = await _context.Usuarios.CountAsync(u => u.IdSede == id);
            if (cuentas > 0)
                return $"Tiene {cuentas} {(cuentas == 1 ? "cuenta asignada" : "cuentas asignadas")} (activas o no). Pásalas a otra sede en Usuarios.";
            if (await _context.Movimientos.AnyAsync(m => m.IdSede == id) || await _context.Traslados.AnyAsync(t => t.IdSede == id)
                || await _context.AjusteInventarios.AnyAsync(a => a.IdSede == id) || await _context.Camaras.AnyAsync(c => c.IdSede == id)
                || await _context.Reportes.AnyAsync(r => r.IdSede == id))
                return "Ya tiene historial (entradas, ventas, conteos o traslados): se conserva para los reportes.";
            if (await _context.ProductoUbicacions.AnyAsync(pu => pu.IdUbicacionNavigation.IdSede == id && pu.CantidadActual > 0))
                return "Tiene mercadería en sus zonas.";
            return null;
        }

        [NonAction]
        private async Task ValidarSede(SedeFormViewModel model)
        {
            model.Nombre = Limpiar(model.Nombre);
            model.Direccion = Limpiar(model.Direccion);
            model.Ciudad = Limpiar(model.Ciudad);
            model.Telefono = Limpiar(model.Telefono);

            if (model.Nombre == null)
                ModelState.AddModelError(nameof(model.Nombre), "Escribe el nombre de la sede.");
            else if (model.Nombre.Length > 100)
                ModelState.AddModelError(nameof(model.Nombre), "El nombre no puede pasar de 100 caracteres.");
            else if (await _context.Sedes.AnyAsync(s => s.Nombre.ToLower() == model.Nombre.ToLower() && s.IdSede != model.Id))
                ModelState.AddModelError(nameof(model.Nombre), "Ya hay una sede con ese nombre.");
            if (model.Direccion == null)
                ModelState.AddModelError(nameof(model.Direccion), "Escribe la dirección: sale en la nota de venta.");
            else if (model.Direccion.Length > 200)
                ModelState.AddModelError(nameof(model.Direccion), "La dirección no puede pasar de 200 caracteres.");
            if (model.Ciudad == null)
                ModelState.AddModelError(nameof(model.Ciudad), "Escribe la ciudad.");
            else if (model.Ciudad.Length > 80)
                ModelState.AddModelError(nameof(model.Ciudad), "La ciudad no puede pasar de 80 caracteres.");
            if (model.Telefono != null && !Telefono.IsMatch(model.Telefono))
                ModelState.AddModelError(nameof(model.Telefono), "Usa solo números y espacios (6 a 15), por ejemplo 066 312345.");
        }

        [NonAction]
        private async Task ValidarZona(ZonaFormViewModel model, int idSede)
        {
            model.Codigo = Limpiar(model.Codigo)?.ToUpperInvariant();
            model.Descripcion = Limpiar(model.Descripcion);

            if (model.Codigo == null)
                ModelState.AddModelError(nameof(model.Codigo), "Escribe el código de la zona.");
            else if (!CodigoZona.IsMatch(model.Codigo))
                ModelState.AddModelError(nameof(model.Codigo), "Usa letras, números y guiones, sin espacios (máximo 20). Ej.: A-06");
            else if (await _context.Ubicaciones.AnyAsync(u => u.IdSede == idSede && u.CodigoEstante == model.Codigo && u.IdUbicacion != model.Id))
                ModelState.AddModelError(nameof(model.Codigo), $"Esta sede ya tiene una zona {model.Codigo}.");
            if (model.Descripcion?.Length > 200)
                ModelState.AddModelError(nameof(model.Descripcion), "La descripción no puede pasar de 200 caracteres.");
            if (model.Tipo is not (TipoZona.Almacenaje or TipoZona.Recepcion))
                ModelState.AddModelError(nameof(model.Tipo), "Elige el tipo de zona.");
        }

        // Antes del script de zonas por sede, la BD no deja repetir un código de otra sede
        [NonAction]
        private async Task<bool> GuardarZona(ZonaFormViewModel model)
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                ModelState.AddModelError(nameof(model.Codigo),
                    $"Otra sede ya usa el código {model.Codigo}. Para repetir códigos entre sedes hay que correr el script Database/2026-09-28_zonas_por_sede.sql.");
                _context.ChangeTracker.Clear();
                return false;
            }
        }

        [NonAction]
        private async Task<bool> TieneHistorial(int idUbicacion) =>
            await _context.DetalleMovimientos.AnyAsync(d => d.IdUbicacion == idUbicacion)
            || await _context.DetalleAjustes.AnyAsync(d => d.IdUbicacion == idUbicacion)
            || await _context.DetalleTraslados.AnyAsync(d => d.IdUbicacionOrigen == idUbicacion || d.IdUbicacionDestino == idUbicacion)
            || await _context.Camaras.AnyAsync(c => c.IdUbicacion == idUbicacion);

        [NonAction]
        private async Task<ZonasSedeViewModel> ModeloZonas(int idSede, string sedeNombre, ZonaFormViewModel nueva)
        {
            var zonas = await _context.Ubicaciones
                .Where(u => u.IdSede == idSede)
                .OrderBy(u => u.Tipo == TipoZona.Recepcion ? 0 : 1)
                .ThenBy(u => u.CodigoEstante)
                .Select(u => new ZonaFila(
                    u.IdUbicacion,
                    u.CodigoEstante,
                    u.Descripcion,
                    u.Tipo == TipoZona.Recepcion,
                    u.PosX != null,
                    u.ProductoUbicacions.Sum(pu => pu.CantidadActual),
                    u.ProductoUbicacions.Count(pu => pu.CantidadActual > 0),
                    u.DetalleMovimientos.Any() || u.DetalleAjustes.Any() || u.Camaras.Any()
                        || u.DetalleTrasladoIdUbicacionOrigenNavigations.Any() || u.DetalleTrasladoIdUbicacionDestinoNavigations.Any()))
                .ToListAsync();

            nueva.SedeId = idSede;
            nueva.SedeNombre = sedeNombre;
            return new ZonasSedeViewModel { SedeId = idSede, SedeNombre = sedeNombre, Zonas = zonas, Nueva = nueva };
        }

        [NonAction]
        private async Task CargarSedes(NegocioFormViewModel model)
        {
            model.Sedes = await _context.Sedes
                .OrderBy(s => s.Nombre)
                .Select(s => new SedeFila(s.IdSede, s.Nombre, s.Direccion, s.Ciudad, s.Telefono, s.Ubicacions.Count, s.Usuarios.Count))
                .ToListAsync();
        }

        [NonAction]
        private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }
}
