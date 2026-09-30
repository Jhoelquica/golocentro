using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestionAlmacen_Golocentro.ViewModels
{
    // Datos del negocio que salen en la nota de venta
    public record DatosNegocio(
        string NombreComercial,
        string? RazonSocial,
        string? Ruc,
        string? Telefono,
        string? Correo,
        string MensajeNota)
    {
        public const string MensajePorDefecto = "¡Gracias por su compra!";
        public static readonly DatosNegocio PorDefecto = new("Distribuidora Golocentro", null, null, null, null, MensajePorDefecto);
    }

    public record SedeFila(int Id, string Nombre, string Direccion, string Ciudad, string? Telefono, int Zonas, int Usuarios);

    public class NegocioFormViewModel
    {
        public string? NombreComercial { get; set; }
        public string? RazonSocial { get; set; }
        public string? Ruc { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? MensajeNota { get; set; }

        [BindNever, ValidateNever]
        public List<SedeFila> Sedes { get; set; } = new();
    }

    public class SedeFormViewModel
    {
        // 0 = sede nueva
        public int Id { get; set; }
        public bool EsNueva => Id == 0;

        // Solo al crear: deja lista la zona R-01 de Recepción para poder registrar entradas
        public bool CrearRecepcion { get; set; } = true;

        // Solo al editar: por qué no se puede borrar (null = se puede)
        [BindNever, ValidateNever]
        public string? MotivoNoEliminar { get; set; }
        public string? Nombre { get; set; }
        public string? Direccion { get; set; }
        public string? Ciudad { get; set; }
        public string? Telefono { get; set; }

        [BindNever, ValidateNever]
        public DatosNegocio Negocio { get; set; } = DatosNegocio.PorDefecto;
    }

    // ===== Zonas de una sede =====
    public static class TipoZona
    {
        public const string Almacenaje = "almacenaje";
        public const string Recepcion = "recepcion";
    }

    public record ZonaFila(
        int Id, string Codigo, string? Descripcion, bool Recepcion, bool Dibujada, int Stock, int Productos, bool TieneHistorial);

    public class ZonaFormViewModel
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        public string? Descripcion { get; set; }
        public string Tipo { get; set; } = TipoZona.Almacenaje;

        [BindNever, ValidateNever]
        public int SedeId { get; set; }

        [BindNever, ValidateNever]
        public string SedeNombre { get; set; } = "";
    }

    public class ZonasSedeViewModel
    {
        public int SedeId { get; set; }
        public string SedeNombre { get; set; } = "";
        public List<ZonaFila> Zonas { get; set; } = new();
        public ZonaFormViewModel Nueva { get; set; } = new();
    }

    // ===== Editor del plano =====
    // Todo en "cuadritos" del plano (el mismo sistema que usa el mapa); null = zona sin dibujar
    public record ZonaEditor(int Id, string Codigo, string? Descripcion, bool Recepcion, int Productos,
        decimal? X, decimal? Y, decimal? Ancho, decimal? Alto);

    public record LineaEditor(string Tipo, decimal X1, decimal Y1, decimal X2, decimal Y2);

    public class PlanoEditorViewModel
    {
        public const decimal TamanoMinimo = 5, TamanoMaximo = 100;

        public int SedeId { get; set; }
        public string SedeNombre { get; set; } = "";
        public decimal Ancho { get; set; }
        public decimal Alto { get; set; }
        public List<ZonaEditor> Zonas { get; set; } = new();
        public List<LineaEditor> Lineas { get; set; } = new();
    }

    // Lo que manda el editor al guardar (JSON en un campo oculto)
    public class PlanoGuardado
    {
        public decimal Ancho { get; set; }
        public decimal Alto { get; set; }
        public List<ZonaEditor> Zonas { get; set; } = new();
        public List<LineaEditor> Lineas { get; set; } = new();
    }
}
