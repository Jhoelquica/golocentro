using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class ProductoPresentacion
{
    public int IdPresentacion { get; set; }

    public int IdProducto { get; set; }

    public string Nombre { get; set; } = null!;

    public int Factor { get; set; }

    public decimal Precio { get; set; }

    public virtual Producto IdProductoNavigation { get; set; } = null!;
}
