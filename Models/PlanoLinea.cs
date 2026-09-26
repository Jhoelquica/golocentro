using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class PlanoLinea
{
    public int IdLinea { get; set; }

    public int IdSede { get; set; }

    public string Tipo { get; set; } = null!;

    public decimal X1 { get; set; }

    public decimal Y1 { get; set; }

    public decimal X2 { get; set; }

    public decimal Y2 { get; set; }

    public virtual Sede IdSedeNavigation { get; set; } = null!;
}
