using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Services
{
    // Reglas de stock compartidas por Entrada y Venta.
    public static class OperacionesAlmacen
    {
        // La sede de un movimiento es la de sus ubicaciones (la dueña no tiene sede en el claim).
        // Todas deben ser de una misma sede y, si el usuario tiene sede asignada, de la suya.
        public static async Task<(int? SedeId, string? Error)> ResolverSedeMovimiento(
            AppDbContext context, IEnumerable<int> idsUbicacion, int? sedeUsuario)
        {
            var ids = idsUbicacion.Distinct().ToList();
            var ubicaciones = await context.Ubicaciones
                .Where(u => ids.Contains(u.IdUbicacion))
                .Select(u => new { u.IdUbicacion, u.IdSede })
                .ToListAsync();

            if (ubicaciones.Count != ids.Count)
                return (null, "Selecciona una ubicación válida para cada producto.");

            var sedes = ubicaciones.Select(u => u.IdSede).Distinct().ToList();
            if (sedes.Count > 1)
                return (null, "Todos los productos de un movimiento deben estar en ubicaciones de la misma sede. Registra un movimiento por cada sede.");

            if (sedeUsuario.HasValue && sedes[0] != sedeUsuario.Value)
                return (null, "Las ubicaciones seleccionadas no pertenecen a tu sede.");

            return (sedes[0], null);
        }

        // Stock actual (entidades rastreadas) de cada par producto+ubicación; los pares sin fila no aparecen.
        public static async Task<Dictionary<(int, int), ProductoUbicacion>> CargarStocks(
            AppDbContext context, IEnumerable<(int ProductoId, int UbicacionId)> pares)
        {
            var lista = pares.Distinct().ToList();
            var idsProducto = lista.Select(p => p.ProductoId).Distinct().ToList();
            var idsUbicacion = lista.Select(p => p.UbicacionId).Distinct().ToList();

            var filas = await context.ProductoUbicacions
                .Where(pu => idsProducto.Contains(pu.IdProducto) && idsUbicacion.Contains(pu.IdUbicacion))
                .ToListAsync();

            return filas.ToDictionary(pu => (pu.IdProducto, pu.IdUbicacion));
        }

        private static readonly string[] ExtensionesEvidencia = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxBytesEvidencia = 5 * 1024 * 1024;

        // Las evidencias se sirven desde wwwroot: solo se aceptan imágenes, para que nadie pueda
        // subir un .html o un ejecutable que la app luego publique como propio.
        public static string? ValidarEvidencia(IFormFile? archivo)
        {
            if (archivo == null || archivo.Length == 0)
                return null;

            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!ExtensionesEvidencia.Contains(extension) || !archivo.ContentType.StartsWith("image/"))
                return "La foto debe ser una imagen JPG, PNG o WEBP.";
            if (archivo.Length > MaxBytesEvidencia)
                return "La foto no debe pasar de 5 MB.";
            return null;
        }

        // Program.cs la fija con la carpeta wwwroot real: en IIS o en un servicio de Linux el directorio
        // actual del proceso no siempre es el de la aplicación.
        public static string CarpetaEvidencias { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "evidencias");

        // Llamar después de ValidarEvidencia. Se guarda con un nombre generado (sin el nombre original).
        public static async Task GuardarEvidencia(AppDbContext context, int movimientoId, IFormFile archivo, string tipoMovimiento)
        {
            var carpeta = CarpetaEvidencias;
            Directory.CreateDirectory(carpeta);

            var nombre = $"{Guid.NewGuid():N}{Path.GetExtension(archivo.FileName).ToLowerInvariant()}";
            await using (var stream = new FileStream(Path.Combine(carpeta, nombre), FileMode.CreateNew))
                await archivo.CopyToAsync(stream);

            context.Evidencia.Add(new Evidencium
            {
                IdMovimiento = movimientoId,
                Tipo = tipoMovimiento,
                UrlArchivo = $"/evidencias/{nombre}",
                Fecha = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }
}
