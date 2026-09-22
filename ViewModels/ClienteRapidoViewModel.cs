using System.ComponentModel.DataAnnotations;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public class ClienteRapidoViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; }

        public string? RucDni { get; set; }

        public string? Contacto { get; set; }

        public string? Direccion { get; set; }
    }
}