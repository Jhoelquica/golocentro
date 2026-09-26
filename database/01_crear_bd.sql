-- =====================================================================
-- Crea la base de datos GestionAlmacen_Golocentro con todas sus tablas
-- (vacias). Usalo en una PC nueva donde todavia no existe la BD.
--
-- Generado desde el modelo de EF Core (Data/AppDbContext.cs) con:
--     dotnet ef dbcontext script
--
-- Como ejecutarlo: en SSMS abre este archivo y pulsa F5 (Ejecutar).
-- Despues ejecuta 02_datos_prueba.sql.
--
-- Para empezar de cero si ya la creaste antes:
--     DROP DATABASE GestionAlmacen_Golocentro;
--
-- (Archivo sin tildes a proposito: asi se lee igual con cualquier
-- codificacion.)
-- =====================================================================

IF DB_ID(N'GestionAlmacen_Golocentro') IS NULL
    CREATE DATABASE GestionAlmacen_Golocentro;
GO

USE GestionAlmacen_Golocentro;
GO

CREATE TABLE [Cliente] (
    [id_cliente] int NOT NULL IDENTITY,
    [nombre] nvarchar(150) NOT NULL,
    [ruc_dni] nvarchar(20) NOT NULL,
    [contacto] nvarchar(100) NULL,
    [direccion] nvarchar(200) NULL,
    CONSTRAINT [PK__Cliente__677F38F5FBAA93A3] PRIMARY KEY ([id_cliente])
);
GO


CREATE TABLE [Producto] (
    [id_producto] int NOT NULL IDENTITY,
    [nombre] nvarchar(150) NOT NULL,
    [tipo] nvarchar(50) NOT NULL,
    [codigo] nvarchar(50) NOT NULL,
    [unidad_medida] nvarchar(20) NOT NULL,
    [precio_unitario] decimal(10,2) NOT NULL,
    [stock_actual] int NOT NULL,
    [stock_minimo] int NOT NULL,
    [fecha_vencimiento] date NULL,
    [lote] nvarchar(50) NULL,
    CONSTRAINT [PK__Producto__FF341C0DBD0BB93D] PRIMARY KEY ([id_producto])
);
GO


CREATE TABLE [Proveedor] (
    [id_proveedor] int NOT NULL IDENTITY,
    [nombre] nvarchar(150) NOT NULL,
    [ruc] nvarchar(20) NOT NULL,
    [contacto] nvarchar(100) NULL,
    [direccion] nvarchar(200) NULL,
    CONSTRAINT [PK__Proveedo__8D3DFE28DB6DC92E] PRIMARY KEY ([id_proveedor])
);
GO


CREATE TABLE [Sede] (
    [id_sede] int NOT NULL IDENTITY,
    [nombre] nvarchar(100) NOT NULL,
    [direccion] nvarchar(200) NOT NULL,
    [ciudad] nvarchar(80) NOT NULL DEFAULT N'Huamanga',
    [estado] nvarchar(10) NOT NULL DEFAULT N'activo',
    CONSTRAINT [PK__Sede__D693504B762C7B31] PRIMARY KEY ([id_sede])
);
GO


CREATE TABLE [Ubicacion] (
    [id_ubicacion] int NOT NULL IDENTITY,
    [codigo_estante] nvarchar(20) NOT NULL,
    [descripcion] nvarchar(200) NULL,
    [capacidad] int NOT NULL,
    [id_sede] int NOT NULL,
    CONSTRAINT [PK__Ubicacio__81BAA591335FDF8D] PRIMARY KEY ([id_ubicacion]),
    CONSTRAINT [FK_Ubicacion_Sede] FOREIGN KEY ([id_sede]) REFERENCES [Sede] ([id_sede])
);
GO


CREATE TABLE [Usuario] (
    [id_usuario] int NOT NULL IDENTITY,
    [nombre] nvarchar(100) NOT NULL,
    [rol] nvarchar(20) NOT NULL,
    [usuario] nvarchar(50) NOT NULL,
    [contrasena] nvarchar(255) NOT NULL,
    [estado] nvarchar(10) NOT NULL DEFAULT N'activo',
    [fecha_creacion] datetime2 NOT NULL DEFAULT ((sysdatetime())),
    [id_sede] int NULL,
    [FotoUrl] nvarchar(max) NULL,
    CONSTRAINT [PK__Usuario__4E3E04AD7D620393] PRIMARY KEY ([id_usuario]),
    CONSTRAINT [FK_Usuario_Sede] FOREIGN KEY ([id_sede]) REFERENCES [Sede] ([id_sede])
);
GO


CREATE TABLE [Camara] (
    [id_camara] int NOT NULL IDENTITY,
    [codigo] nvarchar(50) NOT NULL,
    [descripcion] nvarchar(200) NULL,
    [id_ubicacion] int NOT NULL,
    [id_sede] int NOT NULL,
    CONSTRAINT [PK__Camara__CB0FB4DB70147C2C] PRIMARY KEY ([id_camara]),
    CONSTRAINT [FK_Camara_Sede] FOREIGN KEY ([id_sede]) REFERENCES [Sede] ([id_sede]),
    CONSTRAINT [FK_Camara_Ubicacion] FOREIGN KEY ([id_ubicacion]) REFERENCES [Ubicacion] ([id_ubicacion])
);
GO


