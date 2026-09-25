using System;
using System.Collections.Generic;
using GestionAlmacen_Golocentro.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AjusteInventario> AjusteInventarios { get; set; }

    public virtual DbSet<Alertum> Alerta { get; set; }

    public virtual DbSet<Camara> Camaras { get; set; }

    public virtual DbSet<Cliente> Clientes { get; set; }

    public virtual DbSet<DetalleAjuste> DetalleAjustes { get; set; }

    public virtual DbSet<DetalleMovimiento> DetalleMovimientos { get; set; }

    public virtual DbSet<DetalleTraslado> DetalleTraslados { get; set; }

    public virtual DbSet<Evidencium> Evidencia { get; set; }

    public virtual DbSet<Movimiento> Movimientos { get; set; }

    public virtual DbSet<PlanoLinea> PlanoLineas { get; set; }

    public virtual DbSet<Producto> Productos { get; set; }

    public virtual DbSet<ProductoUbicacion> ProductoUbicacions { get; set; }

    public virtual DbSet<Proveedor> Proveedors { get; set; }

    public virtual DbSet<Reporte> Reportes { get; set; }

    public virtual DbSet<Sede> Sedes { get; set; }

    public virtual DbSet<Traslado> Traslados { get; set; }

    public virtual DbSet<Ubicacion> Ubicacions { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AjusteInventario>(entity =>
        {
            entity.HasKey(e => e.IdAjuste).HasName("ajuste_inventario_pkey");

            entity.ToTable("ajuste_inventario");

            entity.Property(e => e.IdAjuste)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_ajuste");
            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("fecha");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.Motivo)
                .HasMaxLength(20)
                .HasColumnName("motivo");
            entity.Property(e => e.Observaciones)
                .HasMaxLength(200)
                .HasColumnName("observaciones");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.AjusteInventarios)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ajuste_inventario_id_sede_fkey");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.AjusteInventarios)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ajuste_inventario_id_usuario_fkey");
        });

        modelBuilder.Entity<Alertum>(entity =>
        {
            entity.HasKey(e => e.IdAlerta).HasName("alerta_pkey");

            entity.ToTable("alerta");

            entity.Property(e => e.IdAlerta)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_alerta");
            entity.Property(e => e.Estado)
                .HasMaxLength(15)
                .HasDefaultValueSql("'pendiente'::character varying")
                .HasColumnName("estado");
            entity.Property(e => e.FechaAtendida)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("fecha_atendida");
            entity.Property(e => e.FechaGenerada)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("fecha_generada");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.IdUsuarioAtiende).HasColumnName("id_usuario_atiende");
            entity.Property(e => e.Mensaje).HasColumnName("mensaje");
            entity.Property(e => e.Tipo)
                .HasMaxLength(20)
                .HasColumnName("tipo");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.Alerta)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("alerta_id_producto_fkey");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Alerta)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("alerta_id_sede_fkey");

            entity.HasOne(d => d.IdUsuarioAtiendeNavigation).WithMany(p => p.Alerta)
                .HasForeignKey(d => d.IdUsuarioAtiende)
                .HasConstraintName("alerta_id_usuario_atiende_fkey");
        });

        modelBuilder.Entity<Camara>(entity =>
        {
            entity.HasKey(e => e.IdCamara).HasName("camara_pkey");

            entity.ToTable("camara");

            entity.HasIndex(e => e.Codigo, "camara_codigo_key").IsUnique();

            entity.Property(e => e.IdCamara)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_camara");
            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .HasColumnName("codigo");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(200)
                .HasColumnName("descripcion");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.IdUbicacion).HasColumnName("id_ubicacion");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Camaras)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("camara_id_sede_fkey");

            entity.HasOne(d => d.IdUbicacionNavigation).WithMany(p => p.Camaras)
                .HasForeignKey(d => d.IdUbicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("camara_id_ubicacion_fkey");
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.IdCliente).HasName("cliente_pkey");

            entity.ToTable("cliente");

            entity.HasIndex(e => e.RucDni, "cliente_ruc_dni_key").IsUnique();

            entity.Property(e => e.IdCliente)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_cliente");
            entity.Property(e => e.Contacto)
                .HasMaxLength(100)
                .HasColumnName("contacto");
            entity.Property(e => e.Direccion)
                .HasMaxLength(200)
                .HasColumnName("direccion");
            entity.Property(e => e.Nombre)
                .HasMaxLength(150)
                .HasColumnName("nombre");
            entity.Property(e => e.RucDni)
                .HasMaxLength(20)
                .HasColumnName("ruc_dni");
        });

        modelBuilder.Entity<DetalleAjuste>(entity =>
        {
            entity.HasKey(e => e.IdDetalle).HasName("detalle_ajuste_pkey");

            entity.ToTable("detalle_ajuste");

            entity.HasIndex(e => new { e.IdAjuste, e.IdProducto, e.IdUbicacion }, "uq_detalleajuste_producto").IsUnique();

            entity.Property(e => e.IdDetalle)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_detalle");
            entity.Property(e => e.CantidadAnterior).HasColumnName("cantidad_anterior");
            entity.Property(e => e.CantidadNueva).HasColumnName("cantidad_nueva");
            entity.Property(e => e.IdAjuste).HasColumnName("id_ajuste");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdUbicacion).HasColumnName("id_ubicacion");

            entity.HasOne(d => d.IdAjusteNavigation).WithMany(p => p.DetalleAjustes)
                .HasForeignKey(d => d.IdAjuste)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_ajuste_id_ajuste_fkey");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.DetalleAjustes)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_ajuste_id_producto_fkey");

            entity.HasOne(d => d.IdUbicacionNavigation).WithMany(p => p.DetalleAjustes)
                .HasForeignKey(d => d.IdUbicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_ajuste_id_ubicacion_fkey");
        });

        modelBuilder.Entity<DetalleMovimiento>(entity =>
        {
            entity.HasKey(e => e.IdDetalle).HasName("detalle_movimiento_pkey");

            entity.ToTable("detalle_movimiento");

            entity.Property(e => e.IdDetalle)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_detalle");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad");
            entity.Property(e => e.IdMovimiento).HasColumnName("id_movimiento");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdUbicacion).HasColumnName("id_ubicacion");
            entity.Property(e => e.PrecioUnitarioSnapshot)
                .HasPrecision(10, 2)
                .HasColumnName("precio_unitario_snapshot");
            entity.Property(e => e.StockAnterior).HasColumnName("stock_anterior");

            entity.HasOne(d => d.IdMovimientoNavigation).WithMany(p => p.DetalleMovimientos)
                .HasForeignKey(d => d.IdMovimiento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_movimiento_id_movimiento_fkey");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.DetalleMovimientos)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_movimiento_id_producto_fkey");

            entity.HasOne(d => d.IdUbicacionNavigation).WithMany(p => p.DetalleMovimientos)
                .HasForeignKey(d => d.IdUbicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_movimiento_id_ubicacion_fkey");
        });

        modelBuilder.Entity<DetalleTraslado>(entity =>
        {
            entity.HasKey(e => e.IdDetalle).HasName("detalle_traslado_pkey");

            entity.ToTable("detalle_traslado");

            entity.Property(e => e.IdDetalle)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_detalle");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdTraslado).HasColumnName("id_traslado");
            entity.Property(e => e.IdUbicacionDestino).HasColumnName("id_ubicacion_destino");
            entity.Property(e => e.IdUbicacionOrigen).HasColumnName("id_ubicacion_origen");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.DetalleTraslados)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_traslado_id_producto_fkey");

            entity.HasOne(d => d.IdTrasladoNavigation).WithMany(p => p.DetalleTraslados)
                .HasForeignKey(d => d.IdTraslado)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_traslado_id_traslado_fkey");

            entity.HasOne(d => d.IdUbicacionDestinoNavigation).WithMany(p => p.DetalleTrasladoIdUbicacionDestinoNavigations)
                .HasForeignKey(d => d.IdUbicacionDestino)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_traslado_destino_fkey");

            entity.HasOne(d => d.IdUbicacionOrigenNavigation).WithMany(p => p.DetalleTrasladoIdUbicacionOrigenNavigations)
                .HasForeignKey(d => d.IdUbicacionOrigen)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detalle_traslado_origen_fkey");
        });

        modelBuilder.Entity<Evidencium>(entity =>
        {
            entity.HasKey(e => e.IdEvidencia).HasName("evidencia_pkey");

            entity.ToTable("evidencia");

            entity.Property(e => e.IdEvidencia)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_evidencia");
            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("fecha");
            entity.Property(e => e.IdCamara).HasColumnName("id_camara");
            entity.Property(e => e.IdDetalle).HasColumnName("id_detalle");
            entity.Property(e => e.IdMovimiento).HasColumnName("id_movimiento");
            entity.Property(e => e.Tipo)
                .HasMaxLength(50)
                .HasColumnName("tipo");
            entity.Property(e => e.UrlArchivo)
                .HasMaxLength(500)
                .HasColumnName("url_archivo");

            entity.HasOne(d => d.IdCamaraNavigation).WithMany(p => p.Evidencia)
                .HasForeignKey(d => d.IdCamara)
                .HasConstraintName("evidencia_id_camara_fkey");

            entity.HasOne(d => d.IdDetalleNavigation).WithMany(p => p.Evidencia)
                .HasForeignKey(d => d.IdDetalle)
                .HasConstraintName("evidencia_id_detalle_fkey");

            entity.HasOne(d => d.IdMovimientoNavigation).WithMany(p => p.Evidencia)
                .HasForeignKey(d => d.IdMovimiento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("evidencia_id_movimiento_fkey");
        });

        modelBuilder.Entity<Movimiento>(entity =>
        {
            entity.HasKey(e => e.IdMovimiento).HasName("movimiento_pkey");

            entity.ToTable("movimiento");

            entity.Property(e => e.IdMovimiento)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_movimiento");
            entity.Property(e => e.ComprobanteEmitido)
                .HasDefaultValue(false)
                .HasColumnName("comprobante_emitido");
            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("fecha");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdProveedor).HasColumnName("id_proveedor");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.Observaciones).HasColumnName("observaciones");
            entity.Property(e => e.Tipo)
                .HasMaxLength(20)
                .HasColumnName("tipo");

            entity.HasOne(d => d.IdClienteNavigation).WithMany(p => p.Movimientos)
                .HasForeignKey(d => d.IdCliente)
                .HasConstraintName("movimiento_id_cliente_fkey");

            entity.HasOne(d => d.IdProveedorNavigation).WithMany(p => p.Movimientos)
                .HasForeignKey(d => d.IdProveedor)
                .HasConstraintName("movimiento_id_proveedor_fkey");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Movimientos)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("movimiento_id_sede_fkey");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Movimientos)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("movimiento_id_usuario_fkey");
        });

        modelBuilder.Entity<PlanoLinea>(entity =>
        {
            entity.HasKey(e => e.IdLinea).HasName("plano_linea_pkey");

            entity.ToTable("plano_linea");

            entity.Property(e => e.IdLinea)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_linea");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.Tipo)
                .HasMaxLength(15)
                .HasColumnName("tipo");
            entity.Property(e => e.X1)
                .HasPrecision(5, 1)
                .HasColumnName("x1");
            entity.Property(e => e.X2)
                .HasPrecision(5, 1)
                .HasColumnName("x2");
            entity.Property(e => e.Y1)
                .HasPrecision(5, 1)
                .HasColumnName("y1");
            entity.Property(e => e.Y2)
                .HasPrecision(5, 1)
                .HasColumnName("y2");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.PlanoLineas)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("plano_linea_id_sede_fkey");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.IdProducto).HasName("producto_pkey");

            entity.ToTable("producto");

            entity.HasIndex(e => e.Codigo, "producto_codigo_key").IsUnique();

            entity.Property(e => e.IdProducto)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_producto");
            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .HasColumnName("codigo");
            entity.Property(e => e.FechaVencimiento).HasColumnName("fecha_vencimiento");
            entity.Property(e => e.Lote)
                .HasMaxLength(50)
                .HasColumnName("lote");
            entity.Property(e => e.Nombre)
                .HasMaxLength(150)
                .HasColumnName("nombre");
            entity.Property(e => e.PrecioUnitario)
                .HasPrecision(10, 2)
                .HasColumnName("precio_unitario");
            entity.Property(e => e.StockActual)
                .HasDefaultValue(0)
                .HasColumnName("stock_actual");
            entity.Property(e => e.StockMinimo)
                .HasDefaultValue(0)
                .HasColumnName("stock_minimo");
            entity.Property(e => e.Tipo)
                .HasMaxLength(50)
                .HasColumnName("tipo");
            entity.Property(e => e.UnidadMedida)
                .HasMaxLength(20)
                .HasColumnName("unidad_medida");
        });

        modelBuilder.Entity<ProductoUbicacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("producto_ubicacion_pkey");

            entity.ToTable("producto_ubicacion");

            entity.HasIndex(e => new { e.IdProducto, e.IdUbicacion }, "uq_producto_ubicacion").IsUnique();

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CantidadActual)
                .HasDefaultValue(0)
                .HasColumnName("cantidad_actual");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdUbicacion).HasColumnName("id_ubicacion");
            entity.Property(e => e.UltimaActualizacion)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("ultima_actualizacion");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.ProductoUbicacions)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("producto_ubicacion_id_producto_fkey");

            entity.HasOne(d => d.IdUbicacionNavigation).WithMany(p => p.ProductoUbicacions)
                .HasForeignKey(d => d.IdUbicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("producto_ubicacion_id_ubicacion_fkey");
        });

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.HasKey(e => e.IdProveedor).HasName("proveedor_pkey");

            entity.ToTable("proveedor");

            entity.HasIndex(e => e.Ruc, "proveedor_ruc_key").IsUnique();

            entity.Property(e => e.IdProveedor)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_proveedor");
            entity.Property(e => e.Contacto)
                .HasMaxLength(100)
                .HasColumnName("contacto");
            entity.Property(e => e.Direccion)
                .HasMaxLength(200)
                .HasColumnName("direccion");
            entity.Property(e => e.Nombre)
                .HasMaxLength(150)
                .HasColumnName("nombre");
            entity.Property(e => e.Ruc)
                .HasMaxLength(20)
                .HasColumnName("ruc");
        });

        modelBuilder.Entity<Reporte>(entity =>
        {
            entity.HasKey(e => e.IdReporte).HasName("reporte_pkey");

            entity.ToTable("reporte");

            entity.Property(e => e.IdReporte)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_reporte");
            entity.Property(e => e.FechaGeneracion)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("fecha_generacion");
            entity.Property(e => e.FiltrosAplicados).HasColumnName("filtros_aplicados");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.Tipo)
                .HasMaxLength(50)
                .HasColumnName("tipo");
            entity.Property(e => e.UrlExportacion)
                .HasMaxLength(500)
                .HasColumnName("url_exportacion");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Reportes)
                .HasForeignKey(d => d.IdSede)
                .HasConstraintName("reporte_id_sede_fkey");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Reportes)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("reporte_id_usuario_fkey");
        });

        modelBuilder.Entity<Sede>(entity =>
        {
            entity.HasKey(e => e.IdSede).HasName("sede_pkey");

            entity.ToTable("sede");

            entity.Property(e => e.IdSede)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_sede");
            entity.Property(e => e.Ciudad)
                .HasMaxLength(80)
                .HasDefaultValueSql("'Huamanga'::character varying")
                .HasColumnName("ciudad");
            entity.Property(e => e.Direccion)
                .HasMaxLength(200)
                .HasColumnName("direccion");
            entity.Property(e => e.Estado)
                .HasMaxLength(10)
                .HasDefaultValueSql("'activo'::character varying")
                .HasColumnName("estado");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
            entity.Property(e => e.PlanoAlto)
                .HasPrecision(5, 1)
                .HasColumnName("plano_alto");
            entity.Property(e => e.PlanoAncho)
                .HasPrecision(5, 1)
                .HasColumnName("plano_ancho");
        });

        modelBuilder.Entity<Traslado>(entity =>
        {
            entity.HasKey(e => e.IdTraslado).HasName("traslado_pkey");

            entity.ToTable("traslado");

            entity.Property(e => e.IdTraslado)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_traslado");
            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("fecha");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.Observaciones)
                .HasMaxLength(200)
                .HasColumnName("observaciones");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Traslados)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("traslado_id_sede_fkey");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Traslados)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("traslado_id_usuario_fkey");
        });

        modelBuilder.Entity<Ubicacion>(entity =>
        {
            entity.HasKey(e => e.IdUbicacion).HasName("ubicacion_pkey");

            entity.ToTable("ubicacion");

            entity.HasIndex(e => e.CodigoEstante, "ubicacion_codigo_estante_key").IsUnique();

            entity.Property(e => e.IdUbicacion)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_ubicacion");
            entity.Property(e => e.Alto)
                .HasPrecision(5, 1)
                .HasColumnName("alto");
            entity.Property(e => e.Ancho)
                .HasPrecision(5, 1)
                .HasColumnName("ancho");
            entity.Property(e => e.Capacidad).HasColumnName("capacidad");
            entity.Property(e => e.CodigoEstante)
                .HasMaxLength(20)
                .HasColumnName("codigo_estante");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(200)
                .HasColumnName("descripcion");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.PosX)
                .HasPrecision(5, 1)
                .HasColumnName("pos_x");
            entity.Property(e => e.PosY)
                .HasPrecision(5, 1)
                .HasColumnName("pos_y");
            entity.Property(e => e.Tipo)
                .HasMaxLength(15)
                .HasDefaultValueSql("'almacenaje'::character varying")
                .HasColumnName("tipo");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Ubicacions)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ubicacion_id_sede_fkey");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("usuario_pkey");

            entity.ToTable("usuario");

            entity.HasIndex(e => e.Usuario1, "usuario_usuario_key").IsUnique();

            entity.Property(e => e.IdUsuario)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id_usuario");
            entity.Property(e => e.Contrasena)
                .HasMaxLength(255)
                .HasColumnName("contrasena");
            entity.Property(e => e.Estado)
                .HasMaxLength(10)
                .HasDefaultValueSql("'activo'::character varying")
                .HasColumnName("estado");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("fecha_creacion");
            entity.Property(e => e.FotoUrl)
                .HasMaxLength(255)
                .HasColumnName("foto_url");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
            entity.Property(e => e.Rol)
                .HasMaxLength(20)
                .HasColumnName("rol");
            entity.Property(e => e.Usuario1)
                .HasMaxLength(50)
                .HasColumnName("usuario");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.IdSede)
                .HasConstraintName("usuario_id_sede_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
