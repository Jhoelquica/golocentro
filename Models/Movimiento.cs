using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Movimiento
{
    public int IdMovimiento { get; set; }

    public string Tipo { get; set; } = null!;

    public DateTime Fecha { get; set; }

    public int IdUsuario { get; set; }

    public int? IdCliente { get; set; }

    public int? IdProveedor { get; set; }

    public bool ComprobanteEmitido { get; set; }

    public string? Observaciones { get; set; }

    public int? IdSede { get; set; }

    public virtual ICollection<DetalleMovimiento> DetalleMovimientos { get; set; } = new List<DetalleMovimiento>();

    public virtual ICollection<Evidencium> Evidencia { get; set; } = new List<Evidencium>();

    public virtual Cliente? IdClienteNavigation { get; set; }

    public virtual Proveedor? IdProveedorNavigation { get; set; }

    public virtual Sede IdSedeNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
