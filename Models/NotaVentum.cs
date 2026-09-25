using System;
using System.Collections.Generic;

namespace GestionAlmacen_Golocentro.Models;

public partial class NotaVentum
{
    public int IdNota { get; set; }

    public int IdMovimiento { get; set; }

    public string Serie { get; set; } = null!;

    public int Numero { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Descuento { get; set; }

    public decimal Total { get; set; }

    public string MetodoPago { get; set; } = null!;

    public virtual Movimiento IdMovimientoNavigation { get; set; } = null!;
}
