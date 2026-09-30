using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public record SedeOpcion(int Id, string Nombre);

    public record StockEnZona(int ProductoId, int Cantidad);

    // Posición y tamaño en "cuadritos" del plano; null si la zona aún no está dibujada
    public record ZonaMapa(
        int Id,
        string Codigo,
        string? Descripcion,
        bool EsRecepcion,
        decimal? X,
        decimal? Y,
        decimal? Ancho,
        decimal? Alto,
        List<StockEnZona> Stock);

    public record LineaMapa(string Tipo, decimal X1, decimal Y1, decimal X2, decimal Y2);

    public record ProductoMapa(int Id, string Nombre, string Codigo, int StockTotal, int StockMinimo);

    public class MapaViewModel
    {
        public int? SedeId { get; set; }
        public string SedeNombre { get; set; } = "";
        public bool PuedeElegirSede { get; set; }
        public List<SedeOpcion> Sedes { get; set; } = new();
        public decimal Ancho { get; set; }
        public decimal Alto { get; set; }
        public List<ZonaMapa> Zonas { get; set; } = new();
        public List<LineaMapa> Lineas { get; set; } = new();
        public List<ProductoMapa> Productos { get; set; } = new();
    }

    public record TrasladoReciente(DateTime Fecha, string Producto, int Cantidad, string Origen, string Destino, string Usuario);

    public class MoverFormViewModel
    {
        [Required(ErrorMessage = "Elige el producto que vas a mover.")]
        public int? ProductoId { get; set; }

        [Required(ErrorMessage = "Elige desde qué zona lo sacas.")]
        public int? OrigenId { get; set; }

        [Required(ErrorMessage = "Elige a qué zona lo llevas.")]
        public int? DestinoId { get; set; }

        [Required(ErrorMessage = "Ingresa la cantidad.")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que 0.")]
        public int? Cantidad { get; set; }

        [StringLength(200, ErrorMessage = "La observación no puede pasar de 200 caracteres.")]
        public string? Observaciones { get; set; }

        public int? Sede { get; set; }

        [BindNever, ValidateNever]
        public MoverDatos Datos { get; set; } = new();
    }

    // Lo que la vista necesita para armar los selectores; lo llena el controlador
    public class MoverDatos
    {
        public string SedeNombre { get; set; } = "";
        public bool PuedeElegirSede { get; set; }
        public List<SedeOpcion> Sedes { get; set; } = new();
        public List<ProductoMapa> Productos { get; set; } = new();
        public List<ZonaMapa> Zonas { get; set; } = new();
        public List<TrasladoReciente> Recientes { get; set; } = new();
    }
}
