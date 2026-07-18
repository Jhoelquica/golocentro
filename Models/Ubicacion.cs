using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Ubicacion
{
    public int IdUbicacion { get; set; }

    public string CodigoEstante { get; set; } = null!;

    public string? Descripcion { get; set; }

    public int Capacidad { get; set; }

    public int IdSede { get; set; }

    public virtual ICollection<Camara> Camaras { get; set; } = new List<Camara>();

    public virtual ICollection<DetalleMovimiento> DetalleMovimientos { get; set; } = new List<DetalleMovimiento>();

    public virtual Sede IdSedeNavigation { get; set; } = null!;

    public virtual ICollection<ProductoUbicacion> ProductoUbicacions { get; set; } = new List<ProductoUbicacion>();
}
