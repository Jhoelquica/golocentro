using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Control de concurrencia del stock (xmin de PostgreSQL, configurado en Data/AppDbContext.Aliases.cs).
// Sin él, dos personas que venden, reciben o mueven el mismo producto de la misma zona a la vez
// leen la misma cantidad y la segunda pisa el cambio de la primera.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class ConcurrenciaTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;

    public ConcurrenciaTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public Task InitializeAsync() => _base.LimpiarAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static Task<ProductoUbicacion> Fila(AppDbContext db, Semilla.Datos d) =>
        db.ProductoUbicacions.SingleAsync(pu => pu.IdProducto == d.Caramelo && pu.IdUbicacion == d.ZonaA01);

    private async Task<int> StockActual(Semilla.Datos d)
    {
        await using var db = _base.NuevoContexto();
        return (await Fila(db, d)).CantidadActual;
    }

    [Fact]
    public async Task Dos_operaciones_sobre_la_misma_zona_la_segunda_falla_y_el_stock_no_queda_pisado()
    {
        Semilla.Datos d;
        await using (var db = _base.NuevoContexto())
            d = await Semilla.CrearAsync(db);

        // Las dos personas abren la venta cuando hay 10 caramelos en A-01
        await using var primera = _base.NuevoContexto();
        await using var segunda = _base.NuevoContexto();
        var filaPrimera = await Fila(primera, d);
        var filaSegunda = await Fila(segunda, d);
        Assert.Equal(10, filaPrimera.CantidadActual);
        Assert.Equal(10, filaSegunda.CantidadActual);

        // La primera vende 3 y guarda
        filaPrimera.CantidadActual -= 3;
        await primera.SaveChangesAsync();

        // La segunda vende 5 calculando sobre los 10 que leyó
        filaSegunda.CantidadActual -= 5;
        var rechazo = await Record.ExceptionAsync(() => segunda.SaveChangesAsync());

        // Quedan 7 (10 - 3), no 5 (lo que habría dejado la segunda pisando a la primera)...
        Assert.Equal(7, await StockActual(d));
        // ...porque la segunda se rechazó
        Assert.IsType<DbUpdateConcurrencyException>(rechazo);
    }

    [Fact]
    public async Task Tras_el_rechazo_se_reintenta_con_el_stock_nuevo_y_se_descuentan_las_dos_ventas()
    {
        Semilla.Datos d;
        await using (var db = _base.NuevoContexto())
            d = await Semilla.CrearAsync(db);

        await using var primera = _base.NuevoContexto();
        await using var segunda = _base.NuevoContexto();
        var filaPrimera = await Fila(primera, d);
        var filaSegunda = await Fila(segunda, d);

        filaPrimera.CantidadActual -= 3;
        await primera.SaveChangesAsync();

        filaSegunda.CantidadActual -= 5;
        var rechazo = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => segunda.SaveChangesAsync());

        // Se vuelve a leer la cantidad actual (7) y se aplica la venta de 5 sobre ella
        await rechazo.Entries.Single().ReloadAsync();
        Assert.Equal(7, filaSegunda.CantidadActual);
        filaSegunda.CantidadActual -= 5;
        await segunda.SaveChangesAsync();

        Assert.Equal(2, await StockActual(d));
    }

    [Fact]
    public async Task Una_venta_que_choca_con_otra_operacion_se_rechaza_sin_guardar_nada()
    {
        Semilla.Datos d;
        await using (var db = _base.NuevoContexto())
            d = await Semilla.CrearAsync(db);
        int idFila;
        await using (var db = _base.NuevoContexto())
            idFila = (await Fila(db, d)).Id;

        // Justo antes de que la venta guarde, otra persona se lleva 1 caramelo de la misma zona
        var otraOperacion = new OperacionSimultanea(_base, idFila);
        await using var db2 = _base.NuevoContexto(otraOperacion);
        var ventas = Sesion.Preparar(new VentaController(db2), Sesion.Usuario(d.Duena, "duena", null));

        var resultado = await ventas.Nueva(new VentaFormViewModel
        {
            ClienteId = d.ClienteGeneral,
            MetodoPago = "efectivo",
            Items = { new VentaItem { ProductoId = d.Caramelo, UbicacionId = d.ZonaA01, Cantidad = 4 } }
        });

        Assert.True(otraOperacion.Ocurrio);

        // El stock refleja solo la otra operación (10 - 1 = 9); sin protección la venta habría escrito 6 (10 - 4)
        // y el caramelo que se llevó la otra persona desaparecería de las cuentas
        Assert.Equal(9, await StockActual(d));

        // No quedó ni la venta ni su nota
        await using var lectura = _base.NuevoContexto();
        Assert.Equal(0, await lectura.Movimientos.CountAsync());
        Assert.Equal(0, await lectura.NotaVenta.CountAsync());

        // La venta vuelve al formulario con el aviso para volver a intentarlo
        Assert.IsType<ViewResult>(resultado);
        Assert.False(ventas.ModelState.IsValid);
        Assert.Contains(ventas.ModelState[""]!.Errors,
            e => e.ErrorMessage == "El stock cambió mientras registrabas la venta. Revisa las cantidades y vuelve a intentarlo.");
    }

    // Simula a otra persona: cambia el stock por su cuenta, justo cuando el sistema está por guardar
    private sealed class OperacionSimultanea : SaveChangesInterceptor
    {
        private readonly BaseDePruebas _base;
        private readonly int _idFila;

        public OperacionSimultanea(BaseDePruebas baseDePruebas, int idFila)
        {
            _base = baseDePruebas;
            _idFila = idFila;
        }

        public bool Ocurrio { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Ocurrio)
            {
                Ocurrio = true;
                await using var otra = _base.NuevoContexto();
                await otra.ProductoUbicacions
                    .Where(pu => pu.Id == _idFila)
                    .ExecuteUpdateAsync(s => s.SetProperty(pu => pu.CantidadActual, pu => pu.CantidadActual - 1), cancellationToken);
            }
            return result;
        }
    }
}
