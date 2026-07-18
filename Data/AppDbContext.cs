using System;
using System.Collections.Generic;
using GestionAlmacen_Golocentro.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Alertum> Alerta { get; set; }

    public virtual DbSet<Camara> Camaras { get; set; }

    public virtual DbSet<Cliente> Clientes { get; set; }

    public virtual DbSet<DetalleMovimiento> DetalleMovimientos { get; set; }

    public virtual DbSet<Evidencium> Evidencia { get; set; }

    public virtual DbSet<Movimiento> Movimientos { get; set; }

    public virtual DbSet<Producto> Productos { get; set; }

    public virtual DbSet<ProductoUbicacion> ProductoUbicacions { get; set; }

    public virtual DbSet<Proveedor> Proveedores { get; set; }

    public virtual DbSet<Reporte> Reportes { get; set; }

    public virtual DbSet<Sede> Sedes { get; set; }

    public virtual DbSet<Ubicacion> Ubicaciones { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alertum>(entity =>
        {
            entity.HasKey(e => e.IdAlerta).HasName("PK__Alerta__1227953E4C300724");

            entity.Property(e => e.IdAlerta).HasColumnName("id_alerta");
            entity.Property(e => e.Estado)
                .HasMaxLength(15)
                .HasDefaultValue("pendiente")
                .HasColumnName("estado");
            entity.Property(e => e.FechaAtendida).HasColumnName("fecha_atendida");
            entity.Property(e => e.FechaGenerada)
                .HasDefaultValueSql("(sysdatetime())")
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
                .HasConstraintName("FK_Alerta_Producto");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Alerta)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Alerta_Sede");

            entity.HasOne(d => d.IdUsuarioAtiendeNavigation).WithMany(p => p.Alerta)
                .HasForeignKey(d => d.IdUsuarioAtiende)
                .HasConstraintName("FK_Alerta_Usuario");
        });

        modelBuilder.Entity<Camara>(entity =>
        {
            entity.HasKey(e => e.IdCamara).HasName("PK__Camara__CB0FB4DB70147C2C");

            entity.ToTable("Camara");

            entity.HasIndex(e => e.Codigo, "UQ__Camara__40F9A2063111D4B6").IsUnique();

            entity.Property(e => e.IdCamara).HasColumnName("id_camara");
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
                .HasConstraintName("FK_Camara_Sede");

            entity.HasOne(d => d.IdUbicacionNavigation).WithMany(p => p.Camaras)
                .HasForeignKey(d => d.IdUbicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Camara_Ubicacion");
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.IdCliente).HasName("PK__Cliente__677F38F5FBAA93A3");

            entity.ToTable("Cliente");

            entity.HasIndex(e => e.RucDni, "UQ__Cliente__BAE363B0F0293B86").IsUnique();

            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
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

        modelBuilder.Entity<DetalleMovimiento>(entity =>
        {
            entity.HasKey(e => e.IdDetalle).HasName("PK__DetalleM__4F1332DE728D46B2");

            entity.ToTable("DetalleMovimiento");

            entity.Property(e => e.IdDetalle).HasColumnName("id_detalle");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad");
            entity.Property(e => e.IdMovimiento).HasColumnName("id_movimiento");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdUbicacion).HasColumnName("id_ubicacion");
            entity.Property(e => e.PrecioUnitarioSnapshot)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("precio_unitario_snapshot");
            entity.Property(e => e.StockAnterior).HasColumnName("stock_anterior");

            entity.HasOne(d => d.IdMovimientoNavigation).WithMany(p => p.DetalleMovimientos)
                .HasForeignKey(d => d.IdMovimiento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DetalleMovimiento_Movimiento");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.DetalleMovimientos)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DetalleMovimiento_Producto");

            entity.HasOne(d => d.IdUbicacionNavigation).WithMany(p => p.DetalleMovimientos)
                .HasForeignKey(d => d.IdUbicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DetalleMovimiento_Ubicacion");
        });

        modelBuilder.Entity<Evidencium>(entity =>
        {
            entity.HasKey(e => e.IdEvidencia).HasName("PK__Evidenci__62875FB95B67FAF0");

            entity.Property(e => e.IdEvidencia).HasColumnName("id_evidencia");
            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("(sysdatetime())")
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
                .HasConstraintName("FK_Evidencia_Camara");

            entity.HasOne(d => d.IdDetalleNavigation).WithMany(p => p.Evidencia)
                .HasForeignKey(d => d.IdDetalle)
                .HasConstraintName("FK_Evidencia_DetalleMovimiento");

            entity.HasOne(d => d.IdMovimientoNavigation).WithMany(p => p.Evidencia)
                .HasForeignKey(d => d.IdMovimiento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Evidencia_Movimiento");
        });

        modelBuilder.Entity<Movimiento>(entity =>
        {
            entity.HasKey(e => e.IdMovimiento).HasName("PK__Movimien__2A071C24D7F6B4BB");

            entity.ToTable("Movimiento");

            entity.Property(e => e.IdMovimiento).HasColumnName("id_movimiento");
            entity.Property(e => e.ComprobanteEmitido).HasColumnName("comprobante_emitido");
            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("(sysdatetime())")
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
                .HasConstraintName("FK_Movimiento_Cliente");

            entity.HasOne(d => d.IdProveedorNavigation).WithMany(p => p.Movimientos)
                .HasForeignKey(d => d.IdProveedor)
                .HasConstraintName("FK_Movimiento_Proveedor");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Movimientos)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Movimiento_Sede");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Movimientos)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Movimiento_Usuario");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.IdProducto).HasName("PK__Producto__FF341C0DBD0BB93D");

            entity.ToTable("Producto");

            entity.HasIndex(e => e.Codigo, "UQ__Producto__40F9A20657184105").IsUnique();

            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
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
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("precio_unitario");
            entity.Property(e => e.StockActual).HasColumnName("stock_actual");
            entity.Property(e => e.StockMinimo).HasColumnName("stock_minimo");
            entity.Property(e => e.Tipo)
                .HasMaxLength(50)
                .HasColumnName("tipo");
            entity.Property(e => e.UnidadMedida)
                .HasMaxLength(20)
                .HasColumnName("unidad_medida");
        });

        modelBuilder.Entity<ProductoUbicacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Producto__3213E83F5E1E686F");

            entity.ToTable("ProductoUbicacion");

            entity.HasIndex(e => new { e.IdProducto, e.IdUbicacion }, "UQ_ProductoUbicacion").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CantidadActual).HasColumnName("cantidad_actual");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdUbicacion).HasColumnName("id_ubicacion");
            entity.Property(e => e.UltimaActualizacion)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("ultima_actualizacion");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.ProductoUbicacions)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductoUbicacion_Producto");

            entity.HasOne(d => d.IdUbicacionNavigation).WithMany(p => p.ProductoUbicacions)
                .HasForeignKey(d => d.IdUbicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductoUbicacion_Ubicacion");
        });

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.HasKey(e => e.IdProveedor).HasName("PK__Proveedo__8D3DFE28DB6DC92E");

            entity.ToTable("Proveedor");

            entity.HasIndex(e => e.Ruc, "UQ__Proveedo__C2B74E619588A6B9").IsUnique();

            entity.Property(e => e.IdProveedor).HasColumnName("id_proveedor");
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
            entity.HasKey(e => e.IdReporte).HasName("PK__Reporte__87E4F5CB5CE19525");

            entity.ToTable("Reporte");

            entity.Property(e => e.IdReporte).HasColumnName("id_reporte");
            entity.Property(e => e.FechaGeneracion)
                .HasDefaultValueSql("(sysdatetime())")
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
                .HasConstraintName("FK_Reporte_Sede");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Reportes)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reporte_Usuario");
        });

        modelBuilder.Entity<Sede>(entity =>
        {
            entity.HasKey(e => e.IdSede).HasName("PK__Sede__D693504B762C7B31");

            entity.ToTable("Sede");

            entity.Property(e => e.IdSede).HasColumnName("id_sede");
            entity.Property(e => e.Ciudad)
                .HasMaxLength(80)
                .HasDefaultValue("Huamanga")
                .HasColumnName("ciudad");
            entity.Property(e => e.Direccion)
                .HasMaxLength(200)
                .HasColumnName("direccion");
            entity.Property(e => e.Estado)
                .HasMaxLength(10)
                .HasDefaultValue("activo")
                .HasColumnName("estado");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<Ubicacion>(entity =>
        {
            entity.HasKey(e => e.IdUbicacion).HasName("PK__Ubicacio__81BAA591335FDF8D");

            entity.ToTable("Ubicacion");

            entity.HasIndex(e => e.CodigoEstante, "UQ__Ubicacio__C20D4E361AE763BB").IsUnique();

            entity.Property(e => e.IdUbicacion).HasColumnName("id_ubicacion");
            entity.Property(e => e.Capacidad).HasColumnName("capacidad");
            entity.Property(e => e.CodigoEstante)
                .HasMaxLength(20)
                .HasColumnName("codigo_estante");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(200)
                .HasColumnName("descripcion");
            entity.Property(e => e.IdSede).HasColumnName("id_sede");

            entity.HasOne(d => d.IdSedeNavigation).WithMany(p => p.Ubicacions)
                .HasForeignKey(d => d.IdSede)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Ubicacion_Sede");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PK__Usuario__4E3E04AD7D620393");

            entity.ToTable("Usuario");

            entity.HasIndex(e => e.Usuario1, "UQ__Usuario__9AFF8FC662AB68B8").IsUnique();

            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.Contrasena)
                .HasMaxLength(255)
                .HasColumnName("contrasena");
            entity.Property(e => e.Estado)
                .HasMaxLength(10)
                .HasDefaultValue("activo")
                .HasColumnName("estado");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("fecha_creacion");
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
                .HasConstraintName("FK_Usuario_Sede");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
