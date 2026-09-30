using System.Security.Claims;
using GestionAlmacen_Golocentro.Controllers;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.Services;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Tests.Integracion;

// Sede de cada operación. La encargada y el trabajador tienen una sede asignada (claim SedeId); la dueña no
// tiene y opera en todas. Se prueba la regla central (OperacionesAlmacen.ResolverSedeMovimiento), que al
// guardar no quede nada, y los filtros de lo que cada uno ve y puede tocar.
// Además de la semilla: 10 caramelos en N-01 (sede Norte) y un proveedor para las entradas.
[Collection(ColeccionBaseDeDatos.Nombre)]
public class SedeTests : IAsyncLifetime
{
    private const string NoEsTuSede = "Las ubicaciones seleccionadas no pertenecen a tu sede.";
    private const string DosSedes = "Todos los productos de un movimiento deben estar en ubicaciones de la misma sede. Registra un movimiento por cada sede.";
    private const string ZonaInvalida = "Selecciona una ubicación válida para cada producto.";

    private readonly BaseDePruebas _base;
    private Semilla.Datos _d = null!;
    private int _proveedor;

    public SedeTests(BaseDePruebas baseDePruebas) => _base = baseDePruebas;

    public async Task InitializeAsync()
    {
        await _base.LimpiarAsync();
        await using var db = _base.NuevoContexto();
        _d = await Semilla.CrearAsync(db);
        db.ProductoUbicacions.Add(new ProductoUbicacion { IdProducto = _d.Caramelo, IdUbicacion = _d.ZonaN01, CantidadActual = 10, UltimaActualizacion = DateTime.Now });
        var proveedor = new Proveedor { Nombre = "Distribuidora de Prueba", Ruc = "20123456789" };
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync();
        _proveedor = proveedor.IdProveedor;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ===== Ayudas =====

    private ClaimsPrincipal Duena => Sesion.Usuario(_d.Duena, "duena", null);
    private ClaimsPrincipal TrabajadorPrincipal => Sesion.Usuario(_d.TrabajadorPrincipal, "trabajador", _d.SedePrincipal);
    private ClaimsPrincipal TrabajadorNorte => Sesion.Usuario(_d.TrabajadorNorte, "trabajador", _d.SedeNorte);

    private async Task<(int? Sede, string? Error)> Resolver(int? sedeUsuario, params int[] zonas)
    {
        await using var db = _base.NuevoContexto();
        return await OperacionesAlmacen.ResolverSedeMovimiento(db, zonas, sedeUsuario);
    }

    private async Task<int> Stock(int idProducto, int idZona)
    {
        await using var db = _base.NuevoContexto();
        return await db.ProductoUbicacions.Where(pu => pu.IdProducto == idProducto && pu.IdUbicacion == idZona)
            .Select(pu => pu.CantidadActual).SingleOrDefaultAsync();
    }

    private async Task<(int Movimientos, int Detalles, int Notas, int Traslados)> Registros()
    {
        await using var db = _base.NuevoContexto();
        return (await db.Movimientos.CountAsync(), await db.DetalleMovimientos.CountAsync(),
            await db.NotaVenta.CountAsync(), await db.Traslados.CountAsync());
    }

    private async Task<(IActionResult Resultado, VentaController Controlador)> Vender(ClaimsPrincipal usuario, params (int Producto, int Zona, int Cantidad)[] lineas)
    {
        var db = _base.NuevoContexto();
        var ventas = Sesion.Preparar(new VentaController(db), usuario);
        var formulario = new VentaFormViewModel { ClienteId = _d.ClienteGeneral, MetodoPago = "efectivo" };
        formulario.Items.AddRange(lineas.Select(l => new VentaItem { ProductoId = l.Producto, UbicacionId = l.Zona, Cantidad = l.Cantidad }));
        return (await ventas.Nueva(formulario), ventas);
    }

    private async Task<(IActionResult Resultado, MovimientoController Controlador)> RegistrarEntrada(ClaimsPrincipal usuario, int producto, int zona, int cantidad)
    {
        var db = _base.NuevoContexto();
        var movimientos = Sesion.Preparar(new MovimientoController(db), usuario);
        var formulario = new MovimientoEntradaViewModel { ProveedorId = _proveedor, NumeroFactura = "F001-123" };
        formulario.Detalles.Add(new DetalleEntradaViewModel { ProductoId = producto, UbicacionId = zona, Cantidad = cantidad });
        return (await movimientos.Entrada(formulario), movimientos);
    }

    private static void AssertError(Controller controlador, string campo, string mensaje)
    {
        Assert.False(controlador.ModelState.IsValid);
        Assert.Contains(controlador.ModelState[campo]!.Errors, e => e.ErrorMessage == mensaje);
    }

    // ===== 1. Regla central: OperacionesAlmacen.ResolverSedeMovimiento =====

    [Fact]
    public async Task Zonas_de_la_sede_del_usuario_se_aceptan_y_devuelven_esa_sede()
    {
        Assert.Equal((_d.SedePrincipal, (string?)null), await Resolver(_d.SedePrincipal, _d.ZonaA01));
        Assert.Equal((_d.SedePrincipal, (string?)null), await Resolver(_d.SedePrincipal, _d.ZonaA01, _d.ZonaA02));
        Assert.Equal((_d.SedeNorte, (string?)null), await Resolver(_d.SedeNorte, _d.ZonaN01));
    }

    [Fact]
    public async Task Usuario_de_la_sede_Principal_con_zona_de_la_Norte_se_rechaza()
    {
        Assert.Equal(((int?)null, NoEsTuSede), await Resolver(_d.SedePrincipal, _d.ZonaN01));
    }

    [Fact]
    public async Task Usuario_de_la_sede_Norte_con_zona_de_la_Principal_se_rechaza()
    {
        Assert.Equal(((int?)null, NoEsTuSede), await Resolver(_d.SedeNorte, _d.ZonaA01));
    }

    [Fact]
    public async Task La_duena_opera_en_cualquier_sede_y_se_resuelve_la_de_la_zona_elegida()
    {
        Assert.Equal((_d.SedeNorte, (string?)null), await Resolver(null, _d.ZonaN01));
        Assert.Equal((_d.SedePrincipal, (string?)null), await Resolver(null, _d.ZonaA02));
    }

    [Fact]
    public async Task Zonas_de_dos_sedes_en_un_mismo_movimiento_se_rechazan_tambien_para_la_duena()
    {
        Assert.Equal(((int?)null, DosSedes), await Resolver(null, _d.ZonaA01, _d.ZonaN01));
    }

    [Fact]
    public async Task Una_zona_que_no_existe_se_rechaza()
    {
        Assert.Equal(((int?)null, ZonaInvalida), await Resolver(null, 99999));
        Assert.Equal(((int?)null, ZonaInvalida), await Resolver(_d.SedePrincipal, _d.ZonaA01, 99999));
    }

    [Fact]
    public async Task Una_zona_de_una_sede_marcada_inactiva_hoy_se_acepta_porque_el_sistema_no_usa_ese_estado()
    {
        // Las zonas no tienen estado. La sede sí tiene la columna estado (activo/inactivo) en la base,
        // pero el sistema solo la llena con "activo" al crearla y no la revisa en ningún lado.
        await using (var db = _base.NuevoContexto())
            await db.Sedes.Where(s => s.IdSede == _d.SedeNorte).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, "inactivo"));

