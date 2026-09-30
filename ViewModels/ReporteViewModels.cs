namespace GestionAlmacen_Golocentro.ViewModels
{
    // Filtros comunes: periodo y sede. La encargada solo ve su sede; la dueña elige (o todas).
    public class FiltroReporte
    {
        public string Accion { get; set; } = "";
        public bool ConFechas { get; set; } = true;
        public DateOnly Desde { get; set; }
        public DateOnly Hasta { get; set; }
        public int? SedeId { get; set; }
        public string SedeNombre { get; set; } = "";
        public bool PuedeElegirSede { get; set; }
        public List<SedeOpcion> Sedes { get; set; } = new();

        public DateTime Inicio => Desde.ToDateTime(TimeOnly.MinValue);
        public DateTime Fin => Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);

        public string Periodo => Desde == Hasta
            ? Desde.ToString("dd/MM/yyyy")
            : $"{Desde:dd/MM/yyyy} al {Hasta:dd/MM/yyyy}";
    }

    public class ReporteIndexViewModel
    {
        public string SedeNombre { get; set; } = "";
        public decimal VentasMes { get; set; }
        public int NumeroVentasMes { get; set; }
        public int EntradasMes { get; set; }
        public int ProductosEnAlerta { get; set; }
        public decimal ValorStock { get; set; }
    }

    // ---- Ventas ----
    public record VentaReporteFila(
        int IdMovimiento, string Numero, DateTime Fecha, string Cliente, string Vendedor, string Sede,
        string MetodoPago, decimal Subtotal, decimal Descuento, decimal Total, bool Anulada);

    public record VentaPorPeriodo(string Etiqueta, string EtiquetaLarga, int Ventas, decimal Total);

    public record TotalPorVendedor(string Vendedor, int Ventas, decimal Total);

    public class ReporteVentasViewModel
    {
        public FiltroReporte Filtro { get; set; } = new();
        public int Ventas { get; set; }
        public int Anuladas { get; set; }
        public decimal Total { get; set; }
        public decimal Descuentos { get; set; }
        public decimal TicketPromedio { get; set; }
        public bool PorMes { get; set; }
        public List<VentaPorPeriodo> PorPeriodo { get; set; } = new();
        public List<TotalPorMetodo> PorMetodo { get; set; } = new();
        public List<TotalPorVendedor> PorVendedor { get; set; } = new();
        public List<VentaReporteFila> Filas { get; set; } = new();
    }

    // ---- Productos más vendidos ----
    public record ProductoVendidoFila(
        string Codigo, string Nombre, string Unidad, int Cantidad, decimal Importe, int Ventas, decimal Porcentaje);

    public class ReporteProductosViewModel
    {
        public FiltroReporte Filtro { get; set; } = new();
        public List<ProductoVendidoFila> Filas { get; set; } = new();
        public decimal Importe { get; set; }
        public int Unidades { get; set; }
    }

    // ---- Entradas ----
    public record EntradaReporteFila(
        int IdMovimiento, DateTime Fecha, string Proveedor, string? Comprobante, string Usuario, string Sede,
        int Productos, int Unidades);

    public record TotalPorProveedor(string Proveedor, int Entradas, int Unidades);

    public class ReporteEntradasViewModel
    {
        public FiltroReporte Filtro { get; set; } = new();
        public List<EntradaReporteFila> Filas { get; set; } = new();
        public List<TotalPorProveedor> PorProveedor { get; set; } = new();
        public int Unidades { get; set; }
    }

    // ---- Kardex por producto ----
    public record KardexProductoOpcion(int Id, string Codigo, string Nombre, int Stock);

    // Entra/Sale en null cuando el movimiento no cambia el stock de la sede (traslado entre zonas, conteo sin diferencia)
    public record MovimientoKardex(
        DateTime Fecha, string Tipo, string TipoTexto, string Documento, string? Detalle, string Zona,
        int? Entra, int? Sale, int Saldo, string Usuario, string Sede, string? Enlace);

    public static class TipoKardex
    {
        public const string Entrada = "entrada";
        public const string Venta = "venta";
        public const string Anulacion = "anulacion";
        public const string Conteo = "conteo";
        public const string Traslado = "traslado";
        public const string Salida = "salida";
    }

    public class ReporteKardexViewModel
    {
        public FiltroReporte Filtro { get; set; } = new();
        public List<KardexProductoOpcion> Productos { get; set; } = new();
        public int? ProductoId { get; set; }
        public string ProductoNombre { get; set; } = "";
        public string ProductoCodigo { get; set; } = "";
        public string Unidad { get; set; } = "";
        public int SaldoInicial { get; set; }
        public int Entradas { get; set; }
        public int Ventas { get; set; }
        public int Devoluciones { get; set; }
        public int Ajustes { get; set; }
        public int SaldoFinal { get; set; }
        public int StockActual { get; set; }
        public string ZonasActuales { get; set; } = "";

        // El saldo inicial salió negativo: hubo cambios de stock sin registro (p. ej. datos borrados o cargados directo en la BD)
        public bool Descuadre { get; set; }
        public List<MovimientoKardex> Filas { get; set; } = new();
    }

    // ---- Stock valorizado ----
    public static class EstadoStock
    {
        public const string SinStock = "sin_stock";
        public const string Bajo = "bajo";
        public const string Normal = "normal";

        public static string De(int stock, int minimo) =>
            stock <= 0 ? SinStock : stock <= minimo ? Bajo : Normal;

        public static string Texto(string estado) => estado switch
        {
            SinStock => "Sin stock",
            Bajo => "Stock bajo",
            _ => "Normal"
        };
    }

    public record StockReporteFila(
        int Id, string Codigo, string Nombre, string Tipo, string Unidad, int Stock, int Minimo,
        decimal Precio, decimal Valor, string Zonas, string Estado, DateOnly? Vencimiento);

    public class ReporteStockViewModel
    {
        public FiltroReporte Filtro { get; set; } = new();
        public bool SoloAlertas { get; set; }
        public List<StockReporteFila> Filas { get; set; } = new();
        public int Productos { get; set; }
        public int SinStock { get; set; }
        public int Bajos { get; set; }
        public int Unidades { get; set; }
        public decimal Valor { get; set; }
    }
}
