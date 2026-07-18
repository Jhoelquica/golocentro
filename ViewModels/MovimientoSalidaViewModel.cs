using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public class MovimientoSalidaViewModel
    {
        public List<DetalleSalidaViewModel> Detalles { get; set; } = new List<DetalleSalidaViewModel>();

        [Display(Name = "Cliente")]
        public int? ClienteId { get; set; }

        [Display(Name = "Nº Pedido / Comprobante")]
        public string NumeroPedido { get; set; }

        [Display(Name = "Evidencia")]
        public IFormFile? Evidencia { get; set; }
    }
}