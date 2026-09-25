using System.ComponentModel.DataAnnotations;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public class ClienteRapidoViewModel
    {
        [Required(ErrorMessage = "Ingresa el nombre del cliente.")]
        [StringLength(150, ErrorMessage = "El nombre no puede pasar de 150 caracteres.")]
        public string? Nombre { get; set; }

        // Misma regla que el módulo de Clientes (chk_cliente_rucdni pide 8 a 11 caracteres)
        [Required(ErrorMessage = "Ingresa el DNI o RUC.")]
        [RegularExpression(@"^\d{8,11}$", ErrorMessage = "El DNI o RUC debe tener entre 8 y 11 dígitos.")]
        public string? RucDni { get; set; }

        [RegularExpression(@"^9\d{8}$", ErrorMessage = "El celular debe tener 9 dígitos y empezar con 9.")]
        public string? Celular { get; set; }
    }
}
