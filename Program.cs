using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Helpers;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllersWithViews();

var app = builder.Build();

// En desarrollo se ve la página técnica del error; en producción, la página amable (el detalle va al log)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Respuestas sin contenido (404 de una nota que no existe, etc.) muestran la página de error en vez de quedar en blanco
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