CREATE TABLE [ProductoUbicacion] (
    [id] int NOT NULL IDENTITY,
    [id_producto] int NOT NULL,
    [id_ubicacion] int NOT NULL,
    [cantidad_actual] int NOT NULL,
    [ultima_actualizacion] datetime2 NOT NULL DEFAULT ((sysdatetime())),
    CONSTRAINT [PK__Producto__3213E83F5E1E686F] PRIMARY KEY ([id]),
    CONSTRAINT [FK_ProductoUbicacion_Producto] FOREIGN KEY ([id_producto]) REFERENCES [Producto] ([id_producto]),
    CONSTRAINT [FK_ProductoUbicacion_Ubicacion] FOREIGN KEY ([id_ubicacion]) REFERENCES [Ubicacion] ([id_ubicacion])
);
GO


CREATE TABLE [Alerta] (
    [id_alerta] int NOT NULL IDENTITY,
    [id_producto] int NOT NULL,
    [tipo] nvarchar(20) NOT NULL,
    [mensaje] nvarchar(max) NOT NULL,
    [fecha_generada] datetime2 NOT NULL DEFAULT ((sysdatetime())),
    [estado] nvarchar(15) NOT NULL DEFAULT N'pendiente',
    [id_usuario_atiende] int NULL,
    [fecha_atendida] datetime2 NULL,
    [id_sede] int NOT NULL,
    CONSTRAINT [PK__Alerta__1227953E4C300724] PRIMARY KEY ([id_alerta]),
    CONSTRAINT [FK_Alerta_Producto] FOREIGN KEY ([id_producto]) REFERENCES [Producto] ([id_producto]),
    CONSTRAINT [FK_Alerta_Sede] FOREIGN KEY ([id_sede]) REFERENCES [Sede] ([id_sede]),
    CONSTRAINT [FK_Alerta_Usuario] FOREIGN KEY ([id_usuario_atiende]) REFERENCES [Usuario] ([id_usuario])
);
GO


CREATE TABLE [Movimiento] (
    [id_movimiento] int NOT NULL IDENTITY,
    [tipo] nvarchar(20) NOT NULL,
    [fecha] datetime2 NOT NULL DEFAULT ((sysdatetime())),
    [id_usuario] int NOT NULL,
    [id_cliente] int NULL,
    [id_proveedor] int NULL,
    [comprobante_emitido] bit NOT NULL,
    [observaciones] nvarchar(max) NULL,
    [id_sede] int NULL,
    CONSTRAINT [PK__Movimien__2A071C24D7F6B4BB] PRIMARY KEY ([id_movimiento]),
    CONSTRAINT [FK_Movimiento_Cliente] FOREIGN KEY ([id_cliente]) REFERENCES [Cliente] ([id_cliente]),
    CONSTRAINT [FK_Movimiento_Proveedor] FOREIGN KEY ([id_proveedor]) REFERENCES [Proveedor] ([id_proveedor]),
    CONSTRAINT [FK_Movimiento_Sede] FOREIGN KEY ([id_sede]) REFERENCES [Sede] ([id_sede]),
    CONSTRAINT [FK_Movimiento_Usuario] FOREIGN KEY ([id_usuario]) REFERENCES [Usuario] ([id_usuario])
);
GO


CREATE TABLE [Reporte] (
    [id_reporte] int NOT NULL IDENTITY,
    [tipo] nvarchar(50) NOT NULL,
    [fecha_generacion] datetime2 NOT NULL DEFAULT ((sysdatetime())),
    [filtros_aplicados] nvarchar(max) NULL,
    [url_exportacion] nvarchar(500) NULL,
    [id_usuario] int NOT NULL,
    [id_sede] int NULL,
    CONSTRAINT [PK__Reporte__87E4F5CB5CE19525] PRIMARY KEY ([id_reporte]),
    CONSTRAINT [FK_Reporte_Sede] FOREIGN KEY ([id_sede]) REFERENCES [Sede] ([id_sede]),
    CONSTRAINT [FK_Reporte_Usuario] FOREIGN KEY ([id_usuario]) REFERENCES [Usuario] ([id_usuario])
);
GO


