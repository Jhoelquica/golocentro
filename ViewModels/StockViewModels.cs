namespace GestionAlmacen_Golocentro.ViewModels
{
    // Consulta de stock del día a día (todos los roles): cuánto hay de cada producto y en qué zonas.
    public record StockZonaItem(int IdUbicacion, string Codigo, string Sede, int Cantidad);

    public record StockProductoItem(
        int Id,
        string Codigo,
        string Nombre,
        string Tipo,
        string Unidad,
        int Stock,
        int Minimo,
        string Estado,
        DateOnly? Vencimiento,
        int? DiasParaVencer,
        List<StockZonaItem> Zonas)
    {
        // Vencido o vence en 30 días o menos, y todavía hay stock que se puede perder
        public bool PorVencer => Stock > 0 && DiasParaVencer is int dias && dias <= StockConsultaViewModel.DiasAvisoVencimiento;
    }

    public class StockConsultaViewModel
    {
        public const int DiasAvisoVencimiento = 30;

        public List<StockProductoItem> Productos { get; set; } = new();
        public int? SedeId { get; set; }
        public string SedeNombre { get; set; } = "";
        public bool PuedeElegirSede { get; set; }
        public List<SedeOpcion> Sedes { get; set; } = new();
        public bool PuedeVerKardex { get; set; }
        public string? Busqueda { get; set; }
        public string Filtro { get; set; } = "todos";

        public int ConStock => Productos.Count(p => p.Stock > 0);
        public int Unidades => Productos.Sum(p => p.Stock);
        public int PorReponer => Productos.Count(p => p.Estado != EstadoStock.Normal);
        public int SinStock => Productos.Count(p => p.Estado == EstadoStock.SinStock);
        public int PorVencer => Productos.Count(p => p.PorVencer);
    }
}
