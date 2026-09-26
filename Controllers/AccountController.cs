using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using GestionAlmacen_Golocentro.Helpers;
using GestionAlmacen_Golocentro.ViewModels;

namespace GestionAlmacen_Golocentro.Controllers
{
    public class AccountController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public AccountController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ── PERFIL ──────────────────────────────────────────
        [Authorize]
        public async Task<IActionResult> Perfil()
        {
            var idUsuarioClaim = User.FindFirst("UsuarioId")?.Value;
            if (string.IsNullOrEmpty(idUsuarioClaim))
                return RedirectToAction("Login", "Account");

            var idUsuario = int.Parse(idUsuarioClaim);
            var usuario = await _context.Usuarios
                .Include(u => u.IdSedeNavigation)
                .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
            if (usuario == null) return NotFound();
            return View(usuario);
        }

        // ── CAMBIAR FOTO (GET) ───────────────────────────────
        [Authorize]
        public IActionResult CambiarFoto()
        {
            return View();
        }

        // ── CAMBIAR FOTO (POST) ──────────────────────────────
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarFoto(IFormFile foto)
        {
            if (foto == null || foto.Length == 0)
            {
                TempData["Error"] = "Debes seleccionar una imagen.";
                return View();
            }

            // Validar tipo de archivo
            var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(foto.FileName).ToLowerInvariant();
            if (!extensionesPermitidas.Contains(extension))
            {
                TempData["Error"] = "Solo se permiten imágenes JPG, PNG o WEBP.";
                return View();
            }

            // Validar tamaño máximo (2 MB)
            if (foto.Length > 2 * 1024 * 1024)
            {
                TempData["Error"] = "La imagen no debe superar 2 MB.";
                return View();
            }

            var idUsuarioClaim = User.FindFirst("UsuarioId")?.Value;
            if (string.IsNullOrEmpty(idUsuarioClaim))
                return RedirectToAction("Login", "Account");

            var idUsuario = int.Parse(idUsuarioClaim);
            var usuario = await _context.Usuarios.FindAsync(idUsuario);
            if (usuario == null) return NotFound();

            // Crear carpeta si no existe
            var carpeta = Path.Combine(_env.WebRootPath, "uploads", "perfiles");
            if (!Directory.Exists(carpeta))
                Directory.CreateDirectory(carpeta);

            // Eliminar foto anterior si existe
            if (!string.IsNullOrEmpty(usuario.FotoUrl))
            {
                var fotoAnterior = Path.Combine(carpeta, usuario.FotoUrl);
                if (System.IO.File.Exists(fotoAnterior))
                    System.IO.File.Delete(fotoAnterior);
            }

            // Guardar nueva foto con nombre único
            var nombreArchivo = $"user_{idUsuario}_{Guid.NewGuid():N}{extension}";
            var rutaCompleta = Path.Combine(carpeta, nombreArchivo);

            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                await foto.CopyToAsync(stream);

            // Actualizar BD
            usuario.FotoUrl = nombreArchivo;
            await _context.SaveChangesAsync();

            // Actualizar el claim FotoUrl en la cookie actual
            // para que el layout muestre la nueva foto sin re-login
            var identity = (ClaimsIdentity)User.Identity!;
            var claimFoto = identity.FindFirst("FotoUrl");
            if (claimFoto != null) identity.RemoveClaim(claimFoto);
            identity.AddClaim(new Claim("FotoUrl", nombreArchivo));

            // Re-emitir cookie de autenticación con el claim actualizado
            await HttpContext.SignInAsync(
                "Cookies",
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true }
            );

            TempData["Success"] = "Foto actualizada correctamente.";
            return RedirectToAction(nameof(Perfil));
        }


        private readonly AppDbContext _context;


        // GET: Account/Login
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string nombreUsuario, string contraseña, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["NombreUsuario"] = nombreUsuario;

            if (string.IsNullOrEmpty(nombreUsuario) || string.IsNullOrEmpty(contraseña))
            {
                ViewBag.Error = "Debe ingresar usuario y contraseña.";
                return View();
            }

            // Buscar usuario en la base de datos
            // OJO: la propiedad se llama "Usuario1", no "NombreUsuario"
            // Primero buscamos solo por usuario (no por contraseña, porque ahora está hasheada).
            // Sin distinguir mayúsculas: los usuarios nuevos se guardan en minúsculas.
            var nombreBuscado = nombreUsuario.Trim().ToLower();
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Usuario1.ToLower() == nombreBuscado);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(contraseña, usuario.Contrasena))
            {
                ViewBag.Error = "Usuario o contraseña incorrectos.";
                return View();
            }

            if (usuario.Estado != "activo")
            {
                ViewBag.Error = "Tu cuenta está inactiva. Comunícate con la administración.";
                return View();
            }

            await IniciarSesion(usuario);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            // Todos los roles empiezan en el dashboard (tiene los accesos a vender, entradas y stock)
            return RedirectToAction("Index", "Home");
        }

        // La sede y el sello de la contraseña van en la cookie; Program.cs los revisa en cada página
        [NonAction]
        private async Task IniciarSesion(Usuario usuario)
        {
            var sede = usuario.IdSede.HasValue ? await _context.Sedes.FindAsync(usuario.IdSede.Value) : null;
            var identidad = new ClaimsIdentity(SesionHelper.Claims(usuario, sede), CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidad),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
        }

        // ── CAMBIAR MI CONTRASEÑA ───────────────────────────
        [Authorize]
        public IActionResult CambiarContrasena() => View(new CambiarContrasenaViewModel());

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(CambiarContrasenaViewModel model)
        {
            if (!int.TryParse(User.FindFirst("UsuarioId")?.Value, out var idUsuario))
                return RedirectToAction(nameof(Login));
            var usuario = await _context.Usuarios.FindAsync(idUsuario);
            if (usuario == null)
                return RedirectToAction(nameof(Login));

            if (string.IsNullOrEmpty(model.Actual))
                ModelState.AddModelError(nameof(model.Actual), "Escribe tu contraseña actual.");
            else if (!BCrypt.Net.BCrypt.Verify(model.Actual, usuario.Contrasena))
                ModelState.AddModelError(nameof(model.Actual), "La contraseña actual no es correcta.");

            foreach (var (campo, mensaje) in SesionHelper.ValidarContrasena(model.Nueva, model.Confirmar, usuario.Usuario1, nameof(model.Nueva), nameof(model.Confirmar)))
                ModelState.AddModelError(campo, mensaje);
            if (ModelState.IsValid && model.Nueva == model.Actual)
                ModelState.AddModelError(nameof(model.Nueva), "La nueva contraseña debe ser distinta de la actual.");

            if (!ModelState.IsValid)
                return View(new CambiarContrasenaViewModel());

            usuario.Contrasena = BCrypt.Net.BCrypt.HashPassword(model.Nueva);
            await _context.SaveChangesAsync();

            // Con la contraseña nueva cambia el sello: se renueva esta sesión y las demás quedan cerradas
            await IniciarSesion(usuario);
            TempData["Success"] = "Contraseña cambiada. Las sesiones abiertas en otros equipos se cerraron.";
            return RedirectToAction(nameof(Perfil));
        }

        // GET: Account/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // GET: Account/AccessDenied
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}