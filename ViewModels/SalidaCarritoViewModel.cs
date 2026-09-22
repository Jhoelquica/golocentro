using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public class SalidaCarritoViewModel
    {
        public List<ItemSalidaViewModel> Items { get; set; } = new List<ItemSalidaViewModel>();

        [Required(ErrorMessage = "Seleccione un cliente")]
        [Display(Name = "Cliente")]
        public int? ClienteId { get; set; }

        [Display(Name = "Nº Comprobante")]
        public string? NumeroComprobante { get; set; }

        [Display(Name = "Evidencia")]
        public IFormFile? Evidencia { get; set; }

        public decimal TotalGeneral { get; set; }
    }

    public class ItemSalidaViewModel
    {
        [Required]
        public int ProductoId { get; set; }

        [Required]
        public int UbicacionId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Cantidad debe ser mayor a 0")]
        public int Cantidad { get; set; }

        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Cantidad * PrecioUnitario;
        public string NombreProducto { get; set; }
        public string? CodigoUbicacion { get; set; }
        public int StockDisponible { get; set; }
    }
}