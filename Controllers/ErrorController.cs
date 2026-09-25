using System.Diagnostics;
using GestionAlmacen_Golocentro.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GestionAlmacen_Golocentro.Controllers
{
    // Páginas de error para el usuario: "no encontrado" (404 y demás códigos sin contenido)
    // y fallas internas (500). El detalle técnico va al log, nunca a la pantalla.
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("Error/{codigo:int?}")]
        public IActionResult Index(int? codigo)
        {
            var codigoFinal = codigo ?? 500;
            // Código corto para dictarlo por teléfono; el log guarda también el identificador completo
            var traza = Activity.Current?.TraceId.ToHexString() ?? HttpContext.TraceIdentifier;
            var codigoSoporte = new string(traza.Where(char.IsLetterOrDigit).Take(8).ToArray()).ToUpperInvariant();

            var falla = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (falla?.Error != null)
            {
                codigoFinal = 500;
                _logger.LogError(falla.Error, "Error no controlado en {Ruta} (código {Codigo}, traza {Traza})", falla.Path, codigoSoporte, traza);
            }

            var original = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            Response.StatusCode = codigoFinal;
            return View(new ErrorViewModel
            {
                Codigo = codigoFinal,
                RequestId = codigoFinal >= 500 ? codigoSoporte : null,
                RutaOriginal = original?.OriginalPath ?? falla?.Path
            });
        }
    }
}
