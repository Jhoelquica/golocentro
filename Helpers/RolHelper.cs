namespace GestionAlmacen_Golocentro.Helpers;

// La BD guarda los roles sin tildes ni eñes (chk_usuario_rol); esto es solo para mostrarlos.
public static class RolHelper
{
    public static string Etiqueta(string? rol) => rol switch
    {
        "duena" => "Dueña",
        "encargada" => "Encargada",
        "trabajador" => "Trabajador",
        null or "" => "",
        _ => char.ToUpper(rol[0]) + rol[1..]
    };
}