        Assert.Equal((_d.SedeNorte, (string?)null), await Resolver(null, _d.ZonaN01));
    }

    // ===== 2. Al guardar: una zona de otra sede no deja nada =====

    [Fact]
    public async Task Venta_de_un_trabajador_con_zona_de_otra_sede_no_guarda_nada()
    {
        var (resultado, ventas) = await Vender(TrabajadorPrincipal, (_d.Caramelo, _d.ZonaN01, 2));

        Assert.IsType<ViewResult>(resultado);
        AssertError(ventas, "", NoEsTuSede);
        Assert.Equal((0, 0, 0, 0), await Registros());
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaN01));
    }

    [Fact]
    public async Task Venta_de_un_trabajador_de_la_Norte_con_zona_de_la_Principal_no_guarda_nada()
    {
        var (resultado, ventas) = await Vender(TrabajadorNorte, (_d.Caramelo, _d.ZonaA01, 2));

        Assert.IsType<ViewResult>(resultado);
        AssertError(ventas, "", NoEsTuSede);
        Assert.Equal((0, 0, 0, 0), await Registros());
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    [Fact]
    public async Task Venta_con_zonas_de_dos_sedes_no_guarda_nada()
    {
        var (resultado, ventas) = await Vender(Duena, (_d.Caramelo, _d.ZonaA01, 2), (_d.Caramelo, _d.ZonaN01, 2));

        Assert.IsType<ViewResult>(resultado);
        AssertError(ventas, "", DosSedes);
        Assert.Equal((0, 0, 0, 0), await Registros());
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaN01));
    }

    [Fact]
    public async Task Entrada_de_un_trabajador_con_zona_de_otra_sede_no_guarda_nada()
    {
        var (resultado, movimientos) = await RegistrarEntrada(TrabajadorPrincipal, _d.Chocolate, _d.ZonaN01, 5);

        Assert.IsType<ViewResult>(resultado);
        AssertError(movimientos, "", NoEsTuSede);
        Assert.Equal((0, 0, 0, 0), await Registros());
        await using var db = _base.NuevoContexto();
        Assert.False(await db.ProductoUbicacions.AnyAsync(pu => pu.IdProducto == _d.Chocolate && pu.IdUbicacion == _d.ZonaN01));
    }

    [Fact]
    public async Task La_duena_puede_vender_desde_la_sede_Norte_y_la_venta_queda_en_esa_sede()
    {
        var (resultado, _) = await Vender(Duena, (_d.Caramelo, _d.ZonaN01, 2));

        Assert.IsType<RedirectToActionResult>(resultado);
        await using var db = _base.NuevoContexto();
        Assert.Equal(_d.SedeNorte, (await db.Movimientos.SingleAsync()).IdSede);
        Assert.Equal(8, await Stock(_d.Caramelo, _d.ZonaN01));
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaA01));
    }

    [Fact]
    public async Task Una_entrada_del_trabajador_en_su_propia_sede_si_se_registra()
    {
        var (resultado, _) = await RegistrarEntrada(TrabajadorNorte, _d.Chocolate, _d.ZonaN01, 5);

        Assert.IsType<RedirectToActionResult>(resultado);
        await using var db = _base.NuevoContexto();
        var entrada = await db.Movimientos.SingleAsync();
        Assert.Equal(("Entrada", _d.SedeNorte, _proveedor), (entrada.Tipo, entrada.IdSede, entrada.IdProveedor));
        Assert.Equal(5, await Stock(_d.Chocolate, _d.ZonaN01));
    }

    [Fact]
    public async Task Un_trabajador_no_puede_mover_mercaderia_desde_una_zona_de_otra_sede()
    {
        await using var db = _base.NuevoContexto();
        var mapa = Sesion.Preparar(new MapaController(db), TrabajadorPrincipal);

        // Aunque pida la sede Norte, a un trabajador de la Principal solo se le cargan las zonas de la Principal
        var resultado = await mapa.Mover(new MoverFormViewModel
        {
            Sede = _d.SedeNorte, ProductoId = _d.Caramelo, OrigenId = _d.ZonaN01, DestinoId = _d.ZonaA02, Cantidad = 2
        });

        Assert.IsType<ViewResult>(resultado);
        AssertError(mapa, nameof(MoverFormViewModel.OrigenId), "Esa zona no pertenece a esta sede.");
        Assert.Equal((0, 0, 0, 0), await Registros());
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaN01));
    }

    [Fact]
    public async Task Un_trabajador_no_puede_abrir_ni_guardar_el_conteo_de_una_zona_de_otra_sede()
    {
        await using var db = _base.NuevoContexto();
        var inventario = Sesion.Preparar(new InventarioController(db), TrabajadorPrincipal);

        Assert.IsType<NotFoundResult>(await inventario.Contar(_d.ZonaN01));
        Assert.IsType<NotFoundResult>(await inventario.Contar(_d.ZonaN01, new ConteoFormViewModel { Motivo = "conteo" }));

        await using var lectura = _base.NuevoContexto();
        Assert.Equal(0, await lectura.AjusteInventarios.CountAsync());
        Assert.Equal(10, await Stock(_d.Caramelo, _d.ZonaN01));
    }

    // ===== 3. Lo que cada uno ve: los formularios y listas se filtran por la sede del usuario =====

    [Fact]
    public async Task La_venta_de_un_trabajador_solo_ofrece_zonas_con_stock_de_su_sede()
    {
        async Task<List<int>> ZonasOfrecidas(ClaimsPrincipal usuario)
        {
            await using var db = _base.NuevoContexto();
            var ventas = Sesion.Preparar(new VentaController(db), usuario);
            var modelo = Assert.IsType<VentaFormViewModel>(Assert.IsType<ViewResult>(await ventas.Nueva()).Model);
            return modelo.Datos.Productos.SelectMany(p => p.Zonas).Select(z => z.Id).OrderBy(x => x).ToList();
        }

        Assert.Equal(new[] { _d.ZonaA01, _d.ZonaA02 }.OrderBy(x => x), await ZonasOfrecidas(TrabajadorPrincipal));
        Assert.Equal(new[] { _d.ZonaN01 }, await ZonasOfrecidas(TrabajadorNorte));
        Assert.Equal(new[] { _d.ZonaA01, _d.ZonaA02, _d.ZonaN01 }.OrderBy(x => x), await ZonasOfrecidas(Duena));
    }

    [Fact]
    public async Task La_entrada_de_un_trabajador_solo_ofrece_zonas_de_su_sede()
    {
        async Task<List<int>> ZonasOfrecidas(ClaimsPrincipal usuario)
        {
            await using var db = _base.NuevoContexto();
            var movimientos = Sesion.Preparar(new MovimientoController(db), usuario);
            var modelo = Assert.IsType<MovimientoEntradaViewModel>(Assert.IsType<ViewResult>(await movimientos.Entrada()).Model);
            return modelo.Datos.Zonas.Select(z => z.Id).OrderBy(x => x).ToList();
        }

        Assert.Equal(new[] { _d.ZonaA01, _d.ZonaA02 }.OrderBy(x => x), await ZonasOfrecidas(TrabajadorPrincipal));
        Assert.Equal(new[] { _d.ZonaN01 }, await ZonasOfrecidas(TrabajadorNorte));
        Assert.Equal(3, (await ZonasOfrecidas(Duena)).Count);
    }

    [Fact]
    public async Task Un_trabajador_no_ve_las_ventas_de_otra_sede()
    {
        await Vender(Duena, (_d.Caramelo, _d.ZonaA01, 1));   // venta en la sede Principal

        async Task<int> VentasVisibles(ClaimsPrincipal usuario)
        {
            await using var db = _base.NuevoContexto();
            var ventas = Sesion.Preparar(new VentaController(db), usuario);
            return Assert.IsType<VentasIndexViewModel>(Assert.IsType<ViewResult>(await ventas.Index(null, null, null)).Model).Filas.Count;
        }

        Assert.Equal(1, await VentasVisibles(TrabajadorPrincipal));
        Assert.Equal(0, await VentasVisibles(TrabajadorNorte));
        Assert.Equal(1, await VentasVisibles(Duena));
    }
}
