using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class DetalleAjuste
{
    public int IdDetalle { get; set; }

    public int IdAjuste { get; set; }

    public int IdProducto { get; set; }

    public int IdUbicacion { get; set; }

    public int CantidadAnterior { get; set; }

    public int CantidadNueva { get; set; }

    public virtual AjusteInventario IdAjusteNavigation { get; set; } = null!;

    public virtual Producto IdProductoNavigation { get; set; } = null!;

    public virtual Ubicacion IdUbicacionNavigation { get; set; } = null!;
}
