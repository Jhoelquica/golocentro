namespace GestionAlmacen_Golocentro.ViewModels
{
    public class DashboardViewModel
    {
        public string NombreCompleto { get; set; } = "";
        public string? Rol { get; set; }

        public int EntradasHoy { get; set; }
        public int SalidasHoy { get; set; }
        public int AlertasActivas { get; set; }
        public int AlertasStockBajo { get; set; }
        public int AlertasVencimiento { get; set; }

        // Siempre 7 elementos, del más antiguo a hoy (los días sin movimientos van en 0)
        public List<ActividadDia> UltimosSieteDias { get; set; } = new();
        public List<MovimientoResumen> UltimosMovimientos { get; set; } = new();
        public List<AlertaResumen> UltimasAlertas { get; set; } = new();
    }

    public record ActividadDia(DateTime Fecha, int Entradas, int Salidas);

    public record MovimientoResumen(
        int IdMovimiento,
        string Tipo,
        DateTime Fecha,
        string Usuario,
        string? Contraparte,
        int CantidadProductos,
        bool Anulada);

    public record AlertaResumen(string Tipo, string Mensaje, DateTime FechaGenerada);
}
