namespace GestionAlmacen_Golocentro.ViewModels
{
    public class ProductoListViewModel
    {
        public int IdProducto { get; set; }
        public string Nombre { get; set; }
        public string Tipo { get; set; }
        public string Codigo { get; set; }
        public string UnidadMedida { get; set; }
        public decimal PrecioUnitario { get; set; }

        // Calculado: suma de ProductoUbicacion.CantidadActual
        // (filtrado por la sede del usuario logueado, si aplica)
        public int StockActual { get; set; }

        public int StockMinimo { get; set; }
    }
}
