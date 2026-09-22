using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public class MovimientoEntradaViewModel
    {
        public List<DetalleEntradaViewModel> Detalles { get; set; } = new List<DetalleEntradaViewModel>();

        [Required(ErrorMessage = "Debe seleccionar un proveedor")]
        [Display(Name = "Proveedor")]
        public int? ProveedorId { get; set; }

        [Display(Name = "Nº Factura / Guía")]
        public string? NumeroFactura { get; set; }

        [Display(Name = "Evidencia")]
        public IFormFile? Evidencia { get; set; }
    }
}