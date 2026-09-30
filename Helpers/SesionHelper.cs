using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GestionAlmacen_Golocentro.Models;

namespace GestionAlmacen_Golocentro.Helpers;

// Datos de la sesión (cookie) y reglas de contraseña compartidas por el login, el perfil y el módulo de usuarios.
public static class SesionHelper
{
    public const string ClaimSello = "SelloClave";
    public const int MinimoContrasena = 8;

    // BCrypt solo usa los primeros 72 bytes
    public const int MaximoContrasena = 72;

    // Huella corta del hash de la contraseña: si la contraseña cambia, las sesiones abiertas dejan de valer.
    // No revela nada de la contraseña (es un hash del hash).
    public static string Sello(string hashContrasena) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashContrasena)))[..16];

    public static List<Claim> Claims(Usuario usuario, Sede? sede) => new()
    {
        new Claim(ClaimTypes.Name, usuario.Usuario1),
        new Claim(ClaimTypes.Role, usuario.Rol),
        new Claim("UsuarioId", usuario.IdUsuario.ToString()),
        new Claim("NombreCompleto", usuario.Nombre),
        new Claim("FotoUrl", usuario.FotoUrl ?? ""),
        // La dueña no tiene sede: ve todas
        new Claim("SedeId", sede?.IdSede.ToString() ?? ""),
        new Claim("SedeNombre", sede?.Nombre ?? "Todas las sedes"),
        new Claim(ClaimSello, Sello(usuario.Contrasena))
    };

    // Errores por campo (nombre del campo, mensaje); vacío si la contraseña sirve
    public static List<(string Campo, string Mensaje)> ValidarContrasena(
        string? nueva, string? confirmar, string? nombreUsuario, string campoNueva, string campoConfirmar)
    {
        var errores = new List<(string, string)>();
        if (string.IsNullOrEmpty(nueva))
            errores.Add((campoNueva, "Escribe la contraseña."));
        else if (nueva.Length < MinimoContrasena)
            errores.Add((campoNueva, $"La contraseña debe tener al menos {MinimoContrasena} caracteres."));
        else if (Encoding.UTF8.GetByteCount(nueva) > MaximoContrasena)
            errores.Add((campoNueva, "La contraseña es demasiado larga."));
        else if (!string.IsNullOrEmpty(nombreUsuario) && nueva.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase))
            errores.Add((campoNueva, "La contraseña no puede ser igual al nombre de usuario."));

        if (!string.IsNullOrEmpty(nueva) && nueva != confirmar)
            errores.Add((campoConfirmar, "Las dos contraseñas no coinciden."));
        return errores;
    }
}
