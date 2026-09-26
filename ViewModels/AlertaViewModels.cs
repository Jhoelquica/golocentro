namespace GestionAlmacen_Golocentro.ViewModels
{
    // Una alerta pendiente con los datos de hoy del producto (stock, zonas) para decidir qué hacer
    public record AlertaItem(
        int IdProducto,
        string Codigo,
        string Nombre,
        string Unidad,
        string Sede,
        int Stock,
        int Minimo,
        string Estado,
        DateOnly? Vencimiento,
        int? Dias,
        string? Lote,
        string Zonas,
        DateTime Desde);

    public record AlertaResuelta(string Tipo, string Mensaje, DateTime Desde, DateTime Resuelta, string Sede);

    public class AlertasViewModel
    {
        public string SedeNombre { get; set; } = "";
        public bool VariasSedes { get; set; }
        public bool PuedeVerKardex { get; set; }
        public List<AlertaItem> Reponer { get; set; } = new();
        public List<AlertaItem> Vencer { get; set; } = new();
        public List<AlertaResuelta> Resueltas { get; set; } = new();
    }
}
