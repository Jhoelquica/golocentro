using GestionAlmacen_Golocentro.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Data;

// Alias en español de dos DbSet que el scaffold nombra distinto (Ubicacions / Proveedors).
// Vive en un archivo aparte a propósito: `dotnet ef dbcontext scaffold --force` no lo pisa.
public partial class AppDbContext
{
    public DbSet<Ubicacion> Ubicaciones => Set<Ubicacion>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
}
