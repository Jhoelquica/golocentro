using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestionAlmacen_Golocentro.ViewModels
{
    // ===== Registrar entrada =====
    public class DetalleEntradaViewModel
    {
        public int ProductoId { get; set; }
        public int UbicacionId { get; set; }
        public int Cantidad { get; set; }
    }

    public class MovimientoEntradaViewModel
    {
        public List<DetalleEntradaViewModel> Detalles { get; set; } = new();
        public int? ProveedorId { get; set; }
        public string? NumeroFactura { get; set; }
        public IFormFile? Evidencia { get; set; }

        [BindNever, ValidateNever]
        public EntradaDatos Datos { get; set; } = new();
    }

    public record ProductoEntrada(int Id, string Nombre, string Codigo, string Unidad);

    public record ZonaEntrada(int Id, string Codigo, string? Descripcion, bool Recepcion, string Sede);

    // Dónde está hoy cada producto (para sugerir recibirlo ahí)
    public record StockZonaEntrada(int ProductoId, int ZonaId, int Cantidad);

    public record ProveedorOpcion(int Id, string Nombre);

    public class EntradaDatos
    {
        public List<ProductoEntrada> Productos { get; set; } = new();
        public List<ZonaEntrada> Zonas { get; set; } = new();
        public List<StockZonaEntrada> Stock { get; set; } = new();
        public List<ProveedorOpcion> Proveedores { get; set; } = new();
        public bool PuedeCrear { get; set; }
        public bool VariasSedes { get; set; }
    }

    // ===== Lista de movimientos (toda la actividad de stock) =====
    public static class TipoActividad
    {
        public const string Entrada = "entrada";
        public const string Venta = "venta";
        public const string Traslado = "traslado";
        public const string Conteo = "conteo";
        public const string Salida = "salida";
    }

    public record ActividadFila(
        DateTime Fecha,
        string Tipo,
        string TipoTexto,
        string Titulo,
        string? Documento,
        string Resumen,
        string Usuario,
        string Sede,
        decimal? Total,
        bool Anulada,
        int Fotos,
        string? Enlace);

    public class MovimientosIndexViewModel
    {
        public string Tipo { get; set; } = "todos";
        public DateOnly Desde { get; set; }
        public DateOnly Hasta { get; set; }
        public bool VariasSedes { get; set; }
        public Dictionary<string, int> PorTipo { get; set; } = new();
        public List<ActividadFila> Filas { get; set; } = new();
        public int Pagina { get; set; }
        public int TotalPaginas { get; set; }
        public int Total { get; set; }
    }

    // ===== Detalle de una entrada (o salida antigua) =====
    public record LineaDetalleMovimiento(string Producto, string Codigo, string Unidad, string Zona, int Cantidad, int StockAnterior, int StockDespues);

    public class MovimientoDetalleViewModel
    {
        public int Id { get; set; }
        public bool EsEntrada { get; set; }
        public DateTime Fecha { get; set; }
        public string Usuario { get; set; } = "";
        public string Sede { get; set; } = "";
        public string? Contraparte { get; set; }
        public string? Documento { get; set; }
        public List<LineaDetalleMovimiento> Lineas { get; set; } = new();
        public List<EvidenciaNota> Evidencias { get; set; } = new();
        public int Unidades => Lineas.Sum(l => l.Cantidad);
    }
}
