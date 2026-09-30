using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Helpers;
using GestionAlmacen_Golocentro.Services;

var builder = WebApplication.CreateBuilder(args);

// Montos, fechas y números siempre como en Perú, aunque el servidor esté en inglés
CultureInfo.DefaultThreadCurrentCulture = Cultura.Peru;
CultureInfo.DefaultThreadCurrentUICulture = Cultura.Peru;

// Configurar Entity Framework con PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Autenticación por cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        // En cada página se revisa la cuenta: si la dueña la desactivó, le cambió el rol o la sede,
        // o se cambió la contraseña, la sesión abierta se cierra y hay que volver a entrar.
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async contexto =>
            {
                var principal = contexto.Principal!;
                if (!int.TryParse(principal.FindFirst("UsuarioId")?.Value, out var idUsuario))
                {
                    contexto.RejectPrincipal();
                    return;
                }

                var db = contexto.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var cuenta = await db.Usuarios
                    .Where(u => u.IdUsuario == idUsuario)
                    .Select(u => new { u.Estado, u.Rol, u.IdSede, u.Contrasena })
                    .FirstOrDefaultAsync();

                var vigente = cuenta != null
                    && cuenta.Estado == "activo"
                    && cuenta.Rol == principal.FindFirst(ClaimTypes.Role)?.Value
                    && (cuenta.IdSede?.ToString() ?? "") == (principal.FindFirst("SedeId")?.Value ?? "")
                    && SesionHelper.Sello(cuenta.Contrasena) == principal.FindFirst(SesionHelper.ClaimSello)?.Value;
                if (!vigente)
                {
                    contexto.RejectPrincipal();
                    await contexto.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

// Iniciar sesión: como mucho 10 intentos por minuto desde un mismo equipo (frena a quien prueba contraseñas).
// Al pasarse, la página de error explica que hay que esperar un minuto.
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy(AccountController.LimiteIngreso, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllersWithViews();

// Las llaves que firman la sesión y los formularios. En el servidor van en una carpeta fija
// (Golocentro__CarpetaLlaves): si no, al reiniciar el servicio se cerraría la sesión de todos.
var carpetaLlaves = builder.Configuration["Golocentro:CarpetaLlaves"];
if (!string.IsNullOrWhiteSpace(carpetaLlaves))
    builder.Services.AddDataProtection()
        .SetApplicationName("Golocentro")
        .PersistKeysToFileSystem(new DirectoryInfo(carpetaLlaves));

var app = builder.Build();

OperacionesAlmacen.CarpetaEvidencias = Path.Combine(app.Environment.WebRootPath, "evidencias");

// Detrás de Nginx (u otro proxy en el mismo servidor): la IP real del cliente y si entró por https
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// En desarrollo se ve la página técnica del error; en producción, la página amable (el detalle va al log)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Cabeceras de seguridad básicas: el navegador no "adivina" el tipo de un archivo subido, ninguna otra web
// puede mostrar el sistema dentro de un marco y los enlaces externos no reciben las rutas internas.
app.Use(async (contexto, siguiente) =>
{
    var cabeceras = contexto.Response.Headers;
    cabeceras.XContentTypeOptions = "nosniff";
    cabeceras.XFrameOptions = "SAMEORIGIN";
    cabeceras["Referrer-Policy"] = "same-origin";
    await siguiente();
});

// Respuestas sin contenido (404 de una nota que no existe, etc.) muestran la página de error en vez de quedar en blanco
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(Cultura.Peru),
    SupportedCultures = new List<CultureInfo> { Cultura.Peru },
    SupportedUICultures = new List<CultureInfo> { Cultura.Peru },
    RequestCultureProviders = new List<IRequestCultureProvider>()
});

// Las fotos de evidencia y de perfil son del negocio: solo se ven con la sesión iniciada
app.Use(async (contexto, siguiente) =>
{
    var ruta = contexto.Request.Path;
    if (ruta.StartsWithSegments("/evidencias") || ruta.StartsWithSegments("/uploads"))
    {
        var sesion = await contexto.AuthenticateAsync();
        if (!sesion.Succeeded)
        {
            await contexto.ChallengeAsync();
            return;
        }
    }
    await siguiente();
});

// Imágenes y librerías casi no cambian: el navegador las guarda 30 días y no vuelve a pedirlas.
// Las fotos subidas (evidencias, perfiles) tienen nombre único; se guardan solo en ese navegador.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = contexto =>
    {
        var ruta = contexto.Context.Request.Path.Value ?? "";
        if (ruta.StartsWith("/img/") || ruta.StartsWith("/lib/"))
            contexto.Context.Response.Headers.CacheControl = "public,max-age=2592000";
        else if (ruta.StartsWith("/evidencias/") || ruta.StartsWith("/uploads/"))
            contexto.Context.Response.Headers.CacheControl = "private,max-age=604800";
    }
});
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
