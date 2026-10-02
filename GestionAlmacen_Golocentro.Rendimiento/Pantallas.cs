using System.Security.Claims;
using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GestionAlmacen_Golocentro.Rendimiento;

// Pantallas de lista y de reporte, llamadas como una petición real de la dueña (ve todas las sedes)
public sealed record Pantalla(string Nombre, string Accion, bool Principal, Func<AppDbContext, Task<IActionResult>> Cargar);

public static class Pantallas
{
    public static List<Pantalla> Todas(int productoKardex)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var hace3Meses = hoy.AddDays(-92);
        return new()
        {
            new("Ventas · lista (hoy, como abre)", "VentaController.Index", true, db => Preparar(new VentaController(db)).Index(null, null, null)),
            new("Ventas · lista (últimos 3 meses)", "VentaController.Index", false, db => Preparar(new VentaController(db)).Index(hace3Meses, hoy, null)),
            new("Movimientos · lista (3 meses)", "MovimientoController.Index", true, db => Preparar(new MovimientoController(db)).Index(null, hace3Meses, hoy)),
            new("Productos · catálogo", "ProductoController.Index", false, db => Preparar(new ProductoController(db)).Index(null, null)),
            new("Stock · consulta", "ProductoController.Stock", true, db => Preparar(new ProductoController(db)).Stock(null, null, null)),
            new("Alertas", "AlertaController.Index", true, db => Preparar(new AlertaController(db)).Index()),
            new("Dashboard", "HomeController.Index", true, db => Preparar(new HomeController(db)).Index()),
            new("Reportes · inicio (reporte principal)", "ReporteController.Index", true, db => Preparar(new ReporteController(db)).Index()),
            new("Reporte de ventas (mes)", "ReporteController.Ventas", false, db => Preparar(new ReporteController(db)).Ventas(null, null, null, null)),
            new("Reporte de productos vendidos (mes)", "ReporteController.Productos", false, db => Preparar(new ReporteController(db)).Productos(null, null, null, null)),
            new("Reporte de entradas (mes)", "ReporteController.Entradas", false, db => Preparar(new ReporteController(db)).Entradas(null, null, null, null)),
            new("Reporte de stock", "ReporteController.Stock", false, db => Preparar(new ReporteController(db)).Stock(null, false, null)),
            new("Kardex (producto con más movimientos)", "ReporteController.Kardex", false, db => Preparar(new ReporteController(db)).Kardex(productoKardex, null, null, null, null)),
            new("Mapa del almacén", "MapaController.Index", false, db => Preparar(new MapaController(db)).Index(null)),
            new("Conteo · lista de zonas", "InventarioController.Index", false, db => Preparar(new InventarioController(db)).Index(null)),
        };
    }

    private static T Preparar<T>(T controlador) where T : Controller
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("UsuarioId", "1"), new Claim(ClaimTypes.Role, "duena"), new Claim("SedeId", ""),
                new Claim("NombreCompleto", "Dueña de prueba")
            }, "Cookies"))
        };
        controlador.ControllerContext = new ControllerContext { HttpContext = http };
        controlador.TempData = new TempDataDictionary(http, new SinTempData());
        controlador.Url = new UrlSimple(controlador.ControllerContext);
        return controlador;
    }

    private sealed class SinTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    // Arma enlaces simples sin el enrutador (algunas listas guardan el enlace de cada fila)
    private sealed class UrlSimple : IUrlHelper
    {
        public UrlSimple(ActionContext contexto) => ActionContext = contexto;
        public ActionContext ActionContext { get; }
        public string? Action(UrlActionContext a) => $"/{a.Controller}/{a.Action}";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/";
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }
}
