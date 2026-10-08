namespace GestionAlmacen_Golocentro.Helpers;

// Presentaciones de un producto (UAT 06/10). La unidad base (producto.unidad_medida) es la más chica que se vende:
// tira en los snacks, pack en las galletas. Cada presentación trae varias unidades base y tiene su propio precio
// (Doritos: bolsa = 8 tiras; galleta: caja = 12 packs). El stock se cuenta siempre en la unidad base.
public static class Presentaciones
{
    public const int MaxNombre = 30;
    public const int MaxFactor = 100_000;

    public record Fila(string? Nombre, int? Factor, decimal? Precio);

    // Errores por fila: (índice, campo, mensaje). Las filas totalmente vacías no cuentan.
    public static List<(int Indice, string Campo, string Mensaje)> Validar(string? unidadBase, IReadOnlyList<Fila> filas)
    {
        var errores = new List<(int, string, string)>();
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i];
            if (EstaVacia(f))
                continue;

            var nombre = f.Nombre?.Trim();
            if (string.IsNullOrEmpty(nombre))
                errores.Add((i, "Nombre", "Escribe el nombre de la presentación (bolsa, caja, pack...)."));
            else if (nombre.Length > MaxNombre)
                errores.Add((i, "Nombre", $"El nombre de la presentación no puede pasar de {MaxNombre} caracteres."));
            else if (string.Equals(nombre, unidadBase?.Trim(), StringComparison.OrdinalIgnoreCase))
                errores.Add((i, "Nombre", $"«{nombre}» ya es la unidad base: la presentación debe ser más grande."));
            else if (!nombres.Add(nombre))
                errores.Add((i, "Nombre", $"La presentación «{nombre}» está repetida."));

            if (f.Factor == null)
                errores.Add((i, "Factor", "Escribe cuántas unidades base trae."));
            else if (f.Factor < 2 || f.Factor > MaxFactor)
                errores.Add((i, "Factor", $"Debe traer de 2 a {MaxFactor:N0} unidades base."));

            if (f.Precio == null)
                errores.Add((i, "Precio", "Escribe el precio de la presentación."));
            else if (f.Precio < 0)
                errores.Add((i, "Precio", "El precio no puede ser negativo."));
            else if (decimal.Round(f.Precio.Value, 2) != f.Precio)
                errores.Add((i, "Precio", "Usa como máximo 2 decimales en el precio."));
        }
        return errores;
    }

    public static bool EstaVacia(Fila f) => string.IsNullOrWhiteSpace(f.Nombre) && f.Factor == null && f.Precio == null;

    // Importe de una línea: la cantidad está en unidades base; cantidad / factor = presentaciones, por su precio
    public static decimal Importe(int cantidadBase, int factor, decimal precio) =>
        decimal.Round(cantidadBase / Math.Max(factor, 1) * precio, 2);

    // "3 bolsas y 2 tiras": la cantidad en unidades base con la presentación más grande que tiene el producto
    public static string Describir(int cantidadBase, string unidadBase, IEnumerable<(string Nombre, int Factor)> presentaciones)
    {
        var mayor = presentaciones.Where(p => p.Factor > 1).OrderByDescending(p => p.Factor).FirstOrDefault();
        if (mayor.Nombre == null || cantidadBase < mayor.Factor)
            return Cantidad(cantidadBase, unidadBase);

        var enteras = cantidadBase / mayor.Factor;
        var sueltas = cantidadBase % mayor.Factor;
        return sueltas == 0
            ? Cantidad(enteras, mayor.Nombre)
            : $"{Cantidad(enteras, mayor.Nombre)} y {Cantidad(sueltas, unidadBase)}";
    }

    public static string Cantidad(int n, string unidad) => $"{n:N0} {(n == 1 ? unidad : Plural(unidad))}";

    // Plural en castellano de una unidad: bolsa → bolsas, unidad → unidades, pack → packs; las abreviaturas (kg, lt) no cambian
    public static string Plural(string unidad)
    {
        var u = unidad.Trim();
        if (u.Length <= 2 || u.Contains('.') || u.Contains(' '))
            return u;
        var ultima = char.ToLowerInvariant(u[^1]);
        if ("aeiouáéíóú".Contains(ultima))
            return u + "s";
        if (ultima == 'z')
            return u[..^1] + "ces";
        if ("dlnr".Contains(ultima))
            return u + "es";
        return u + "s";
    }
}
