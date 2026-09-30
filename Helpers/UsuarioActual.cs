using System.Security.Claims;

namespace GestionAlmacen_Golocentro.Helpers;

public static class UsuarioActual
{
    // Sede del usuario que inició sesión; null para la dueña, que ve todas las sedes
    public static int? SedeId(this ClaimsPrincipal usuario)
    {
        var claim = usuario.FindFirst("SedeId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
