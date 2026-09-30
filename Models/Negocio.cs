using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class Negocio
{
    public int IdNegocio { get; set; }

    public string NombreComercial { get; set; } = null!;

    public string? RazonSocial { get; set; }

    public string? Ruc { get; set; }

    public string? Telefono { get; set; }

    public string? Correo { get; set; }

    public string? MensajeNota { get; set; }
}
