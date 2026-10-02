using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Datos mínimos de un negocio de prueba (inventados, no son los del negocio real):
// dos sedes, tres zonas, la dueña, un trabajador por sede, el cliente "Público en general" y dos productos con stock.
public static class Semilla
{
    public record Datos(
        int SedePrincipal, int SedeNorte,
        int ZonaA01, int ZonaA02, int ZonaN01,
        int Duena, int TrabajadorPrincipal, int TrabajadorNorte,
        int ClienteGeneral,
        int Caramelo, int Chocolate);

    public const int StockInicialCaramelo = 10;
    public const int StockInicialChocolate = 20;

    public static async Task<Datos> CrearAsync(AppDbContext db)
    {
        var principal = new Sede { Nombre = "Sede Principal", Direccion = "Jr. Prueba 123", Ciudad = "Huamanga", Estado = "activo" };
        var norte = new Sede { Nombre = "Sede Norte", Direccion = "Av. Prueba 456", Ciudad = "Huamanga", Estado = "activo" };
        db.Sedes.AddRange(principal, norte);
        await db.SaveChangesAsync();

        var a01 = new Ubicacion { CodigoEstante = "A-01", IdSede = principal.IdSede, Tipo = "almacenaje" };
        var a02 = new Ubicacion { CodigoEstante = "A-02", IdSede = principal.IdSede, Tipo = "almacenaje" };
        var n01 = new Ubicacion { CodigoEstante = "N-01", IdSede = norte.IdSede, Tipo = "almacenaje" };
        db.Ubicaciones.AddRange(a01, a02, n01);

        // El campo contrasena guarda un hash de BCrypt; aquí va un texto cualquiera porque nadie inicia sesión
        var duena = Usuario("Dueña de prueba", "duena", "duena.prueba", null);
        var trabajador = Usuario("Trabajador Principal", "trabajador", "trabajador.principal", principal.IdSede);
        var trabajadorNorte = Usuario("Trabajador Norte", "trabajador", "trabajador.norte", norte.IdSede);
        db.Usuarios.AddRange(duena, trabajador, trabajadorNorte);

        var general = new Cliente { Nombre = "Público en general", RucDni = ClienteGeneral.RucDni };
        db.Clientes.Add(general);

        var caramelo = new Producto
        {
            Nombre = "Caramelo Fresa", Tipo = "Caramelo", Codigo = "PRD-001", UnidadMedida = "unidad",
            PrecioUnitario = 0.30m, StockMinimo = 5
        };
        var chocolate = new Producto
        {
            Nombre = "Chocolate Sublime", Tipo = "Chocolate", Codigo = "PRD-002", UnidadMedida = "unidad",
            PrecioUnitario = 1.50m, StockMinimo = 2
        };
        db.Productos.AddRange(caramelo, chocolate);
        await db.SaveChangesAsync();

        db.ProductoUbicacions.AddRange(
            new ProductoUbicacion { IdProducto = caramelo.IdProducto, IdUbicacion = a01.IdUbicacion, CantidadActual = StockInicialCaramelo, UltimaActualizacion = DateTime.Now },
            new ProductoUbicacion { IdProducto = chocolate.IdProducto, IdUbicacion = a02.IdUbicacion, CantidadActual = StockInicialChocolate, UltimaActualizacion = DateTime.Now });
        await db.SaveChangesAsync();

        return new Datos(
            principal.IdSede, norte.IdSede,
            a01.IdUbicacion, a02.IdUbicacion, n01.IdUbicacion,
            duena.IdUsuario, trabajador.IdUsuario, trabajadorNorte.IdUsuario,
            general.IdCliente,
            caramelo.IdProducto, chocolate.IdProducto);
    }

    private static Usuario Usuario(string nombre, string rol, string usuario, int? idSede) => new()
    {
        Nombre = nombre, Rol = rol, Usuario1 = usuario, Contrasena = "sin-contrasena-real", Estado = "activo",
        IdSede = idSede, FechaCreacion = DateTime.Now
    };
}
