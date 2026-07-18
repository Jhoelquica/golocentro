using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class DetalleMovimiento
{
    public int IdDetalle { get; set; }

    public int IdMovimiento { get; set; }

    public int IdProducto { get; set; }

    public int Cantidad { get; set; }

    public decimal PrecioUnitarioSnapshot { get; set; }

    public int StockAnterior { get; set; }

    public int IdUbicacion { get; set; }

    public virtual ICollection<Evidencium> Evidencia { get; set; } = new List<Evidencium>();

    public virtual Movimiento IdMovimientoNavigation { get; set; } = null!;

    public virtual Producto IdProductoNavigation { get; set; } = null!;

    public virtual Ubicacion IdUbicacionNavigation { get; set; } = null!;
}
