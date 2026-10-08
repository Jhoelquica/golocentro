using GestionAlmacen_Golocentro.Data;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Services;

// Vencimiento y lote por entrada (UAT 06/10). Cada línea de entrada puede traer la fecha de vencimiento y el lote
// de esa mercadería. El vencimiento del producto (el que se ve en Stock y dispara las alertas de "por vencer") es
// el más próximo entre las entradas que todavía están en stock, suponiendo que sale primero lo que llegó antes.
// Si el producto nunca tuvo una entrada con fecha, se respeta la que se escribió a mano en el producto.
public static class VencimientoProducto
{
    public readonly record struct Entrada(int Cantidad, DateOnly? Vence, string? Lote);

    // entradas: de la más reciente a la más antigua. Lo que queda en stock es lo último que llegó.
    public static (DateOnly? Fecha, string? Lote) Calcular(int stock, IEnumerable<Entrada> entradas)
    {
        if (stock <= 0)
            return (null, null);

        (DateOnly? Fecha, string? Lote) proxima = (null, null);
        var porCubrir = stock;
        foreach (var e in entradas)
        {
            if (e.Vence is DateOnly vence && (proxima.Fecha == null || vence < proxima.Fecha))
                proxima = (vence, e.Lote);
            porCubrir -= e.Cantidad;
            if (porCubrir <= 0)
                break;
        }
        return proxima;
    }

    // Recalcula los productos indicados que tienen alguna entrada con fecha de vencimiento. Se llama después de
    // guardar cada operación que cambia el stock (entrada, venta, anulación, conteo).
    public static async Task ActualizarAsync(AppDbContext db, IEnumerable<int> idsProducto)
    {
        var ids = idsProducto.Distinct().ToList();
        var conFecha = await db.DetalleMovimientos
            .Where(d => ids.Contains(d.IdProducto) && d.FechaVencimiento != null && d.IdMovimientoNavigation.Tipo == "Entrada")
            .Select(d => d.IdProducto)
            .Distinct()
            .ToListAsync();
        if (conFecha.Count == 0)
            return;

        var stocks = await db.ProductoUbicacions
            .Where(pu => conFecha.Contains(pu.IdProducto))
            .GroupBy(pu => pu.IdProducto)
            .Select(g => new { IdProducto = g.Key, Stock = g.Sum(pu => pu.CantidadActual) })
            .ToDictionaryAsync(x => x.IdProducto, x => x.Stock);
        var entradas = (await db.DetalleMovimientos
                .Where(d => conFecha.Contains(d.IdProducto) && d.IdMovimientoNavigation.Tipo == "Entrada")
                .OrderByDescending(d => d.IdMovimientoNavigation.Fecha).ThenByDescending(d => d.IdDetalle)
                .Select(d => new { d.IdProducto, d.Cantidad, d.FechaVencimiento, d.Lote })
                .ToListAsync())
            .ToLookup(d => d.IdProducto, d => new Entrada(d.Cantidad, d.FechaVencimiento, d.Lote));

        var productos = await db.Productos.Where(p => conFecha.Contains(p.IdProducto)).ToListAsync();
        foreach (var p in productos)
            (p.FechaVencimiento, p.Lote) = Calcular(stocks.GetValueOrDefault(p.IdProducto), entradas[p.IdProducto]);
        await db.SaveChangesAsync();
    }

    // ¿El vencimiento del producto sale de sus entradas? (entonces no se edita a mano)
    public static Task<bool> SaleDeEntradasAsync(AppDbContext db, int idProducto) =>
        db.DetalleMovimientos.AnyAsync(d => d.IdProducto == idProducto && d.FechaVencimiento != null && d.IdMovimientoNavigation.Tipo == "Entrada");
}
