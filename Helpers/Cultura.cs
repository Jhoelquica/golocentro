using System.Globalization;

namespace GestionAlmacen_Golocentro.Helpers;

// El negocio trabaja en Perú: montos 1,234.50, fechas dd/MM/yyyy y meses en español, sin importar el idioma
// del servidor donde se instale (un Linux suele venir en inglés o sin cultura). Program.cs la fija para todo.
public static class Cultura
{
    public static readonly CultureInfo Peru = CultureInfo.GetCultureInfo("es-PE");
}
