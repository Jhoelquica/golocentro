using System.Reflection;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.Services;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Alertas automáticas (Services/AlertasStock): stock bajo por sede y vencimiento, sin duplicados,
// resolución automática y el límite de "una vez por minuto" del globito del menú.
// Semilla: Caramelo mínimo 5 con 10 en A-01; Chocolate mínimo 2 con 20 en A-02 (ambas zonas de la sede Principal).
[Collection(ColeccionBaseDeDatos.Nombre)]
public class AlertasTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;

    public AlertasTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ===== Ayudas =====

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    // Cambia el stock directo en la base (sin pasar por SaveChanges, así no avisa a las alertas)
    private async Task PonerStock(int idProducto, int idZona, int cantidad)
    {
        await using var db = _base.NuevoContexto();
        var filas = await db.ProductoUbicacions.Where(pu => pu.IdProducto == idProducto && pu.IdUbicacion == idZona)
            .ExecuteUpdateAsync(s => s.SetProperty(pu => pu.CantidadActual, cantidad));
        Assert.Equal(1, filas);
    }

    private async Task PonerVencimiento(int idProducto, DateOnly? vence, string? lote)
    {
        await using var db = _base.NuevoContexto();
        await db.Productos.Where(p => p.IdProducto == idProducto)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaVencimiento, vence).SetProperty(p => p.Lote, lote));
    }

    private async Task Sincronizar()
    {
        await using var db = _base.NuevoContexto();
        await AlertasStock.Sincronizar(db);
    }

    private async Task SincronizarSiToca()
    {
        await using var db = _base.NuevoContexto();
        await AlertasStock.SincronizarSiToca(db);
    }

    private async Task<List<Alertum>> Alertas()
    {
        await using var db = _base.NuevoContexto();
        return await db.Alerta.AsNoTracking().OrderBy(a => a.IdAlerta).ToListAsync();
    }

    private async Task<List<Alertum>> Pendientes(string tipo) =>
        (await Alertas()).Where(a => a.Estado == AlertasStock.Pendiente && a.Tipo == tipo).ToList();

    // ===== 1. Stock bajo =====

    [Fact]
    public async Task Stock_igual_al_minimo_genera_una_alerta_de_stock_bajo_para_su_sede()
    {
        await PonerStock(_d.Caramelo, _d.ZonaA01, 5);

        await Sincronizar();

        var alerta = Assert.Single(await Alertas());
        Assert.Equal((_d.Caramelo, _d.SedePrincipal, AlertasStock.StockMinimo, AlertasStock.Pendiente),
            (alerta.IdProducto, alerta.IdSede, alerta.Tipo, alerta.Estado));
        Assert.Equal("Stock bajo: Caramelo Fresa (5 unidades, mínimo 5)", alerta.Mensaje);
        Assert.Null(alerta.FechaAtendida);
    }

    [Fact]
    public async Task Stock_un_punto_sobre_el_minimo_no_genera_alerta()
    {
        await PonerStock(_d.Caramelo, _d.ZonaA01, 6);

        await Sincronizar();

        Assert.Empty(await Alertas());
    }

    [Fact]
    public async Task Stock_en_cero_avisa_sin_stock()
    {
        await PonerStock(_d.Caramelo, _d.ZonaA01, 0);

        await Sincronizar();

        var alerta = Assert.Single(await Alertas());
        Assert.Equal("Sin stock: Caramelo Fresa (mínimo 5)", alerta.Mensaje);
        Assert.Equal(_d.SedePrincipal, alerta.IdSede);
    }

    [Fact]
    public async Task Se_compara_el_total_de_la_sede_y_no_cada_zona()
    {
        // 3 en A-01 + 3 en A-02 = 6 en la sede: sobre el mínimo (5), aunque cada zona por separado esté bajo
        await PonerStock(_d.Caramelo, _d.ZonaA01, 3);
        await using (var db = _base.NuevoContexto())
        {
            db.ProductoUbicacions.Add(new ProductoUbicacion { IdProducto = _d.Caramelo, IdUbicacion = _d.ZonaA02, CantidadActual = 3, UltimaActualizacion = DateTime.Now });
            await db.SaveChangesAsync();
        }

        await Sincronizar();

        Assert.Empty(await Alertas());
    }

    [Fact]
    public async Task Una_sede_que_no_maneja_el_producto_no_recibe_la_alerta()
    {
        // El caramelo solo tiene filas de stock en la sede Principal; la sede Norte nunca lo tuvo
        await PonerStock(_d.Caramelo, _d.ZonaA01, 2);

        await Sincronizar();

        var alertas = await Alertas();
        Assert.Single(alertas);
        Assert.Equal(_d.SedePrincipal, alertas[0].IdSede);
        Assert.DoesNotContain(alertas, a => a.IdSede == _d.SedeNorte);
    }

    [Fact]
    public async Task Un_producto_que_nunca_tuvo_stock_avisa_en_todas_las_sedes()
    {
        // Producto recién creado, sin stock en ninguna zona: hay que comprarlo, se avisa en las dos sedes
        await using (var db = _base.NuevoContexto())
        {
            db.Productos.Add(new Producto
            {
                Nombre = "Gomitas Ácidas", Tipo = "Gomita", Codigo = "PRD-003", UnidadMedida = "bolsa", PrecioUnitario = 2.00m, StockMinimo = 3
            });
            await db.SaveChangesAsync();
        }

        await Sincronizar();

        var alertas = await Alertas();
        Assert.Equal(2, alertas.Count);
        Assert.All(alertas, a => Assert.Equal("Sin stock: Gomitas Ácidas (mínimo 3)", a.Mensaje));
        Assert.Equal(new[] { _d.SedePrincipal, _d.SedeNorte }.OrderBy(x => x), alertas.Select(a => a.IdSede).OrderBy(x => x));
    }

    // ===== 2. Vencimiento =====

    [Fact]
    public async Task Vence_en_30_dias_con_stock_genera_alerta_de_vencimiento()
    {
        var vence = Hoy.AddDays(30);
        await PonerVencimiento(_d.Chocolate, vence, "L-0925");

        await Sincronizar();

        var alerta = Assert.Single(await Alertas());
        Assert.Equal((_d.Chocolate, _d.SedePrincipal, AlertasStock.Vencimiento, AlertasStock.Pendiente),
            (alerta.IdProducto, alerta.IdSede, alerta.Tipo, alerta.Estado));
        Assert.Equal($"Próximo a vencer: Chocolate Sublime (lote L-0925) – {vence:dd/MM/yyyy}", alerta.Mensaje);
    }

    [Fact]
    public async Task Vence_en_31_dias_no_genera_alerta()
    {
        await PonerVencimiento(_d.Chocolate, Hoy.AddDays(31), "L-0925");

        await Sincronizar();

        Assert.Empty(await Alertas());
    }

    [Fact]
    public async Task Un_producto_vencido_avisa_desde_cuando()
    {
        var vencio = Hoy.AddDays(-1);
        await PonerVencimiento(_d.Chocolate, vencio, null);

        await Sincronizar();

        var alerta = Assert.Single(await Alertas());
        Assert.Equal($"Vencido: Chocolate Sublime desde el {vencio:dd/MM/yyyy}", alerta.Mensaje);
    }

    [Fact]
    public async Task Vence_pronto_pero_sin_stock_no_avisa_vencimiento()
    {
        await PonerVencimiento(_d.Chocolate, Hoy.AddDays(5), "L-0925");
        await PonerStock(_d.Chocolate, _d.ZonaA02, 0);

        await Sincronizar();

        // Solo avisa que no hay stock (0 <= mínimo 2); nada de vencimiento
        Assert.Empty(await Pendientes(AlertasStock.Vencimiento));
        Assert.Equal("Sin stock: Chocolate Sublime (mínimo 2)", Assert.Single(await Alertas()).Mensaje);
    }

    // ===== 3. Sin duplicados y resolución automática =====

    [Fact]
    public async Task Sincronizar_dos_veces_seguidas_no_crea_una_segunda_alerta()
    {
        await PonerStock(_d.Caramelo, _d.ZonaA01, 5);

        await Sincronizar();
        var primera = Assert.Single(await Alertas());
        await Sincronizar();

        var alerta = Assert.Single(await Alertas());
        Assert.Equal(primera.IdAlerta, alerta.IdAlerta);
        Assert.Equal(primera.FechaGenerada, alerta.FechaGenerada);
    }

    [Fact]
    public async Task Si_el_stock_sigue_bajando_se_actualiza_el_mensaje_de_la_misma_alerta()
    {
        await PonerStock(_d.Caramelo, _d.ZonaA01, 5);
        await Sincronizar();
        var antes = Assert.Single(await Alertas());

        await PonerStock(_d.Caramelo, _d.ZonaA01, 3);
        await Sincronizar();

        var despues = Assert.Single(await Alertas());
        Assert.Equal(antes.IdAlerta, despues.IdAlerta);
        Assert.Equal("Stock bajo: Caramelo Fresa (3 unidades, mínimo 5)", despues.Mensaje);
        Assert.Equal(AlertasStock.Pendiente, despues.Estado);
    }

    [Fact]
    public async Task Al_reponer_el_stock_la_alerta_se_resuelve_sola()
    {
        await PonerStock(_d.Caramelo, _d.ZonaA01, 5);
        await Sincronizar();
        var pendiente = Assert.Single(await Alertas());

        await PonerStock(_d.Caramelo, _d.ZonaA01, 20);
        await Sincronizar();

        // Cambia estado 'pendiente' → 'atendida' y se llena fecha_atendida; nadie la atendió a mano
        var resuelta = Assert.Single(await Alertas());
        Assert.Equal(pendiente.IdAlerta, resuelta.IdAlerta);
        Assert.Equal(AlertasStock.Atendida, resuelta.Estado);
        Assert.NotNull(resuelta.FechaAtendida);
        Assert.True(resuelta.FechaAtendida >= resuelta.FechaGenerada);
        Assert.Null(resuelta.IdUsuarioAtiende);
    }

    [Fact]
    public async Task Al_vender_todo_lo_que_vencia_la_alerta_de_vencimiento_se_resuelve_sola()
    {
        await PonerVencimiento(_d.Chocolate, Hoy.AddDays(10), "L-0925");
        await Sincronizar();
        var vencimiento = Assert.Single(await Pendientes(AlertasStock.Vencimiento));

        await PonerStock(_d.Chocolate, _d.ZonaA02, 0);
        await Sincronizar();

        var resuelta = (await Alertas()).Single(a => a.IdAlerta == vencimiento.IdAlerta);
        Assert.Equal(AlertasStock.Atendida, resuelta.Estado);
        Assert.NotNull(resuelta.FechaAtendida);
        Assert.Empty(await Pendientes(AlertasStock.Vencimiento));
        Assert.Single(await Pendientes(AlertasStock.StockMinimo)); // ahora avisa que no hay stock
    }

    [Fact]
    public async Task Si_el_problema_vuelve_se_crea_una_alerta_nueva_y_la_resuelta_queda_como_historial()
    {
        await PonerStock(_d.Caramelo, _d.ZonaA01, 5);
        await Sincronizar();
        await PonerStock(_d.Caramelo, _d.ZonaA01, 20);
        await Sincronizar();
        var resuelta = Assert.Single(await Alertas());

        await PonerStock(_d.Caramelo, _d.ZonaA01, 4);
        await Sincronizar();

        var alertas = await Alertas();
        Assert.Equal(2, alertas.Count);
        var anterior = alertas.Single(a => a.IdAlerta == resuelta.IdAlerta);
        var nueva = alertas.Single(a => a.IdAlerta != resuelta.IdAlerta);
        Assert.Equal(AlertasStock.Atendida, anterior.Estado);          // no se reabre
        Assert.Equal(resuelta.FechaAtendida, anterior.FechaAtendida);
        Assert.Equal(AlertasStock.Pendiente, nueva.Estado);
        Assert.Equal("Stock bajo: Caramelo Fresa (4 unidades, mínimo 5)", nueva.Mensaje);
    }

    [Fact]
    public async Task Alertas_repetidas_de_antes_quedan_en_una_sola()
    {
        // Dos sincronizaciones al mismo tiempo pudieron dejar dos pendientes iguales
        await using (var db = _base.NuevoContexto())
        {
            db.Alerta.AddRange(
                new Alertum { IdProducto = _d.Caramelo, IdSede = _d.SedePrincipal, Tipo = AlertasStock.StockMinimo, Estado = AlertasStock.Pendiente, Mensaje = "vieja", FechaGenerada = DateTime.Now.AddMinutes(-10) },
                new Alertum { IdProducto = _d.Caramelo, IdSede = _d.SedePrincipal, Tipo = AlertasStock.StockMinimo, Estado = AlertasStock.Pendiente, Mensaje = "repetida", FechaGenerada = DateTime.Now.AddMinutes(-5) });
            await db.SaveChangesAsync();
        }
        await PonerStock(_d.Caramelo, _d.ZonaA01, 5);

        await Sincronizar();

        var alertas = await Alertas();
        var pendiente = Assert.Single(alertas, a => a.Estado == AlertasStock.Pendiente);
        Assert.Equal("Stock bajo: Caramelo Fresa (5 unidades, mínimo 5)", pendiente.Mensaje);  // se queda la más antigua
        Assert.Equal(alertas.Min(a => a.FechaGenerada), pendiente.FechaGenerada);
        Assert.Single(alertas, a => a.Estado == AlertasStock.Atendida);                           // la repetida se cierra
    }

    // ===== 4. Una vez por minuto (SincronizarSiToca / Invalidar) =====
    // El reloj no se puede controlar sin tocar el sistema: se prueba con el estado real
    // ("recién sincronizado") y el paso del minuto se simula moviendo la marca interna con reflexión.

    [Fact]
    public async Task Dentro_del_minuto_SincronizarSiToca_no_vuelve_a_calcular()
    {
        await Sincronizar();                                   // recién sincronizado: sin alertas
        await PonerStock(_d.Caramelo, _d.ZonaA01, 5);          // cambio que no avisa (directo en la base)

        await SincronizarSiToca();

        Assert.Empty(await Alertas());
    }

    [Fact]
    public async Task Invalidar_hace_que_la_siguiente_llamada_si_calcule()
    {
        await Sincronizar();
        await PonerStock(_d.Caramelo, _d.ZonaA01, 5);
        await SincronizarSiToca();
        Assert.Empty(await Alertas());

        AlertasStock.Invalidar();
        await SincronizarSiToca();

        Assert.Single(await Alertas());
    }

    [Fact]
    public async Task Pasado_el_minuto_SincronizarSiToca_vuelve_a_calcular()
    {
        await Sincronizar();
        await PonerStock(_d.Caramelo, _d.ZonaA01, 5);

        var marca = typeof(AlertasStock).GetField("_ultimaSincronizacion", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(marca);
        marca!.SetValue(null, DateTime.UtcNow.AddMinutes(-2).Ticks);   // la última fue hace 2 minutos
        await SincronizarSiToca();

        Assert.Single(await Alertas());
    }

    [Fact]
    public async Task Cambiar_el_stock_con_el_sistema_avisa_a_las_alertas_sin_esperar_el_minuto()
    {
        await Sincronizar();

        // Guardar por el sistema (SaveChanges de AppDbContext) llama solo a Invalidar
        await using (var db = _base.NuevoContexto())
        {
            var fila = await db.ProductoUbicacions.SingleAsync(pu => pu.IdProducto == _d.Caramelo && pu.IdUbicacion == _d.ZonaA01);
            fila.CantidadActual = 5;
            await db.SaveChangesAsync();
        }
        await SincronizarSiToca();

        Assert.Single(await Alertas());
    }
}
