using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Services
{
    // Las alertas se mantienen solas: cada sincronización crea las que faltan, actualiza el mensaje
    // de las pendientes y da por atendidas las que ya no aplican (llegó mercadería, se vendió lo que vencía...).
    // Mismas reglas que la pantalla de Stock: stock <= mínimo, o vence en 30 días o menos y todavía hay stock.
    public static class AlertasStock
    {
        public const string StockMinimo = "stock_minimo";
        public const string Vencimiento = "vencimiento";
        public const string Pendiente = "pendiente";
        public const string Atendida = "atendida";
        public const int DiasAvisoVencimiento = 30;

        public static async Task Sincronizar(AppDbContext db)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var ahora = DateTime.Now;

            var sedes = await db.Sedes.Select(s => s.IdSede).ToListAsync();
            var productos = await db.Productos
                .Select(p => new { p.IdProducto, p.Nombre, p.StockMinimo, p.FechaVencimiento, p.Lote })
                .ToListAsync();
            var stock = (await db.ProductoUbicacions
                    .GroupBy(pu => new { pu.IdProducto, pu.IdUbicacionNavigation.IdSede })
                    .Select(g => new { g.Key.IdProducto, g.Key.IdSede, Cantidad = g.Sum(pu => pu.CantidadActual) })
                    .ToListAsync())
                .ToDictionary(s => (s.IdProducto, s.IdSede), s => s.Cantidad);

            var pendientes = (await db.Alerta.Where(a => a.Estado == Pendiente).ToListAsync())
                .GroupBy(a => (a.IdProducto, a.IdSede, a.Tipo))
                .ToDictionary(g => g.Key, g => g.OrderBy(a => a.FechaGenerada).ToList());
            var vigentes = new HashSet<(int, int, string)>();

            void Vigente(int idProducto, int idSede, string tipo, string mensaje)
            {
                var clave = (idProducto, idSede, tipo);
                vigentes.Add(clave);
                if (pendientes.TryGetValue(clave, out var existentes))
                {
                    if (existentes[0].Mensaje != mensaje)
                        existentes[0].Mensaje = mensaje;
                    return;
                }
                db.Alerta.Add(new Alertum
                {
                    IdProducto = idProducto,
                    IdSede = idSede,
                    Tipo = tipo,
                    Mensaje = mensaje,
                    Estado = Pendiente,
                    FechaGenerada = ahora
                });
            }

            foreach (var idSede in sedes)
            {
                foreach (var p in productos)
                {
                    var cantidad = stock.GetValueOrDefault((p.IdProducto, idSede));
                    if (cantidad <= p.StockMinimo)
                        Vigente(p.IdProducto, idSede, StockMinimo, cantidad <= 0
                            ? $"Sin stock: {p.Nombre} (mínimo {p.StockMinimo})"
                            : $"Stock bajo: {p.Nombre} ({cantidad} unidades, mínimo {p.StockMinimo})");

                    if (cantidad > 0 && p.FechaVencimiento is DateOnly vence)
                    {
                        var dias = vence.DayNumber - hoy.DayNumber;
                        if (dias <= DiasAvisoVencimiento)
                        {
                            var lote = string.IsNullOrWhiteSpace(p.Lote) ? "" : $" (lote {p.Lote})";
                            Vigente(p.IdProducto, idSede, Vencimiento, dias < 0
                                ? $"Vencido: {p.Nombre}{lote} desde el {vence:dd/MM/yyyy}"
                                : $"Próximo a vencer: {p.Nombre}{lote} – {vence:dd/MM/yyyy}");
                        }
                    }
                }
            }

            // Las que ya no aplican (y las duplicadas por dos sincronizaciones a la vez) quedan atendidas
            foreach (var (clave, lista) in pendientes)
            {
                foreach (var alerta in vigentes.Contains(clave) ? lista.Skip(1) : lista)
                {
                    alerta.Estado = Atendida;
                    alerta.FechaAtendida = ahora;
                }
            }

            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync();
        }
    }
}
