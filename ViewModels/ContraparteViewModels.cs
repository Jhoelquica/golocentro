using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestionAlmacen_Golocentro.ViewModels
{
    // Clientes y proveedores tienen la misma forma (nombre, documento, contacto, dirección):
    // comparten vistas y solo cambian estos textos y la ruta.
    public class ContraparteTipo
    {
        public string Controlador { get; init; } = "";
        public string Plural { get; init; } = "";
        public string Singular { get; init; } = "";
        public string DocEtiqueta { get; init; } = "";
        public string DocAyuda { get; init; } = "";
        public int DocMaxLength { get; init; }
        public string Icono { get; init; } = "";
        public string MovimientoSingular { get; init; } = "";
        public string MovimientoPlural { get; init; } = "";

        public static readonly ContraparteTipo Cliente = new()
        {
            Controlador = "Cliente",
            Plural = "Clientes",
            Singular = "cliente",
            DocEtiqueta = "RUC / DNI",
            DocAyuda = "DNI de 8 dígitos o RUC de 11, solo números.",
            DocMaxLength = 11,
            Icono = "bi-people",
            MovimientoSingular = "salida",
            MovimientoPlural = "salidas"
        };

        public static readonly ContraparteTipo Proveedor = new()
        {
            Controlador = "Proveedor",
            Plural = "Proveedores",
            Singular = "proveedor",
            DocEtiqueta = "RUC",
            DocAyuda = "11 dígitos, solo números.",
            DocMaxLength = 11,
            Icono = "bi-truck",
            MovimientoSingular = "entrada",
            MovimientoPlural = "entradas"
        };
    }

    public record ContraparteFila(
        int Id,
        string Nombre,
        string Documento,
        string? Contacto,
        string? Direccion,
        int Movimientos,
        DateTime? UltimoMovimiento);

    public class ContraparteListaViewModel
    {
        public ContraparteTipo Tipo { get; set; } = null!;
        public string? Busqueda { get; set; }
        public int Pagina { get; set; }
        public int TotalPaginas { get; set; }
        public int Total { get; set; }
        public List<ContraparteFila> Filas { get; set; } = new();
    }

    public class ContraparteFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Ingresa el nombre o la razón social.")]
        [StringLength(150, ErrorMessage = "El nombre no puede pasar de 150 caracteres.")]
        public string Nombre { get; set; } = "";

        // Obligatorio y con formato distinto según el tipo (cliente o proveedor): lo valida cada controlador
        public string? Documento { get; set; }

        [StringLength(100, ErrorMessage = "El contacto no puede pasar de 100 caracteres.")]
        public string? Contacto { get; set; }

        [StringLength(200, ErrorMessage = "La dirección no puede pasar de 200 caracteres.")]
        public string? Direccion { get; set; }

        [BindNever, ValidateNever]
        public ContraparteTipo Tipo { get; set; } = null!;
    }
}