CREATE TABLE [DetalleMovimiento] (
    [id_detalle] int NOT NULL IDENTITY,
    [id_movimiento] int NOT NULL,
    [id_producto] int NOT NULL,
    [cantidad] int NOT NULL,
    [precio_unitario_snapshot] decimal(10,2) NOT NULL,
    [stock_anterior] int NOT NULL,
    [id_ubicacion] int NOT NULL,
    CONSTRAINT [PK__DetalleM__4F1332DE728D46B2] PRIMARY KEY ([id_detalle]),
    CONSTRAINT [FK_DetalleMovimiento_Movimiento] FOREIGN KEY ([id_movimiento]) REFERENCES [Movimiento] ([id_movimiento]),
    CONSTRAINT [FK_DetalleMovimiento_Producto] FOREIGN KEY ([id_producto]) REFERENCES [Producto] ([id_producto]),
    CONSTRAINT [FK_DetalleMovimiento_Ubicacion] FOREIGN KEY ([id_ubicacion]) REFERENCES [Ubicacion] ([id_ubicacion])
);
GO


CREATE TABLE [Evidencia] (
    [id_evidencia] int NOT NULL IDENTITY,
    [tipo] nvarchar(50) NOT NULL,
    [url_archivo] nvarchar(500) NOT NULL,
    [id_movimiento] int NOT NULL,
    [id_detalle] int NULL,
    [id_camara] int NULL,
    [fecha] datetime2 NOT NULL DEFAULT ((sysdatetime())),
    CONSTRAINT [PK__Evidenci__62875FB95B67FAF0] PRIMARY KEY ([id_evidencia]),
    CONSTRAINT [FK_Evidencia_Camara] FOREIGN KEY ([id_camara]) REFERENCES [Camara] ([id_camara]),
    CONSTRAINT [FK_Evidencia_DetalleMovimiento] FOREIGN KEY ([id_detalle]) REFERENCES [DetalleMovimiento] ([id_detalle]),
    CONSTRAINT [FK_Evidencia_Movimiento] FOREIGN KEY ([id_movimiento]) REFERENCES [Movimiento] ([id_movimiento])
);
GO


CREATE INDEX [IX_Alerta_id_producto] ON [Alerta] ([id_producto]);
GO


CREATE INDEX [IX_Alerta_id_sede] ON [Alerta] ([id_sede]);
GO


CREATE INDEX [IX_Alerta_id_usuario_atiende] ON [Alerta] ([id_usuario_atiende]);
GO


CREATE INDEX [IX_Camara_id_sede] ON [Camara] ([id_sede]);
GO


CREATE INDEX [IX_Camara_id_ubicacion] ON [Camara] ([id_ubicacion]);
GO


CREATE UNIQUE INDEX [UQ__Camara__40F9A2063111D4B6] ON [Camara] ([codigo]);
GO


CREATE UNIQUE INDEX [UQ__Cliente__BAE363B0F0293B86] ON [Cliente] ([ruc_dni]);
GO


CREATE INDEX [IX_DetalleMovimiento_id_movimiento] ON [DetalleMovimiento] ([id_movimiento]);
GO


CREATE INDEX [IX_DetalleMovimiento_id_producto] ON [DetalleMovimiento] ([id_producto]);
GO


CREATE INDEX [IX_DetalleMovimiento_id_ubicacion] ON [DetalleMovimiento] ([id_ubicacion]);
GO


CREATE INDEX [IX_Evidencia_id_camara] ON [Evidencia] ([id_camara]);
GO


CREATE INDEX [IX_Evidencia_id_detalle] ON [Evidencia] ([id_detalle]);
GO


CREATE INDEX [IX_Evidencia_id_movimiento] ON [Evidencia] ([id_movimiento]);
GO


CREATE INDEX [IX_Movimiento_id_cliente] ON [Movimiento] ([id_cliente]);
GO


CREATE INDEX [IX_Movimiento_id_proveedor] ON [Movimiento] ([id_proveedor]);
GO


CREATE INDEX [IX_Movimiento_id_sede] ON [Movimiento] ([id_sede]);
GO


CREATE INDEX [IX_Movimiento_id_usuario] ON [Movimiento] ([id_usuario]);
GO


CREATE UNIQUE INDEX [UQ__Producto__40F9A20657184105] ON [Producto] ([codigo]);
GO


CREATE INDEX [IX_ProductoUbicacion_id_ubicacion] ON [ProductoUbicacion] ([id_ubicacion]);
GO


CREATE UNIQUE INDEX [UQ_ProductoUbicacion] ON [ProductoUbicacion] ([id_producto], [id_ubicacion]);
GO


CREATE UNIQUE INDEX [UQ__Proveedo__C2B74E619588A6B9] ON [Proveedor] ([ruc]);
GO


CREATE INDEX [IX_Reporte_id_sede] ON [Reporte] ([id_sede]);
GO


CREATE INDEX [IX_Reporte_id_usuario] ON [Reporte] ([id_usuario]);
GO


CREATE INDEX [IX_Ubicacion_id_sede] ON [Ubicacion] ([id_sede]);
GO


CREATE UNIQUE INDEX [UQ__Ubicacio__C20D4E361AE763BB] ON [Ubicacion] ([codigo_estante]);
GO


CREATE INDEX [IX_Usuario_id_sede] ON [Usuario] ([id_sede]);
GO


CREATE UNIQUE INDEX [UQ__Usuario__9AFF8FC662AB68B8] ON [Usuario] ([usuario]);
GO


