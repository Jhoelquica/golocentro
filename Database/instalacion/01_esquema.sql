-- =====================================================================
-- Esquema completo de la base de datos de Golocentro (PostgreSQL), para
-- crearla desde cero en una PC nueva.
--
-- Crea las 19 tablas tal como las espera la app: es el modelo de EF Core
-- (Data/AppDbContext.cs) exportado con `dotnet ef dbcontext script`, más
-- las restricciones CHECK que el modelo no guarda y el cliente
-- «Público en general», que las ventas necesitan.
--
-- Ya incluye lo de los scripts fechados de Database/ (plano, inventario,
-- nota de venta y anulación): en una base creada con este archivo NO hay
-- que ejecutarlos. Esos scripts son para actualizar una base existente.
--
-- Uso: ver la sección «Puesta en marcha en otra PC» del README.
-- Todo va dentro de una transacción: si algo falla, no se crea nada.
-- =====================================================================

SET client_encoding = 'UTF8';

BEGIN;

CREATE TABLE cliente (
    id_cliente integer GENERATED ALWAYS AS IDENTITY,
    nombre character varying(150) NOT NULL,
    ruc_dni character varying(20) NOT NULL,
    contacto character varying(100),
    direccion character varying(200),
    celular character varying(15),
    CONSTRAINT cliente_pkey PRIMARY KEY (id_cliente)
);


CREATE TABLE producto (
    id_producto integer GENERATED ALWAYS AS IDENTITY,
    nombre character varying(150) NOT NULL,
    tipo character varying(50) NOT NULL,
    codigo character varying(50) NOT NULL,
    unidad_medida character varying(20) NOT NULL,
    precio_unitario numeric(10,2) NOT NULL,
    stock_actual integer NOT NULL DEFAULT 0,
    stock_minimo integer NOT NULL DEFAULT 0,
    fecha_vencimiento date,
    lote character varying(50),
    CONSTRAINT producto_pkey PRIMARY KEY (id_producto)
);


CREATE TABLE proveedor (
    id_proveedor integer GENERATED ALWAYS AS IDENTITY,
    nombre character varying(150) NOT NULL,
    ruc character varying(20) NOT NULL,
    contacto character varying(100),
    direccion character varying(200),
    CONSTRAINT proveedor_pkey PRIMARY KEY (id_proveedor)
);


CREATE TABLE sede (
    id_sede integer GENERATED ALWAYS AS IDENTITY,
    nombre character varying(100) NOT NULL,
    direccion character varying(200) NOT NULL,
    ciudad character varying(80) NOT NULL DEFAULT ('Huamanga'::character varying),
    estado character varying(10) NOT NULL DEFAULT ('activo'::character varying),
    plano_ancho numeric(5,1),
    plano_alto numeric(5,1),
    CONSTRAINT sede_pkey PRIMARY KEY (id_sede)
);


CREATE TABLE plano_linea (
    id_linea integer GENERATED ALWAYS AS IDENTITY,
    id_sede integer NOT NULL,
    tipo character varying(15) NOT NULL,
    x1 numeric(5,1) NOT NULL,
    y1 numeric(5,1) NOT NULL,
    x2 numeric(5,1) NOT NULL,
    y2 numeric(5,1) NOT NULL,
    CONSTRAINT plano_linea_pkey PRIMARY KEY (id_linea),
    CONSTRAINT plano_linea_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede)
);


CREATE TABLE ubicacion (
    id_ubicacion integer GENERATED ALWAYS AS IDENTITY,
    codigo_estante character varying(20) NOT NULL,
    descripcion character varying(200),
    capacidad integer,
    id_sede integer NOT NULL,
    tipo character varying(15) NOT NULL DEFAULT ('almacenaje'::character varying),
    pos_x numeric(5,1),
    pos_y numeric(5,1),
    ancho numeric(5,1),
    alto numeric(5,1),
    CONSTRAINT ubicacion_pkey PRIMARY KEY (id_ubicacion),
    CONSTRAINT ubicacion_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede)
);


CREATE TABLE usuario (
    id_usuario integer GENERATED ALWAYS AS IDENTITY,
    nombre character varying(100) NOT NULL,
    rol character varying(20) NOT NULL,
    usuario character varying(50) NOT NULL,
    contrasena character varying(255) NOT NULL,
    estado character varying(10) NOT NULL DEFAULT ('activo'::character varying),
    fecha_creacion timestamp(6) without time zone NOT NULL DEFAULT (now()),
    id_sede integer,
    foto_url character varying(255),
    CONSTRAINT usuario_pkey PRIMARY KEY (id_usuario),
    CONSTRAINT usuario_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede)
);


