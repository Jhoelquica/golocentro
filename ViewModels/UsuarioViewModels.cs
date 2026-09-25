using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GestionAlmacen_Golocentro.ViewModels
{
    // Mismos valores que chk_usuario_rol
    public static class Roles
    {
        public const string Duena = "duena";
        public const string Encargada = "encargada";
        public const string Trabajador = "trabajador";

        // La dueña solo crea encargadas y trabajadores; su propia cuenta no se toca desde aquí
        public static readonly (string Valor, string Texto, string Descripcion)[] Asignables =
        {
            (Encargada, "Encargada", "Además de vender y contar, gestiona productos, clientes, proveedores y reportes de su sede."),
            (Trabajador, "Trabajador", "Vende, registra entradas, anula ventas, mueve mercadería y cuenta inventario en su sede.")
        };

        public static bool EsAsignable(string? rol) => rol == Encargada || rol == Trabajador;
    }

    public record UsuarioFila(
        int Id,
        string Nombre,
        string Usuario,
        string Rol,
        string? Sede,
        bool Activo,
        DateTime FechaCreacion,
        DateTime? UltimoMovimiento,
        bool EsDuena);

    public class UsuarioListaViewModel
    {
        public List<UsuarioFila> Filas { get; set; } = new();
    }

    public class UsuarioFormViewModel
    {
        public int? Id { get; set; }
        public string? Nombre { get; set; }
        public string? NombreUsuario { get; set; }
        public string? Rol { get; set; }
        public int? SedeId { get; set; }
        public bool Activo { get; set; } = true;

        // Solo al crear: la contraseña inicial que la dueña le pasa a la persona
        public string? Contrasena { get; set; }
        public string? ConfirmarContrasena { get; set; }

        [BindNever, ValidateNever]
        public List<SedeOpcion> Sedes { get; set; } = new();
    }

    public class RestablecerContrasenaViewModel
    {
        public int Id { get; set; }
        public string? Nueva { get; set; }
        public string? Confirmar { get; set; }

        [BindNever, ValidateNever]
        public string Nombre { get; set; } = "";

        [BindNever, ValidateNever]
        public string NombreUsuario { get; set; } = "";
    }

    public class CambiarContrasenaViewModel
    {
        public string? Actual { get; set; }
        public string? Nueva { get; set; }
        public string? Confirmar { get; set; }
    }
}
