using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Alertum
{
    public int IdAlerta { get; set; }

    public int IdProducto { get; set; }

    public string Tipo { get; set; } = null!;

    public string Mensaje { get; set; } = null!;

    public DateTime FechaGenerada { get; set; }

    public string Estado { get; set; } = null!;

    public int? IdUsuarioAtiende { get; set; }

    public DateTime? FechaAtendida { get; set; }

    public int IdSede { get; set; }

    public virtual Producto IdProductoNavigation { get; set; } = null!;

    public virtual Sede IdSedeNavigation { get; set; } = null!;

    public virtual Usuario? IdUsuarioAtiendeNavigation { get; set; }
}
