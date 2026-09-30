using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.Services;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Data;

// Lo que el scaffold no genera: alias en español de dos DbSet que nombra distinto (Ubicacions / Proveedors),
// el control de concurrencia del stock y el aviso a las alertas.
// Vive en un archivo aparte a propósito: `dotnet ef dbcontext scaffold --force` no lo pisa.
public partial class AppDbContext
{
    public DbSet<Ubicacion> Ubicaciones => Set<Ubicacion>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        // Si cambia el stock, un producto (mínimo, vencimiento) o las sedes, las alertas se recalculan en la próxima página
        var tocaAlertas = ChangeTracker.Entries().Any(e =>
            (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            && e.Entity is ProductoUbicacion or Producto or Sede);
        var filas = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (tocaAlertas)
            AlertasStock.Invalidar();
        return filas;
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // El stock se actualiza leyendo y escribiendo la cantidad: si dos personas venden, reciben o mueven
        // el mismo producto de la misma zona a la vez, una pisaría el cambio de la otra. xmin (la versión de
        // la fila que PostgreSQL ya lleva) hace que el segundo guardado falle con DbUpdateConcurrencyException
        // en vez de dejar el stock mal.
        modelBuilder.Entity<ProductoUbicacion>()
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
