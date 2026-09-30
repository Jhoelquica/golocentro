using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Traslado
{
    public int IdTraslado { get; set; }

    public DateTime Fecha { get; set; }

    public int IdUsuario { get; set; }

    public int IdSede { get; set; }

    public string? Observaciones { get; set; }

    public virtual ICollection<DetalleTraslado> DetalleTraslados { get; set; } = new List<DetalleTraslado>();

    public virtual Sede IdSedeNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
