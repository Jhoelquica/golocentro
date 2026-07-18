using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Reporte
{
    public int IdReporte { get; set; }

    public string Tipo { get; set; } = null!;

    public DateTime FechaGeneracion { get; set; }

    public string? FiltrosAplicados { get; set; }

    public string? UrlExportacion { get; set; }

    public int IdUsuario { get; set; }

    public int? IdSede { get; set; }

    public virtual Sede? IdSedeNavigation { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
