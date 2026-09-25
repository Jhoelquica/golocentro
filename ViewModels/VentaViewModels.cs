using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestionAlmacen_Golocentro.ViewModels
{
    public static class MetodosPago
    {
        // Mismos valores que chk_notaventa_metodo
        public static readonly (string Valor, string Texto)[] Todos =
        {
            ("efectivo", "Efectivo"),
            ("yape", "Yape"),
            ("plin", "Plin"),
            ("transferencia", "Transferencia"),
            ("tarjeta", "Tarjeta")
        };

        public static bool EsValido(string? valor) => Todos.Any(m => m.Valor == valor);

        public static string Texto(string valor) =>
            Todos.FirstOrDefault(m => m.Valor == valor).Texto ?? valor;
    }

    // Mismos valores que chk_notaventa_estado
    public static class EstadoNota
    {
        public const string Emitida = "emitida";
        public const string Anulada = "anulada";
    }

    // Cliente genérico creado por Database/2026-09-25_nota_venta.sql
    public static class ClienteGeneral
    {
        public const string RucDni = "00000000";
    }

    public record ZonaVenta(int Id, string Codigo, int Cantidad);

    public record ProductoVenta(int Id, string Nombre, string Codigo, string Unidad, decimal Precio, List<ZonaVenta> Zonas);

    public record ClienteVenta(int Id, string Nombre, string RucDni, string? Celular);

    public class VentaItem
    {
        public int ProductoId { get; set; }
        public int UbicacionId { get; set; }
        public int Cantidad { get; set; }

        // Precio cobrado; si viene vacío se usa el precio de lista del producto
        public decimal? Precio { get; set; }
    }

    public class VentaFormViewModel
    {
        public int? ClienteId { get; set; }
        public string? MetodoPago { get; set; }
        public decimal? Descuento { get; set; }

        [StringLength(200, ErrorMessage = "La observación no puede pasar de 200 caracteres.")]
        public string? Observaciones { get; set; }

        public List<VentaItem> Items { get; set; } = new();

        public IFormFile? Evidencia { get; set; }

        [BindNever, ValidateNever]
        public VentaDatos Datos { get; set; } = new();
    }

    public class VentaDatos
    {
        public List<ProductoVenta> Productos { get; set; } = new();
        public List<ClienteVenta> Clientes { get; set; } = new();
        public int ClienteGeneralId { get; set; }
    }

    // Zona: de dónde salió (y adónde vuelve si se anula); no se imprime
    public record LineaNota(int Cantidad, string Descripcion, string Unidad, decimal PrecioUnitario, decimal Importe, string Zona);

    public record EvidenciaNota(string Url, DateTime Fecha);

    public class NotaVentaViewModel
    {
        public int IdMovimiento { get; set; }
        public string Numero { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Vendedor { get; set; } = "";
        public string SedeNombre { get; set; } = "";
        public string? SedeDireccion { get; set; }
        public string? SedeCiudad { get; set; }
        public bool EsPublicoGeneral { get; set; }
        public string ClienteNombre { get; set; } = "";
        public string? ClienteDocumento { get; set; }
        public string? ClienteCelular { get; set; }
        public List<LineaNota> Lineas { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal Descuento { get; set; }
        public decimal Total { get; set; }
        public string MetodoPago { get; set; } = "";
        public string? Observaciones { get; set; }
        public List<EvidenciaNota> Evidencias { get; set; } = new();
        public bool Anulada { get; set; }
        public DateTime? FechaAnulacion { get; set; }
        public string? AnuladaPor { get; set; }
        public string? MotivoAnulacion { get; set; }
        public bool PuedeAnular { get; set; }
    }

    public record VentaFila(int IdMovimiento, string Numero, DateTime Fecha, string Cliente, decimal Total, string MetodoPago, string Vendedor, bool Anulada);

    public record TotalPorMetodo(string Metodo, int Ventas, decimal Total);

    public class VentasIndexViewModel
    {
        public DateOnly Desde { get; set; }
        public DateOnly Hasta { get; set; }
        public string? Busqueda { get; set; }
        public List<VentaFila> Filas { get; set; } = new();

        // Ventas válidas (sin las anuladas), que son las que suman en Total
        public int Ventas { get; set; }
        public int Anuladas { get; set; }
        public decimal Total { get; set; }
        public List<TotalPorMetodo> PorMetodo { get; set; } = new();
    }
}
