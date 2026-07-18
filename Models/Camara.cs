using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Camara
{
    public int IdCamara { get; set; }

    public string Codigo { get; set; } = null!;

    public string? Descripcion { get; set; }

    public int IdUbicacion { get; set; }

    public int IdSede { get; set; }

    public virtual ICollection<Evidencium> Evidencia { get; set; } = new List<Evidencium>();

    public virtual Sede IdSedeNavigation { get; set; } = null!;

    public virtual Ubicacion IdUbicacionNavigation { get; set; } = null!;
}
