using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    public string Nombre { get; set; } = null!;

    public string Rol { get; set; } = null!;

    public string Usuario1 { get; set; } = null!;

    public string Contrasena { get; set; } = null!;

    public string Estado { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public int? IdSede { get; set; }

    public string? FotoUrl { get; set; }

    public virtual ICollection<AjusteInventario> AjusteInventarios { get; set; } = new List<AjusteInventario>();

    public virtual ICollection<Alertum> Alerta { get; set; } = new List<Alertum>();

    public virtual Sede? IdSedeNavigation { get; set; }

    public virtual ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();

    public virtual ICollection<NotaVentum> NotaVenta { get; set; } = new List<NotaVentum>();

    public virtual ICollection<Reporte> Reportes { get; set; } = new List<Reporte>();

    public virtual ICollection<Traslado> Traslados { get; set; } = new List<Traslado>();
}
