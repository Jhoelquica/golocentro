using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Evidencium
{
    public int IdEvidencia { get; set; }

    public string Tipo { get; set; } = null!;

    public string UrlArchivo { get; set; } = null!;

    public int IdMovimiento { get; set; }

    public int? IdDetalle { get; set; }

    public int? IdCamara { get; set; }

    public DateTime Fecha { get; set; }

    public virtual Camara? IdCamaraNavigation { get; set; }

    public virtual DetalleMovimiento? IdDetalleNavigation { get; set; }

    public virtual Movimiento IdMovimientoNavigation { get; set; } = null!;
}
