using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Producto
{
    public int IdProducto { get; set; }

    public string Nombre { get; set; } = null!;

    public string Tipo { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public string UnidadMedida { get; set; } = null!;

    public decimal PrecioUnitario { get; set; }

    public int StockActual { get; set; }

    public int StockMinimo { get; set; }

    public DateOnly? FechaVencimiento { get; set; }

    public string? Lote { get; set; }

    public virtual ICollection<Alertum> Alerta { get; set; } = new List<Alertum>();

    public virtual ICollection<DetalleMovimiento> DetalleMovimientos { get; set; } = new List<DetalleMovimiento>();

    public virtual ICollection<ProductoUbicacion> ProductoUbicacions { get; set; } = new List<ProductoUbicacion>();
}