CREATE TABLE camara (
    id_camara integer GENERATED ALWAYS AS IDENTITY,
    codigo character varying(50) NOT NULL,
    descripcion character varying(200),
    id_ubicacion integer NOT NULL,
    id_sede integer NOT NULL,
    CONSTRAINT camara_pkey PRIMARY KEY (id_camara),
    CONSTRAINT camara_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede),
    CONSTRAINT camara_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES ubicacion (id_ubicacion)
);


CREATE TABLE producto_ubicacion (
    id integer GENERATED ALWAYS AS IDENTITY,
    id_producto integer NOT NULL,
    id_ubicacion integer NOT NULL,
    cantidad_actual integer NOT NULL DEFAULT 0,
    ultima_actualizacion timestamp(6) without time zone NOT NULL DEFAULT (now()),
    CONSTRAINT producto_ubicacion_pkey PRIMARY KEY (id),
    CONSTRAINT producto_ubicacion_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES producto (id_producto),
    CONSTRAINT producto_ubicacion_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES ubicacion (id_ubicacion)
);


CREATE TABLE ajuste_inventario (
    id_ajuste integer GENERATED ALWAYS AS IDENTITY,
    fecha timestamp(6) without time zone NOT NULL DEFAULT (now()),
    id_usuario integer NOT NULL,
    id_sede integer NOT NULL,
    motivo character varying(20) NOT NULL,
    observaciones character varying(200),
    CONSTRAINT ajuste_inventario_pkey PRIMARY KEY (id_ajuste),
    CONSTRAINT ajuste_inventario_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede),
    CONSTRAINT ajuste_inventario_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES usuario (id_usuario)
);


CREATE TABLE alerta (
    id_alerta integer GENERATED ALWAYS AS IDENTITY,
    id_producto integer NOT NULL,
    tipo character varying(20) NOT NULL,
    mensaje text NOT NULL,
    fecha_generada timestamp(6) without time zone NOT NULL DEFAULT (now()),
    estado character varying(15) NOT NULL DEFAULT ('pendiente'::character varying),
    id_usuario_atiende integer,
    fecha_atendida timestamp(6) without time zone,
    id_sede integer NOT NULL,
    CONSTRAINT alerta_pkey PRIMARY KEY (id_alerta),
    CONSTRAINT alerta_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES producto (id_producto),
    CONSTRAINT alerta_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede),
    CONSTRAINT alerta_id_usuario_atiende_fkey FOREIGN KEY (id_usuario_atiende) REFERENCES usuario (id_usuario)
);


CREATE TABLE movimiento (
    id_movimiento integer GENERATED ALWAYS AS IDENTITY,
    tipo character varying(20) NOT NULL,
    fecha timestamp(6) without time zone NOT NULL DEFAULT (now()),
    id_usuario integer NOT NULL,
    id_cliente integer,
    id_proveedor integer,
    comprobante_emitido boolean NOT NULL DEFAULT FALSE,
    observaciones text,
    id_sede integer NOT NULL,
    CONSTRAINT movimiento_pkey PRIMARY KEY (id_movimiento),
    CONSTRAINT movimiento_id_cliente_fkey FOREIGN KEY (id_cliente) REFERENCES cliente (id_cliente),
    CONSTRAINT movimiento_id_proveedor_fkey FOREIGN KEY (id_proveedor) REFERENCES proveedor (id_proveedor),
    CONSTRAINT movimiento_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede),
    CONSTRAINT movimiento_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES usuario (id_usuario)
);


CREATE TABLE reporte (
    id_reporte integer GENERATED ALWAYS AS IDENTITY,
    tipo character varying(50) NOT NULL,
    fecha_generacion timestamp(6) without time zone NOT NULL DEFAULT (now()),
    filtros_aplicados text,
    url_exportacion character varying(500),
    id_usuario integer NOT NULL,
    id_sede integer,
    CONSTRAINT reporte_pkey PRIMARY KEY (id_reporte),
    CONSTRAINT reporte_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede),
    CONSTRAINT reporte_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES usuario (id_usuario)
);


