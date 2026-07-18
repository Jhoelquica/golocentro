using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Sede
{
    public int IdSede { get; set; }

    public string Nombre { get; set; } = null!;

    public string Direccion { get; set; } = null!;

    public string Ciudad { get; set; } = null!;

    public string Estado { get; set; } = null!;

    public virtual ICollection<Alertum> Alerta { get; set; } = new List<Alertum>();

    public virtual ICollection<Camara> Camaras { get; set; } = new List<Camara>();

    public virtual ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();

    public virtual ICollection<Reporte> Reportes { get; set; } = new List<Reporte>();

    public virtual ICollection<Ubicacion> Ubicacions { get; set; } = new List<Ubicacion>();

    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
