using System.ComponentModel.DataAnnotations;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public class DetalleEntradaViewModel
    {
        [Required(ErrorMessage = "Seleccione un producto")]
        public int ProductoId { get; set; }

        [Required(ErrorMessage = "Seleccione una ubicación")]
        public int UbicacionId { get; set; }

        [Required(ErrorMessage = "Ingrese la cantidad")]
        [Range(1, int.MaxValue, ErrorMessage = "Cantidad debe ser mayor a 0")]
        public int Cantidad { get; set; }
    }
}