CREATE TABLE traslado (
    id_traslado integer GENERATED ALWAYS AS IDENTITY,
    fecha timestamp(6) without time zone NOT NULL DEFAULT (now()),
    id_usuario integer NOT NULL,
    id_sede integer NOT NULL,
    observaciones character varying(200),
    CONSTRAINT traslado_pkey PRIMARY KEY (id_traslado),
    CONSTRAINT traslado_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede),
    CONSTRAINT traslado_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES usuario (id_usuario)
);


CREATE TABLE detalle_ajuste (
    id_detalle integer GENERATED ALWAYS AS IDENTITY,
    id_ajuste integer NOT NULL,
    id_producto integer NOT NULL,
    id_ubicacion integer NOT NULL,
    cantidad_anterior integer NOT NULL,
    cantidad_nueva integer NOT NULL,
    CONSTRAINT detalle_ajuste_pkey PRIMARY KEY (id_detalle),
    CONSTRAINT detalle_ajuste_id_ajuste_fkey FOREIGN KEY (id_ajuste) REFERENCES ajuste_inventario (id_ajuste),
    CONSTRAINT detalle_ajuste_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES producto (id_producto),
    CONSTRAINT detalle_ajuste_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES ubicacion (id_ubicacion)
);


CREATE TABLE detalle_movimiento (
    id_detalle integer GENERATED ALWAYS AS IDENTITY,
    id_movimiento integer NOT NULL,
    id_producto integer NOT NULL,
    cantidad integer NOT NULL,
    precio_unitario_snapshot numeric(10,2) NOT NULL,
    stock_anterior integer NOT NULL,
    id_ubicacion integer NOT NULL,
    CONSTRAINT detalle_movimiento_pkey PRIMARY KEY (id_detalle),
    CONSTRAINT detalle_movimiento_id_movimiento_fkey FOREIGN KEY (id_movimiento) REFERENCES movimiento (id_movimiento),
    CONSTRAINT detalle_movimiento_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES producto (id_producto),
    CONSTRAINT detalle_movimiento_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES ubicacion (id_ubicacion)
);


CREATE TABLE nota_venta (
    id_nota integer GENERATED ALWAYS AS IDENTITY,
    id_movimiento integer NOT NULL,
    serie character varying(4) NOT NULL DEFAULT ('NV01'::character varying),
    numero integer NOT NULL,
    subtotal numeric(10,2) NOT NULL,
    descuento numeric(10,2) NOT NULL,
    total numeric(10,2) NOT NULL,
    metodo_pago character varying(15) NOT NULL,
    estado character varying(10) NOT NULL DEFAULT ('emitida'::character varying),
    fecha_anulacion timestamp(6) without time zone,
    id_usuario_anulacion integer,
    motivo_anulacion character varying(200),
    CONSTRAINT nota_venta_pkey PRIMARY KEY (id_nota),
    CONSTRAINT nota_venta_id_movimiento_fkey FOREIGN KEY (id_movimiento) REFERENCES movimiento (id_movimiento),
    CONSTRAINT nota_venta_id_usuario_anulacion_fkey FOREIGN KEY (id_usuario_anulacion) REFERENCES usuario (id_usuario)
);


CREATE TABLE detalle_traslado (
    id_detalle integer GENERATED ALWAYS AS IDENTITY,
    id_traslado integer NOT NULL,
    id_producto integer NOT NULL,
    cantidad integer NOT NULL,
    id_ubicacion_origen integer NOT NULL,
    id_ubicacion_destino integer NOT NULL,
    CONSTRAINT detalle_traslado_pkey PRIMARY KEY (id_detalle),
    CONSTRAINT detalle_traslado_destino_fkey FOREIGN KEY (id_ubicacion_destino) REFERENCES ubicacion (id_ubicacion),
    CONSTRAINT detalle_traslado_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES producto (id_producto),
    CONSTRAINT detalle_traslado_id_traslado_fkey FOREIGN KEY (id_traslado) REFERENCES traslado (id_traslado),
    CONSTRAINT detalle_traslado_origen_fkey FOREIGN KEY (id_ubicacion_origen) REFERENCES ubicacion (id_ubicacion)
);


