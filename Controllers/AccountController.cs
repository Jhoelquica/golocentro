using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

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

            if (string.IsNullOrEmpty(nombreUsuario) || string.IsNullOrEmpty(contraseña))
            {
                ViewBag.Error = "Debe ingresar usuario y contraseña.";
                return View();
            }

            // Buscar usuario en la base de datos
            // OJO: la propiedad se llama "Usuario1", no "NombreUsuario"
            // Primero buscamos solo por usuario (no por contraseña, porque ahora está hasheada)
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Usuario1 == nombreUsuario);

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

            // Obtener la sede del usuario (si tiene id_sede)
            Sede sede = null;
            if (usuario.IdSede.HasValue)
            {
                sede = await _context.Sedes.FindAsync(usuario.IdSede.Value);
            }

            // Crear claims base
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, usuario.Usuario1),  // nombre de usuario
        new Claim(ClaimTypes.Role, usuario.Rol ?? "trabajador"),
        new Claim("UsuarioId", usuario.IdUsuario.ToString()),
        // Como solo hay "Nombre", usamos eso para el nombre completo
        new Claim("NombreCompleto", usuario.Nombre),
        // FotoUrl puede ser null, así que usamos ?? para evitar problemas
        new Claim("FotoUrl", usuario.FotoUrl ?? "")
    };

            // Agregar claims de sede
            if (sede != null)
            {
                claims.Add(new Claim("SedeId", sede.IdSede.ToString()));
                claims.Add(new Claim("SedeNombre", sede.Nombre));
            }
            else
            {
                // duena (id_sede NULL)
                claims.Add(new Claim("SedeId", ""));
                claims.Add(new Claim("SedeNombre", "Todas las sedes"));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            // Redirigir según el rol
            if (usuario.Rol == "duena" || usuario.Rol == "encargada")
                return RedirectToAction("Index", "Home");
            else
                return RedirectToAction("Index", "Movimiento");
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