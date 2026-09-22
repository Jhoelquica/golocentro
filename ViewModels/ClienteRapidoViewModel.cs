using System.ComponentModel.DataAnnotations;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public class ClienteRapidoViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El RUC/DNI es obligatorio")]
        [StringLength(11, MinimumLength = 8, ErrorMessage = "El RUC/DNI debe tener entre 8 y 11 caracteres")]
        public string RucDni { get; set; }

        public string? Contacto { get; set; }

        public string? Direccion { get; set; }
    }
}