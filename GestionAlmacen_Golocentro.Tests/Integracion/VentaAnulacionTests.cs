using System.Data.Common;
using System.Security.Claims;
using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Ventas (VentaController.Nueva) y anulaciones (VentaController.Anular) de punta a punta:
// controlador real, base PostgreSQL real con el esquema del sistema.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class VentaAnulacionTests : IAsyncLifetime
{
    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;

    public VentaAnulacionTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ===== Ayudas =====

    private ClaimsPrincipal Duena => Sesion.Usuario(_d.Duena, "duena", null);
    private ClaimsPrincipal TrabajadorPrincipal => Sesion.Usuario(_d.TrabajadorPrincipal, "trabajador", _d.SedePrincipal);
    private ClaimsPrincipal TrabajadorNorte => Sesion.Usuario(_d.TrabajadorNorte, "trabajador", _d.SedeNorte);

    private VentaItem Caramelo(int cantidad) => new() { ProductoId = _d.Caramelo, UbicacionId = _d.ZonaA01, Cantidad = cantidad };
    private VentaItem Chocolate(int cantidad) => new() { ProductoId = _d.Chocolate, UbicacionId = _d.ZonaA02, Cantidad = cantidad };

    // Cada llamada usa su propio contexto, como una petición real
    private async Task<(IActionResult Resultado, VentaController Controlador)> Vender(ClaimsPrincipal usuario, params VentaItem[] items)
    {
        var db = _base.NuevoContexto();
        var ventas = Sesion.Preparar(new VentaController(db), usuario);
        var formulario = new VentaFormViewModel { ClienteId = _d.ClienteGeneral, MetodoPago = "efectivo" };
        formulario.Items.AddRange(items);
        return (await ventas.Nueva(formulario), ventas);
    }

    private async Task<int> VenderBien(ClaimsPrincipal usuario, params VentaItem[] items)
    {
        var (resultado, _) = await Vender(usuario, items);
        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(VentaController.Nota), redireccion.ActionName);
        return (int)redireccion.RouteValues!["id"]!;
    }

    private async Task<(IActionResult Resultado, VentaController Controlador)> Anular(ClaimsPrincipal usuario, int idVenta, string? motivo, params IInterceptor[] interceptores)
    {
        var db = _base.NuevoContexto(interceptores);
        var ventas = Sesion.Preparar(new VentaController(db), usuario);
        return (await ventas.Anular(idVenta, motivo), ventas);
    }

    private async Task<int> Stock(int idProducto, int idZona)
    {
        await using var db = _base.NuevoContexto();
        return await db.ProductoUbicacions.Where(pu => pu.IdProducto == idProducto && pu.IdUbicacion == idZona)
            .Select(pu => pu.CantidadActual).SingleAsync();
    }

    private async Task<NotaVentum> Nota(int idVenta)
    {
        await using var db = _base.NuevoContexto();
        return await db.NotaVenta.AsNoTracking().SingleAsync(n => n.IdMovimiento == idVenta);
    }

    private async Task<(int Movimientos, int Detalles, int Notas)> Registros()
    {
        await using var db = _base.NuevoContexto();
        return (await db.Movimientos.CountAsync(), await db.DetalleMovimientos.CountAsync(), await db.NotaVenta.CountAsync());
    }

    private static void AssertErrorDeFormulario(VentaController ventas, string mensaje)
    {
        Assert.False(ventas.ModelState.IsValid);
        Assert.Contains(ventas.ModelState[""]!.Errors, e => e.ErrorMessage == mensaje);
    }

    // ===== 1. La venta descuenta stock =====

    [Fact]
    public async Task Venta_con_stock_suficiente_guarda_nota_y_detalle_y_descuenta_cada_zona()
    {
        var (resultado, ventas) = await Vender(Duena, Caramelo(3), Chocolate(2));

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        var idVenta = (int)redireccion.RouteValues!["id"]!;
        Assert.StartsWith("Venta registrada: NV01-000001", (string)ventas.TempData["Exito"]!);

        await using var db = _base.NuevoContexto();
        var movimiento = await db.Movimientos.Include(m => m.DetalleMovimientos).SingleAsync();
        Assert.Equal(idVenta, movimiento.IdMovimiento);
        Assert.Equal("Salida", movimiento.Tipo);
        Assert.Equal(_d.SedePrincipal, movimiento.IdSede);
        Assert.Equal(_d.ClienteGeneral, movimiento.IdCliente);
        Assert.Equal(_d.Duena, movimiento.IdUsuario);

        // El detalle guarda cuánto había antes y el precio cobrado
        Assert.Equal(2, movimiento.DetalleMovimientos.Count);
        var caramelo = movimiento.DetalleMovimientos.Single(x => x.IdProducto == _d.Caramelo);
        Assert.Equal((3, 0.30m, 10, _d.ZonaA01), (caramelo.Cantidad, caramelo.PrecioUnitarioSnapshot, caramelo.StockAnterior, caramelo.IdUbicacion));
        var chocolate = movimiento.DetalleMovimientos.Single(x => x.IdProducto == _d.Chocolate);
        Assert.Equal((2, 1.50m, 20, _d.ZonaA02), (chocolate.Cantidad, chocolate.PrecioUnitarioSnapshot, chocolate.StockAnterior, chocolate.IdUbicacion));

        // La nota: 3 × 0.30 + 2 × 1.50 = 3.90
        var nota = await db.NotaVenta.SingleAsync();
        Assert.Equal(("NV01", 1, EstadoNota.Emitida, "efectivo"), (nota.Serie, nota.Numero, nota.Estado, nota.MetodoPago));
        Assert.Equal((3.90m, 0m, 3.90m), (nota.Subtotal, nota.Descuento, nota.Total));

        // Stock final de cada zona
        Assert.Equal(7, await Stock(_d.Caramelo, _d.ZonaA01));
        Assert.Equal(18, await Stock(_d.Chocolate, _d.ZonaA02));
    }

    [Fact]
    public async Task Venta_que_pide_mas_de_lo_que_hay_se_rechaza_y_no_guarda_nada()
    {
        var (resultado, ventas) = await Vender(Duena, Caramelo(11));

        Assert.IsType<ViewResult>(resultado);
        AssertErrorDeFormulario(ventas, "No alcanza el stock de Caramelo Fresa en esa zona: hay 10, pediste 11.");
        Assert.Equal((0, 0, 0), await Registros());
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    [Fact]
    public async Task Si_una_sola_linea_no_alcanza_no_se_guarda_ninguna()
    {
        // El chocolate sí alcanza (5 de 20); el caramelo no (11 de 10)
        var (resultado, ventas) = await Vender(Duena, Chocolate(5), Caramelo(11));

        Assert.IsType<ViewResult>(resultado);
        AssertErrorDeFormulario(ventas, "No alcanza el stock de Caramelo Fresa en esa zona: hay 10, pediste 11.");
        Assert.Equal((0, 0, 0), await Registros());
        Assert.Equal(20, await Stock(_d.Chocolate, _d.ZonaA02));
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    [Fact]
    public async Task Dos_lineas_del_mismo_producto_y_zona_se_suman_para_validar_el_stock()
    {
        // 6 y 5 pasan cada una por separado, pero juntas son 11 y solo hay 10
        var (resultado, ventas) = await Vender(Duena, Caramelo(6), Caramelo(5));

        Assert.IsType<ViewResult>(resultado);
        AssertErrorDeFormulario(ventas, "No alcanza el stock de Caramelo Fresa en esa zona: hay 10, pediste 11.");
        Assert.Equal((0, 0, 0), await Registros());
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    [Fact]
    public async Task Ventas_seguidas_llevan_numeros_correlativos_sin_repetirse()
    {
        var ids = new List<int>();
        for (var i = 0; i < 3; i++)
            ids.Add(await VenderBien(Duena, Caramelo(1)));

        var numeros = new List<string>();
        foreach (var id in ids)
        {
            var nota = await Nota(id);
            numeros.Add(VentaController.NumeroNota(nota.Serie, nota.Numero));
        }

        Assert.Equal(new[] { "NV01-000001", "NV01-000002", "NV01-000003" }, numeros);
        Assert.Equal(7, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    // ===== 2. Anulación =====

    [Fact]
    public async Task Anular_devuelve_el_stock_a_su_zona_y_marca_la_nota_como_anulada()
    {
        var idVenta = await VenderBien(Duena, Caramelo(3));
        Assert.Equal(7, await Stock(_d.Caramelo, _d.ZonaA01));

        var (resultado, ventas) = await Anular(Duena, idVenta, "  El cliente devolvió los caramelos  ");

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(VentaController.Nota), redireccion.ActionName);
        Assert.Equal("Venta NV01-000001 anulada. Los productos volvieron al stock de sus zonas.", ventas.TempData["Exito"]);
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));

        // Datos de la anulación (chk_notaventa_anulacion) y montos intactos (chk_notaventa_montos)
        var nota = await Nota(idVenta);
        Assert.Equal(EstadoNota.Anulada, nota.Estado);
        Assert.NotNull(nota.FechaAnulacion);
        Assert.Equal(_d.Duena, nota.IdUsuarioAnulacion);
        Assert.Equal("El cliente devolvió los caramelos", nota.MotivoAnulacion);
        Assert.Equal((0.90m, 0m, 0.90m), (nota.Subtotal, nota.Descuento, nota.Total));

        // La nota no se borra: el movimiento y su detalle siguen para la historia (kardex)
        Assert.Equal((1, 1, 1), await Registros());
    }

    [Fact]
    public async Task La_nota_anulada_deja_de_sumar_en_la_lista_de_ventas_y_en_el_reporte()
    {
        var idCaramelos = await VenderBien(Duena, Caramelo(3));   // 0.90
        await VenderBien(Duena, Chocolate(2));                     // 3.00

        var antesLista = await ListaDeVentas();
        var antesReporte = await ReporteDeVentas();
        Assert.Equal((2, 0, 3.90m), (antesLista.Ventas, antesLista.Anuladas, antesLista.Total));
        Assert.Equal((2, 0, 3.90m), (antesReporte.Ventas, antesReporte.Anuladas, antesReporte.Total));

        await Anular(Duena, idCaramelos, "Nota hecha por error");

        var lista = await ListaDeVentas();
        Assert.Equal((1, 1, 3.00m), (lista.Ventas, lista.Anuladas, lista.Total));
        Assert.Equal(2, lista.Filas.Count);                          // se sigue listando...
        Assert.True(lista.Filas.Single(f => f.Total == 0.90m).Anulada); // ...marcada como anulada
        Assert.Equal(3.00m, Assert.Single(lista.PorMetodo).Total);

        var reporte = await ReporteDeVentas();
        Assert.Equal((1, 1, 3.00m), (reporte.Ventas, reporte.Anuladas, reporte.Total));
        Assert.Equal(3.00m, reporte.TicketPromedio);
        Assert.Equal(3.00m, Assert.Single(reporte.PorVendedor).Total);
    }

    [Fact]
    public async Task Anular_dos_veces_la_misma_nota_no_devuelve_el_stock_dos_veces()
    {
        var idVenta = await VenderBien(Duena, Caramelo(3));
        await Anular(Duena, idVenta, "Primera anulación");
        var primera = await Nota(idVenta);

        var (resultado, ventas) = await Anular(Duena, idVenta, "Segunda anulación");

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("La venta NV01-000001 ya estaba anulada.", ventas.TempData["Error"]);
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));       // no 13
        var nota = await Nota(idVenta);
        Assert.Equal("Primera anulación", nota.MotivoAnulacion);        // no la pisa
        Assert.Equal(primera.FechaAnulacion, nota.FechaAnulacion);
    }

    [Fact]
    public async Task Dos_anulaciones_al_mismo_tiempo_devuelven_el_stock_una_sola_vez()
    {
        var idVenta = await VenderBien(Duena, Caramelo(3));
        int idFila;
        await using (var db = _base.NuevoContexto())
            idFila = await db.ProductoUbicacions.Where(pu => pu.IdProducto == _d.Caramelo && pu.IdUbicacion == _d.ZonaA01)
                .Select(pu => pu.Id).SingleAsync();

        // Las dos ven la nota "emitida"; justo antes de que esta la marque, la otra termina su anulación completa
        var otra = new OtraAnulacion(_base, idVenta, idFila, cantidad: 3, _d.TrabajadorPrincipal);
        var (resultado, ventas) = await Anular(Duena, idVenta, "Anulada aquí", otra);

        Assert.True(otra.Ocurrio);
        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("La venta NV01-000001 ya estaba anulada.", ventas.TempData["Error"]);
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));       // devuelto una vez (no 13)
        Assert.Equal("Anulada desde otro equipo", (await Nota(idVenta)).MotivoAnulacion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Anular_sin_motivo_no_cambia_nada(string? motivo)
    {
        var idVenta = await VenderBien(Duena, Caramelo(3));

        var (_, ventas) = await Anular(Duena, idVenta, motivo);

        Assert.Equal("Escribe el motivo de la anulación.", ventas.TempData["Error"]);
        Assert.Equal(EstadoNota.Emitida, (await Nota(idVenta)).Estado);
        Assert.Equal(7, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    [Fact]
    public async Task Un_motivo_de_mas_de_200_caracteres_no_se_acepta()
    {
        var idVenta = await VenderBien(Duena, Caramelo(3));

        var (_, ventas) = await Anular(Duena, idVenta, new string('x', 201));

        Assert.Equal("El motivo no puede pasar de 200 caracteres.", ventas.TempData["Error"]);
        Assert.Equal(EstadoNota.Emitida, (await Nota(idVenta)).Estado);
        Assert.Equal(7, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    [Fact]
    public async Task Un_trabajador_puede_anular_una_venta_de_su_sede()
    {
        var idVenta = await VenderBien(TrabajadorPrincipal, Caramelo(3));

        var (resultado, ventas) = await Anular(TrabajadorPrincipal, idVenta, "Venta cancelada");

        Assert.Equal(nameof(VentaController.Nota), Assert.IsType<RedirectToActionResult>(resultado).ActionName);
        Assert.NotNull(ventas.TempData["Exito"]);
        Assert.Equal(EstadoNota.Anulada, (await Nota(idVenta)).Estado);
        Assert.Equal(_d.TrabajadorPrincipal, (await Nota(idVenta)).IdUsuarioAnulacion);
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    [Fact]
    public async Task Un_trabajador_de_otra_sede_no_puede_anular()
    {
        var idVenta = await VenderBien(Duena, Caramelo(3));   // venta en la sede Principal

        var (resultado, _) = await Anular(TrabajadorNorte, idVenta, "Intento desde la sede Norte");

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(("AccessDenied", "Account"), (redireccion.ActionName, redireccion.ControllerName));
        Assert.Equal(EstadoNota.Emitida, (await Nota(idVenta)).Estado);
        Assert.Equal(7, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    [Fact]
    public async Task Una_nota_anulada_no_libera_su_numero()
    {
        var primera = await VenderBien(Duena, Caramelo(1));
        await Anular(Duena, primera, "Error de digitación");

        var segunda = await VenderBien(Duena, Caramelo(1));

        var nota = await Nota(segunda);
        Assert.Equal("NV01-000002", VentaController.NumeroNota(nota.Serie, nota.Numero));
    }

    // ===== Totales con los mismos cálculos que usa el sistema =====

    private async Task<VentasIndexViewModel> ListaDeVentas()
    {
        await using var db = _base.NuevoContexto();
        var ventas = Sesion.Preparar(new VentaController(db), Duena);
        return Assert.IsType<VentasIndexViewModel>(Assert.IsType<ViewResult>(await ventas.Index(null, null, null)).Model);
    }

    private async Task<ReporteVentasViewModel> ReporteDeVentas()
    {
        await using var db = _base.NuevoContexto();
        var reportes = Sesion.Preparar(new ReporteController(db), Duena);
        return Assert.IsType<ReporteVentasViewModel>(Assert.IsType<ViewResult>(await reportes.Ventas(null, null, null, null)).Model);
    }

    // Simula a otra persona que anula la misma venta y termina justo antes de que esta marque la nota
    private sealed class OtraAnulacion : DbCommandInterceptor
    {
        private readonly BaseDePruebas _base;
        private readonly int _idVenta, _idFila, _cantidad, _idUsuario;

        public OtraAnulacion(BaseDePruebas baseDePruebas, int idVenta, int idFila, int cantidad, int idUsuario)
        {
            _base = baseDePruebas;
            (_idVenta, _idFila, _cantidad, _idUsuario) = (idVenta, idFila, cantidad, idUsuario);
        }

        public bool Ocurrio { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Ocurrio && command.CommandText.Contains("UPDATE") && command.CommandText.Contains("nota_venta"))
            {
                Ocurrio = true;
                await using var otra = _base.NuevoContexto();
                await otra.NotaVenta.Where(n => n.IdMovimiento == _idVenta).ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.Estado, EstadoNota.Anulada)
                    .SetProperty(n => n.FechaAnulacion, DateTime.Now)
                    .SetProperty(n => n.IdUsuarioAnulacion, _idUsuario)
                    .SetProperty(n => n.MotivoAnulacion, "Anulada desde otro equipo"), cancellationToken);
                await otra.ProductoUbicacions.Where(pu => pu.Id == _idFila)
                    .ExecuteUpdateAsync(s => s.SetProperty(pu => pu.CantidadActual, pu => pu.CantidadActual + _cantidad), cancellationToken);
            }
            return result;
        }
    }
}