CREATE TABLE evidencia (
    id_evidencia integer GENERATED ALWAYS AS IDENTITY,
    tipo character varying(50) NOT NULL,
    url_archivo character varying(500) NOT NULL,
    id_movimiento integer NOT NULL,
    id_detalle integer,
    id_camara integer,
    fecha timestamp(6) without time zone NOT NULL DEFAULT (now()),
    CONSTRAINT evidencia_pkey PRIMARY KEY (id_evidencia),
    CONSTRAINT evidencia_id_camara_fkey FOREIGN KEY (id_camara) REFERENCES camara (id_camara),
    CONSTRAINT evidencia_id_detalle_fkey FOREIGN KEY (id_detalle) REFERENCES detalle_movimiento (id_detalle),
    CONSTRAINT evidencia_id_movimiento_fkey FOREIGN KEY (id_movimiento) REFERENCES movimiento (id_movimiento)
);


CREATE INDEX "IX_ajuste_inventario_id_sede" ON ajuste_inventario (id_sede);


CREATE INDEX "IX_ajuste_inventario_id_usuario" ON ajuste_inventario (id_usuario);


CREATE INDEX "IX_alerta_id_producto" ON alerta (id_producto);


CREATE INDEX "IX_alerta_id_sede" ON alerta (id_sede);


CREATE INDEX "IX_alerta_id_usuario_atiende" ON alerta (id_usuario_atiende);


CREATE UNIQUE INDEX camara_codigo_key ON camara (codigo);


CREATE INDEX "IX_camara_id_sede" ON camara (id_sede);


CREATE INDEX "IX_camara_id_ubicacion" ON camara (id_ubicacion);


CREATE UNIQUE INDEX cliente_ruc_dni_key ON cliente (ruc_dni);


CREATE INDEX "IX_detalle_ajuste_id_producto" ON detalle_ajuste (id_producto);


CREATE INDEX "IX_detalle_ajuste_id_ubicacion" ON detalle_ajuste (id_ubicacion);


CREATE UNIQUE INDEX uq_detalleajuste_producto ON detalle_ajuste (id_ajuste, id_producto, id_ubicacion);


CREATE INDEX "IX_detalle_movimiento_id_movimiento" ON detalle_movimiento (id_movimiento);


CREATE INDEX "IX_detalle_movimiento_id_producto" ON detalle_movimiento (id_producto);


CREATE INDEX "IX_detalle_movimiento_id_ubicacion" ON detalle_movimiento (id_ubicacion);


CREATE INDEX "IX_detalle_traslado_id_producto" ON detalle_traslado (id_producto);


CREATE INDEX "IX_detalle_traslado_id_traslado" ON detalle_traslado (id_traslado);


CREATE INDEX "IX_detalle_traslado_id_ubicacion_destino" ON detalle_traslado (id_ubicacion_destino);


CREATE INDEX "IX_detalle_traslado_id_ubicacion_origen" ON detalle_traslado (id_ubicacion_origen);


CREATE INDEX "IX_evidencia_id_camara" ON evidencia (id_camara);


CREATE INDEX "IX_evidencia_id_detalle" ON evidencia (id_detalle);


CREATE INDEX "IX_evidencia_id_movimiento" ON evidencia (id_movimiento);


CREATE INDEX "IX_movimiento_id_cliente" ON movimiento (id_cliente);


CREATE INDEX "IX_movimiento_id_proveedor" ON movimiento (id_proveedor);


CREATE INDEX "IX_movimiento_id_sede" ON movimiento (id_sede);


CREATE INDEX "IX_movimiento_id_usuario" ON movimiento (id_usuario);


CREATE INDEX "IX_nota_venta_id_usuario_anulacion" ON nota_venta (id_usuario_anulacion);


CREATE UNIQUE INDEX uq_notaventa_movimiento ON nota_venta (id_movimiento);


CREATE UNIQUE INDEX uq_notaventa_numero ON nota_venta (serie, numero);


CREATE INDEX "IX_plano_linea_id_sede" ON plano_linea (id_sede);


CREATE UNIQUE INDEX producto_codigo_key ON producto (codigo);


CREATE INDEX "IX_producto_ubicacion_id_ubicacion" ON producto_ubicacion (id_ubicacion);


CREATE UNIQUE INDEX uq_producto_ubicacion ON producto_ubicacion (id_producto, id_ubicacion);


CREATE UNIQUE INDEX proveedor_ruc_key ON proveedor (ruc);


