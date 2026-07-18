using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class ProductoUbicacion
{
    public int Id { get; set; }

    public int IdProducto { get; set; }

    public int IdUbicacion { get; set; }

    public int CantidadActual { get; set; }

    public DateTime UltimaActualizacion { get; set; }

    public virtual Producto IdProductoNavigation { get; set; } = null!;

    public virtual Ubicacion IdUbicacionNavigation { get; set; } = null!;
}
