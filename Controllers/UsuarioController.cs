using System.Text.RegularExpressions;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Helpers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    // Cuentas del personal. No hay registro abierto en el login: solo la dueña crea cuentas,
    // y no se borran (quedan en el historial de movimientos): se desactivan.
    [Authorize(Roles = Roles.Duena)]
    public class UsuarioController : Controller
    {
        private readonly AppDbContext _context;

        public UsuarioController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var filas = await _context.Usuarios
                .OrderBy(u => u.Rol == Roles.Duena ? 0 : u.Rol == Roles.Encargada ? 1 : 2)
                .ThenBy(u => u.Estado == "activo" ? 0 : 1)
                .ThenBy(u => u.Nombre)
                .Select(u => new UsuarioFila(
                    u.IdUsuario,
                    u.Nombre,
                    u.Usuario1,
                    u.Rol,
                    u.IdSedeNavigation != null ? u.IdSedeNavigation.Nombre : null,
                    u.Estado == "activo",
                    u.FechaCreacion,
                    u.Movimientos.Max(m => (DateTime?)m.Fecha),
                    u.Rol == Roles.Duena))
                .ToListAsync();

            return View(new UsuarioListaViewModel { Filas = filas });
        }

        public async Task<IActionResult> Crear()
        {
            var model = new UsuarioFormViewModel { Rol = Roles.Trabajador };
            await CargarSedes(model);
            // Con una sola sede no hay nada que elegir
            if (model.Sedes.Count == 1)
                model.SedeId = model.Sedes[0].Id;
            return View("Formulario", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(UsuarioFormViewModel model)
        {
            model.Id = null;
            await Validar(model);
            foreach (var (campo, mensaje) in SesionHelper.ValidarContrasena(
                         model.Contrasena, model.ConfirmarContrasena, model.NombreUsuario,
                         nameof(model.Contrasena), nameof(model.ConfirmarContrasena)))
                ModelState.AddModelError(campo, mensaje);

            if (!ModelState.IsValid)
                return await Formulario(model);

            var usuario = new Usuario
            {
                Nombre = model.Nombre!,
                Usuario1 = model.NombreUsuario!,
                Rol = model.Rol!,
                IdSede = model.SedeId,
                Estado = model.Activo ? "activo" : "inactivo",
                Contrasena = BCrypt.Net.BCrypt.HashPassword(model.Contrasena),
                FechaCreacion = DateTime.Now
            };
            _context.Usuarios.Add(usuario);
            if (!await GuardarAsync())
                return await Formulario(model);

            TempData["Exito"] = $"Cuenta de {usuario.Nombre} creada. Entra con el usuario «{usuario.Usuario1}» y la contraseña que le asignaste; puede cambiarla desde Mi perfil.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Editar(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();
            if (usuario.Rol == Roles.Duena)
                return CuentaDuena();

            var model = new UsuarioFormViewModel
            {
                Id = usuario.IdUsuario,
                Nombre = usuario.Nombre,
                NombreUsuario = usuario.Usuario1,
                Rol = usuario.Rol,
                SedeId = usuario.IdSede,
                Activo = usuario.Estado == "activo"
            };
            await CargarSedes(model);
            return View("Formulario", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, UsuarioFormViewModel model)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();
            if (usuario.Rol == Roles.Duena)
                return CuentaDuena();

            model.Id = id;
            await Validar(model);
            if (!ModelState.IsValid)
                return await Formulario(model);

            var cambios = usuario.Rol != model.Rol || usuario.IdSede != model.SedeId || (usuario.Estado == "activo") != model.Activo;
            usuario.Nombre = model.Nombre!;
            usuario.Usuario1 = model.NombreUsuario!;
            usuario.Rol = model.Rol!;
            usuario.IdSede = model.SedeId;
            usuario.Estado = model.Activo ? "activo" : "inactivo";
            if (!await GuardarAsync())
                return await Formulario(model);

            TempData["Exito"] = !model.Activo
                ? $"Cuenta de {usuario.Nombre} desactivada: ya no puede entrar y su sesión abierta se cerró."
                : cambios
                    ? $"Cuenta de {usuario.Nombre} actualizada. Si tenía la sesión abierta, debe volver a entrar."
                    : $"Cuenta de {usuario.Nombre} actualizada.";
            return RedirectToAction(nameof(Index));
        }

        // Para cuando alguien olvida su contraseña: la dueña le asigna una nueva
        public async Task<IActionResult> Contrasena(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();
            if (usuario.Rol == Roles.Duena)
                return CuentaDuena();

            return View(new RestablecerContrasenaViewModel { Id = id, Nombre = usuario.Nombre, NombreUsuario = usuario.Usuario1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contrasena(int id, RestablecerContrasenaViewModel model)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();
            if (usuario.Rol == Roles.Duena)
                return CuentaDuena();

            foreach (var (campo, mensaje) in SesionHelper.ValidarContrasena(
                         model.Nueva, model.Confirmar, usuario.Usuario1, nameof(model.Nueva), nameof(model.Confirmar)))
                ModelState.AddModelError(campo, mensaje);

            // Las contraseñas no se devuelven a la vista
            if (!ModelState.IsValid)
                return View(new RestablecerContrasenaViewModel { Id = id, Nombre = usuario.Nombre, NombreUsuario = usuario.Usuario1 });

            usuario.Contrasena = BCrypt.Net.BCrypt.HashPassword(model.Nueva);
            await _context.SaveChangesAsync();

            TempData["Exito"] = $"Contraseña de {usuario.Nombre} restablecida. Si tenía una sesión abierta, se cerró.";
            return RedirectToAction(nameof(Index));
        }

        [NonAction]
        private IActionResult CuentaDuena()
        {
            TempData["Error"] = "La cuenta de la dueña se administra desde Mi perfil.";
            return RedirectToAction(nameof(Index));
        }

        [NonAction]
        private async Task Validar(UsuarioFormViewModel model)
        {
            model.Nombre = model.Nombre?.Trim();
            model.NombreUsuario = model.NombreUsuario?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(model.Nombre))
                ModelState.AddModelError(nameof(model.Nombre), "Escribe el nombre completo.");
            else if (model.Nombre.Length > 100)
                ModelState.AddModelError(nameof(model.Nombre), "El nombre no puede pasar de 100 caracteres.");

            if (string.IsNullOrEmpty(model.NombreUsuario))
                ModelState.AddModelError(nameof(model.NombreUsuario), "Escribe el nombre de usuario.");
            else if (!Regex.IsMatch(model.NombreUsuario, @"^[a-z0-9._-]{3,50}$"))
                ModelState.AddModelError(nameof(model.NombreUsuario), "Usa de 3 a 50 letras sin tildes, números, punto, guion o guion bajo; sin espacios.");
            else
            {
                var existente = await _context.Usuarios
                    .Where(u => u.Usuario1.ToLower() == model.NombreUsuario && u.IdUsuario != (model.Id ?? 0))
                    .Select(u => u.Nombre)
                    .FirstOrDefaultAsync();
                if (existente != null)
                    ModelState.AddModelError(nameof(model.NombreUsuario), $"Ese usuario ya lo usa {existente}. Elige otro.");
            }

            if (!Roles.EsAsignable(model.Rol))
                ModelState.AddModelError(nameof(model.Rol), "Elige el rol.");

            // Encargada y trabajador siempre tienen sede: sin sede la app los trataría como la dueña (todas las sedes)
            if (model.SedeId == null || !await _context.Sedes.AnyAsync(s => s.IdSede == model.SedeId))
                ModelState.AddModelError(nameof(model.SedeId), "Elige la sede donde trabaja.");
        }

        [NonAction]
        private async Task<IActionResult> Formulario(UsuarioFormViewModel model)
        {
            // Las contraseñas no se devuelven a la vista
            model.Contrasena = null;
            model.ConfirmarContrasena = null;
            await CargarSedes(model);
            return View("Formulario", model);
        }

        [NonAction]
        private async Task CargarSedes(UsuarioFormViewModel model)
        {
            model.Sedes = await _context.Sedes
                .OrderBy(s => s.Nombre)
                .Select(s => new SedeOpcion(s.IdSede, s.Nombre))
                .ToListAsync();
        }

        // Si otra persona tomó el mismo usuario entre la validación y el guardado, o la BD rechaza un dato
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
                ModelState.AddModelError(nameof(UsuarioFormViewModel.NombreUsuario), "Ese nombre de usuario ya está en uso. Elige otro.");
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23514" } pg)
            {
                ModelState.AddModelError("", $"La base de datos rechazó un dato ({pg.ConstraintName}). Avísale al administrador del sistema.");
            }
            _context.ChangeTracker.Clear();
            return false;
        }
    }
}