CREATE INDEX "IX_reporte_id_sede" ON reporte (id_sede);


CREATE INDEX "IX_reporte_id_usuario" ON reporte (id_usuario);


CREATE INDEX "IX_traslado_id_sede" ON traslado (id_sede);


CREATE INDEX "IX_traslado_id_usuario" ON traslado (id_usuario);


CREATE INDEX "IX_ubicacion_id_sede" ON ubicacion (id_sede);


CREATE UNIQUE INDEX ubicacion_codigo_estante_key ON ubicacion (codigo_estante);


CREATE INDEX "IX_usuario_id_sede" ON usuario (id_sede);


CREATE UNIQUE INDEX usuario_usuario_key ON usuario (usuario);


-- ---------------------------------------------------------------------
-- Restricciones CHECK (el modelo de EF no las guarda)
-- ---------------------------------------------------------------------

-- Copiadas de los scripts fechados de Database/
ALTER TABLE ubicacion
    ADD CONSTRAINT chk_ubicacion_tipo CHECK (tipo IN ('almacenaje', 'recepcion')),
    ADD CONSTRAINT chk_ubicacion_plano CHECK (
        (pos_x IS NULL AND pos_y IS NULL AND ancho IS NULL AND alto IS NULL)
        OR (pos_x >= 0 AND pos_y >= 0 AND ancho > 0 AND alto > 0));

ALTER TABLE sede
    ADD CONSTRAINT chk_sede_plano CHECK (
        (plano_ancho IS NULL AND plano_alto IS NULL) OR (plano_ancho > 0 AND plano_alto > 0));

ALTER TABLE plano_linea
    ADD CONSTRAINT chk_planolinea_tipo CHECK (tipo IN ('entrada', 'division', 'pasillo'));

ALTER TABLE detalle_traslado
    ADD CONSTRAINT chk_detalletraslado_cantidad CHECK (cantidad > 0),
    ADD CONSTRAINT chk_detalletraslado_ubicaciones CHECK (id_ubicacion_origen <> id_ubicacion_destino);

ALTER TABLE ajuste_inventario
    ADD CONSTRAINT chk_ajusteinventario_motivo CHECK (motivo IN ('conteo_inicial', 'conteo', 'merma', 'correccion'));

ALTER TABLE detalle_ajuste
    ADD CONSTRAINT chk_detalleajuste_cantidades CHECK (cantidad_anterior >= 0 AND cantidad_nueva >= 0);

ALTER TABLE nota_venta
    ADD CONSTRAINT chk_notaventa_montos CHECK (
        subtotal >= 0 AND descuento >= 0 AND descuento <= subtotal AND total = subtotal - descuento),
    ADD CONSTRAINT chk_notaventa_metodo CHECK (metodo_pago IN ('efectivo', 'yape', 'plin', 'transferencia', 'tarjeta')),
    ADD CONSTRAINT chk_notaventa_estado CHECK (estado IN ('emitida', 'anulada')),
    ADD CONSTRAINT chk_notaventa_anulacion CHECK (
        (estado = 'emitida'
            AND fecha_anulacion IS NULL AND id_usuario_anulacion IS NULL AND motivo_anulacion IS NULL)
        OR
        (estado = 'anulada'
            AND fecha_anulacion IS NOT NULL AND id_usuario_anulacion IS NOT NULL
            AND length(trim(motivo_anulacion)) > 0));

-- El código usa estas tres, pero su definición original no está en el
-- repositorio: se reconstruyeron a partir de los comentarios del código.
ALTER TABLE usuario
    ADD CONSTRAINT chk_usuario_rol CHECK (rol IN ('duena', 'encargada', 'trabajador'));

ALTER TABLE cliente
    ADD CONSTRAINT chk_cliente_rucdni CHECK (char_length(ruc_dni) BETWEEN 8 AND 11);

-- Una venta que dejaría el stock en negativo falla aquí (VentaController lo captura)
ALTER TABLE producto_ubicacion
    ADD CONSTRAINT chk_productoubicacion_cantidad CHECK (cantidad_actual >= 0);

-- ---------------------------------------------------------------------
-- Cliente por defecto de las ventas: la app lo busca por ruc_dni = '00000000'
-- ---------------------------------------------------------------------
INSERT INTO cliente (nombre, ruc_dni) VALUES ('Público en general', '00000000');

COMMIT;
