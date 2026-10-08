using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Proveedor
{
    public int IdProveedor { get; set; }

    public string Nombre { get; set; } = null!;

    public string Ruc { get; set; } = null!;

    public string? Contacto { get; set; }

    public string? Direccion { get; set; }

    public string? Celular { get; set; }

    public virtual ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
}
