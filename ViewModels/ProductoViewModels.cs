using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestionAlmacen_Golocentro.ViewModels
{
    // Stock = suma de ProductoUbicacion.CantidadActual en la sede del usuario (o en todas, para la dueña)
    public record ProductoFila(
        int Id,
        string Nombre,
        string Codigo,
        string Tipo,
        string UnidadMedida,
        decimal Precio,
        int StockMinimo,
        int Stock,
        DateOnly? FechaVencimiento,
        bool TieneHistorial);

    public class ProductoListaViewModel
    {
        public string? Busqueda { get; set; }
        public string? Tipo { get; set; }
        public List<string> Tipos { get; set; } = new();
        public int Pagina { get; set; }
        public int TotalPaginas { get; set; }
        public int Total { get; set; }
        public bool PuedeEditar { get; set; }
        public List<ProductoFila> Filas { get; set; } = new();

        // Presentaciones (bolsa, caja...) de los productos de la página, con su precio
        public Dictionary<int, List<PresentacionVenta>> Presentaciones { get; set; } = new();
    }

    public class ProductoFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Ingresa el nombre del producto.")]
        [StringLength(150, ErrorMessage = "El nombre no puede pasar de 150 caracteres.")]
        public string? Nombre { get; set; }

        [Required(ErrorMessage = "Ingresa el código del producto.")]
        [StringLength(50, ErrorMessage = "El código no puede pasar de 50 caracteres.")]
        public string? Codigo { get; set; }

        [Required(ErrorMessage = "Ingresa el tipo o categoría.")]
        [StringLength(50, ErrorMessage = "El tipo no puede pasar de 50 caracteres.")]
        public string? Tipo { get; set; }

        [Required(ErrorMessage = "Ingresa la unidad base (tira, pack, unidad...).")]
        [StringLength(20, ErrorMessage = "La unidad no puede pasar de 20 caracteres.")]
        public string? UnidadMedida { get; set; }

        [Required(ErrorMessage = "Ingresa el precio.")]
        [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "El precio no puede ser negativo.")]
        public decimal? PrecioUnitario { get; set; }

        [Required(ErrorMessage = "Ingresa el stock mínimo (0 si no aplica).")]
        [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
        public int? StockMinimo { get; set; }

        public DateOnly? FechaVencimiento { get; set; }

        [StringLength(50, ErrorMessage = "El lote no puede pasar de 50 caracteres.")]
        public string? Lote { get; set; }

        // El vencimiento sale de las entradas (el más próximo de lo que queda en stock): se muestra sin editar
        [BindNever, ValidateNever]
        public bool VencimientoPorEntradas { get; set; }

        // Presentaciones más grandes que la unidad base (bolsa = 8 tiras, caja = 12 packs), cada una con su precio
        public List<PresentacionFormViewModel> Presentaciones { get; set; } = new();

        // "conteo" o "entrada" cuando se abre en otra pestaña desde esas pantallas
        public string? Desde { get; set; }

        [BindNever, ValidateNever]
        public List<string> TiposExistentes { get; set; } = new();

        [BindNever, ValidateNever]
        public List<string> UnidadesExistentes { get; set; } = new();
    }

    // Una fila de presentación del formulario de producto. Id vacío = nueva.
    public class PresentacionFormViewModel
    {
        public int? Id { get; set; }
        public string? Nombre { get; set; }
        public int? Factor { get; set; }
        public decimal? Precio { get; set; }
    }
}
