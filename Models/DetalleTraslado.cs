using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class DetalleTraslado
{
    public int IdDetalle { get; set; }

    public int IdTraslado { get; set; }

    public int IdProducto { get; set; }

    public int Cantidad { get; set; }

    public int IdUbicacionOrigen { get; set; }

    public int IdUbicacionDestino { get; set; }

    public virtual Producto IdProductoNavigation { get; set; } = null!;

    public virtual Traslado IdTrasladoNavigation { get; set; } = null!;

    public virtual Ubicacion IdUbicacionDestinoNavigation { get; set; } = null!;

    public virtual Ubicacion IdUbicacionOrigenNavigation { get; set; } = null!;
}
