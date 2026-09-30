using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class AjusteInventario
{
    public int IdAjuste { get; set; }

    public DateTime Fecha { get; set; }

    public int IdUsuario { get; set; }

    public int IdSede { get; set; }

    public string Motivo { get; set; } = null!;

    public string? Observaciones { get; set; }

    public virtual ICollection<DetalleAjuste> DetalleAjustes { get; set; } = new List<DetalleAjuste>();

    public virtual Sede IdSedeNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
