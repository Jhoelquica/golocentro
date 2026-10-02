using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Arma un controlador como lo recibiría una petición real: con el usuario de la sesión (los mismos datos
// que pone el login en la cookie) y con TempData para los mensajes de éxito o error.
public static class Sesion
{
    public static ClaimsPrincipal Usuario(int idUsuario, string rol, int? idSede) => new(new ClaimsIdentity(new[]
    {
        new Claim("UsuarioId", idUsuario.ToString()),
        new Claim(ClaimTypes.Role, rol),
        new Claim("SedeId", idSede?.ToString() ?? "")
    }, "Cookies"));

    public static T Preparar<T>(T controlador, ClaimsPrincipal usuario) where T : Controller
    {
        var http = new DefaultHttpContext { User = usuario };
        controlador.ControllerContext = new ControllerContext { HttpContext = http };
        controlador.TempData = new TempDataDictionary(http, new TempDataEnMemoria());
        return controlador;
    }

    private sealed class TempDataEnMemoria : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
