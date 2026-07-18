namespace GestionAlmacen_Golocentro.ViewModels
{
    public class StockViewModels
    {
        /// <summary>
        ///  una clase que contenga los datos que mostraremos en la tabla.
        /// </summary>
        public string ProductoNombre { get; set; }
        public string CodigoUbicacion { get; set; }
        public string SedeNombre { get; set; }
        public int StockActual { get; set; }
        public int? StockMinimo { get; set; }   // opcional, si tu tabla Producto lo tiene
        public bool BajoStock => StockMinimo.HasValue && StockActual <= StockMinimo.Value;
    }
}
