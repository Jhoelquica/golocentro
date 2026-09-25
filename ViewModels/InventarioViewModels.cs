using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public static class MotivosAjuste
    {
        // Mismos valores que chk_ajusteinventario_motivo
        public static readonly (string Valor, string Texto)[] Todos =
        {
            ("conteo_inicial", "Conteo inicial"),
            ("conteo", "Conteo de rutina"),
            ("merma", "Merma o producto dañado"),
            ("correccion", "Corrección de error")
        };

        public static bool EsValido(string? valor) => Todos.Any(m => m.Valor == valor);

        public static string Texto(string valor) =>
            Todos.FirstOrDefault(m => m.Valor == valor).Texto ?? valor;
    }

    public record ZonaConteo(
        int Id,
        string Codigo,
        string? Descripcion,
        bool EsRecepcion,
        int Productos,
        int Unidades,
        DateTime? UltimoConteo);

    public record AjusteReciente(DateTime Fecha, string? Zona, string Motivo, string Usuario, int Contados, int Cambios);

    public class ConteoIndexViewModel
    {
        public int? SedeId { get; set; }
        public string SedeNombre { get; set; } = "";
        public bool PuedeElegirSede { get; set; }
        public List<SedeOpcion> Sedes { get; set; } = new();
        public List<ZonaConteo> Zonas { get; set; } = new();
        public List<AjusteReciente> Recientes { get; set; } = new();
    }

    public class LineaConteo
    {
        public int ProductoId { get; set; }

        // null = no se escribió nada; se valida en el controlador con un mensaje por producto
        public int? Cantidad { get; set; }
    }

    public record ProductoCatalogo(int Id, string Nombre, string Codigo);

    public class ConteoFormViewModel
    {
        public string? Motivo { get; set; }

        [StringLength(200, ErrorMessage = "La observación no puede pasar de 200 caracteres.")]
        public string? Observaciones { get; set; }

        // Las claves del diccionario (p{idProducto}) permiten quitar filas sin romper el enlace de datos
        public Dictionary<string, LineaConteo> Lineas { get; set; } = new();

        [BindNever, ValidateNever]
        public ConteoDatos Datos { get; set; } = new();
    }

    public class ConteoDatos
    {
        public int ZonaId { get; set; }
        public string ZonaCodigo { get; set; } = "";
        public string? ZonaDescripcion { get; set; }
        public bool EsRecepcion { get; set; }
        public string SedeNombre { get; set; } = "";
        public int SedeId { get; set; }
        public bool YaContada { get; set; }
        public List<ProductoCatalogo> Catalogo { get; set; } = new();

        // Cantidad que el sistema tiene hoy en la zona, por producto (solo los que tienen stock)
        public Dictionary<int, int> EnSistema { get; set; } = new();
    }
}
