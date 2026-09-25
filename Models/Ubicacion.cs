using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Ubicacion
{
    public int IdUbicacion { get; set; }

    public string CodigoEstante { get; set; } = null!;

    public string? Descripcion { get; set; }

    public int? Capacidad { get; set; }

    public int IdSede { get; set; }

    public string Tipo { get; set; } = null!;

    public decimal? PosX { get; set; }

    public decimal? PosY { get; set; }

    public decimal? Ancho { get; set; }

    public decimal? Alto { get; set; }

    public virtual ICollection<Camara> Camaras { get; set; } = new List<Camara>();

    public virtual ICollection<DetalleAjuste> DetalleAjustes { get; set; } = new List<DetalleAjuste>();

    public virtual ICollection<DetalleMovimiento> DetalleMovimientos { get; set; } = new List<DetalleMovimiento>();

    public virtual ICollection<DetalleTraslado> DetalleTrasladoIdUbicacionDestinoNavigations { get; set; } = new List<DetalleTraslado>();

    public virtual ICollection<DetalleTraslado> DetalleTrasladoIdUbicacionOrigenNavigations { get; set; } = new List<DetalleTraslado>();

    public virtual Sede IdSedeNavigation { get; set; } = null!;

    public virtual ICollection<ProductoUbicacion> ProductoUbicacions { get; set; } = new List<ProductoUbicacion>();
}
