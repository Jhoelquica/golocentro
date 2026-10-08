--
-- PostgreSQL database dump
--

\restrict StSH9HfYE2FYx3D9tCfauTGfvuLVNUINXzG7RWUf2daiXCFXcgnRkcle7oIcV0S

-- Dumped from database version 18.6
-- Dumped by pg_dump version 18.6

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: ajuste_inventario; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.ajuste_inventario (
    id_ajuste integer NOT NULL,
    fecha timestamp(6) without time zone DEFAULT now() NOT NULL,
    id_usuario integer NOT NULL,
    id_sede integer NOT NULL,
    motivo character varying(20) NOT NULL,
    observaciones character varying(200),
    CONSTRAINT chk_ajusteinventario_motivo CHECK (((motivo)::text = ANY ((ARRAY['conteo_inicial'::character varying, 'conteo'::character varying, 'merma'::character varying, 'correccion'::character varying])::text[])))
);


--
-- Name: ajuste_inventario_id_ajuste_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.ajuste_inventario ALTER COLUMN id_ajuste ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.ajuste_inventario_id_ajuste_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: alerta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.alerta (
    id_alerta integer NOT NULL,
    id_producto integer NOT NULL,
    tipo character varying(20) NOT NULL,
    mensaje text NOT NULL,
    fecha_generada timestamp(6) without time zone DEFAULT now() NOT NULL,
    estado character varying(15) DEFAULT 'pendiente'::character varying NOT NULL,
    id_usuario_atiende integer,
    fecha_atendida timestamp(6) without time zone,
    id_sede integer NOT NULL,
    CONSTRAINT chk_alerta_estado CHECK ((((estado)::text = 'atendida'::text) OR ((estado)::text = 'pendiente'::text))),
    CONSTRAINT chk_alerta_tipo CHECK ((((tipo)::text = 'sobrestock'::text) OR ((tipo)::text = 'vencimiento'::text) OR ((tipo)::text = 'stock_minimo'::text)))
);


--
-- Name: alerta_id_alerta_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.alerta ALTER COLUMN id_alerta ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.alerta_id_alerta_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: camara; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.camara (
    id_camara integer NOT NULL,
    codigo character varying(50) NOT NULL,
    descripcion character varying(200),
    id_ubicacion integer NOT NULL,
    id_sede integer NOT NULL
);


--
-- Name: camara_id_camara_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.camara ALTER COLUMN id_camara ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.camara_id_camara_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: cliente; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cliente (
    id_cliente integer NOT NULL,
    nombre character varying(150) NOT NULL,
    ruc_dni character varying(20) NOT NULL,
    contacto character varying(100),
    direccion character varying(200),
    celular character varying(15),
    CONSTRAINT chk_cliente_rucdni CHECK (((length((ruc_dni)::text) >= 8) AND (length((ruc_dni)::text) <= 11)))
);


--
-- Name: cliente_id_cliente_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.cliente ALTER COLUMN id_cliente ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.cliente_id_cliente_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: detalle_ajuste; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.detalle_ajuste (
    id_detalle integer NOT NULL,
    id_ajuste integer NOT NULL,
    id_producto integer NOT NULL,
    id_ubicacion integer NOT NULL,
    cantidad_anterior integer NOT NULL,
    cantidad_nueva integer NOT NULL,
    CONSTRAINT chk_detalleajuste_cantidades CHECK (((cantidad_anterior >= 0) AND (cantidad_nueva >= 0)))
);


--
-- Name: detalle_ajuste_id_detalle_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.detalle_ajuste ALTER COLUMN id_detalle ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.detalle_ajuste_id_detalle_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: detalle_movimiento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.detalle_movimiento (
    id_detalle integer NOT NULL,
    id_movimiento integer NOT NULL,
    id_producto integer NOT NULL,
    cantidad integer NOT NULL,
    precio_unitario_snapshot numeric(10,2) NOT NULL,
    stock_anterior integer NOT NULL,
    id_ubicacion integer NOT NULL,
    fecha_vencimiento date,
    lote character varying(50),
    presentacion character varying(30),
    factor integer DEFAULT 1 NOT NULL,
    CONSTRAINT chk_detallemovimiento_cantidad CHECK ((cantidad > 0)),
    CONSTRAINT chk_detallemovimiento_factor CHECK (((factor >= 1) AND ((cantidad % factor) = 0))),
    CONSTRAINT chk_detallemovimiento_precio CHECK ((precio_unitario_snapshot >= (0)::numeric)),
    CONSTRAINT chk_detallemovimiento_stockanterior CHECK ((stock_anterior >= 0))
);


--
-- Name: detalle_movimiento_id_detalle_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.detalle_movimiento ALTER COLUMN id_detalle ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.detalle_movimiento_id_detalle_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: detalle_traslado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.detalle_traslado (
    id_detalle integer NOT NULL,
    id_traslado integer NOT NULL,
    id_producto integer NOT NULL,
    cantidad integer NOT NULL,
    id_ubicacion_origen integer NOT NULL,
    id_ubicacion_destino integer NOT NULL,
    CONSTRAINT chk_detalletraslado_cantidad CHECK ((cantidad > 0)),
    CONSTRAINT chk_detalletraslado_ubicaciones CHECK ((id_ubicacion_origen <> id_ubicacion_destino))
);


--
-- Name: detalle_traslado_id_detalle_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.detalle_traslado ALTER COLUMN id_detalle ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.detalle_traslado_id_detalle_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: evidencia; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.evidencia (
    id_evidencia integer NOT NULL,
    tipo character varying(50) NOT NULL,
    url_archivo character varying(500) NOT NULL,
    id_movimiento integer NOT NULL,
    id_detalle integer,
    id_camara integer,
    fecha timestamp(6) without time zone DEFAULT now() NOT NULL
);


--
-- Name: evidencia_id_evidencia_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.evidencia ALTER COLUMN id_evidencia ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.evidencia_id_evidencia_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: movimiento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.movimiento (
    id_movimiento integer NOT NULL,
    tipo character varying(20) NOT NULL,
    fecha timestamp(6) without time zone DEFAULT now() NOT NULL,
    id_usuario integer NOT NULL,
    id_cliente integer,
    id_proveedor integer,
    comprobante_emitido boolean DEFAULT false NOT NULL,
    observaciones text,
    id_sede integer NOT NULL,
    CONSTRAINT chk_movimiento_origendestino CHECK ((((lower((tipo)::text) = 'entrada'::text) AND (id_proveedor IS NOT NULL) AND (id_cliente IS NULL)) OR ((lower((tipo)::text) = 'salida'::text) AND (id_cliente IS NOT NULL) AND (id_proveedor IS NULL)))),
    CONSTRAINT chk_movimiento_tipo CHECK ((lower((tipo)::text) = ANY (ARRAY['entrada'::text, 'salida'::text])))
);


--
-- Name: movimiento_id_movimiento_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.movimiento ALTER COLUMN id_movimiento ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.movimiento_id_movimiento_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: negocio; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.negocio (
    id_negocio integer DEFAULT 1 NOT NULL,
    nombre_comercial character varying(100) NOT NULL,
    razon_social character varying(150),
    ruc character varying(11),
    telefono character varying(15),
    correo character varying(100),
    mensaje_nota character varying(120),
    CONSTRAINT chk_negocio_nombre CHECK ((length(TRIM(BOTH FROM nombre_comercial)) > 0)),
    CONSTRAINT chk_negocio_ruc CHECK (((ruc IS NULL) OR ((ruc)::text ~ '^(10|15|17|20)[0-9]{9}$'::text))),
    CONSTRAINT chk_negocio_unico CHECK ((id_negocio = 1))
);


--
-- Name: nota_venta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.nota_venta (
    id_nota integer NOT NULL,
    id_movimiento integer NOT NULL,
    serie character varying(4) DEFAULT 'NV01'::character varying NOT NULL,
    numero integer NOT NULL,
    subtotal numeric(10,2) NOT NULL,
    descuento numeric(10,2) DEFAULT 0 NOT NULL,
    total numeric(10,2) NOT NULL,
    metodo_pago character varying(15) NOT NULL,
    estado character varying(10) DEFAULT 'emitida'::character varying NOT NULL,
    fecha_anulacion timestamp(6) without time zone,
    id_usuario_anulacion integer,
    motivo_anulacion character varying(200),
    CONSTRAINT chk_notaventa_anulacion CHECK (((((estado)::text = 'emitida'::text) AND (fecha_anulacion IS NULL) AND (id_usuario_anulacion IS NULL) AND (motivo_anulacion IS NULL)) OR (((estado)::text = 'anulada'::text) AND (fecha_anulacion IS NOT NULL) AND (id_usuario_anulacion IS NOT NULL) AND (length(TRIM(BOTH FROM motivo_anulacion)) > 0)))),
    CONSTRAINT chk_notaventa_estado CHECK (((estado)::text = ANY ((ARRAY['emitida'::character varying, 'anulada'::character varying])::text[]))),
    CONSTRAINT chk_notaventa_metodo CHECK (((metodo_pago)::text = ANY ((ARRAY['efectivo'::character varying, 'yape'::character varying, 'plin'::character varying, 'transferencia'::character varying, 'tarjeta'::character varying])::text[]))),
    CONSTRAINT chk_notaventa_montos CHECK (((subtotal >= (0)::numeric) AND (descuento >= (0)::numeric) AND (descuento <= subtotal) AND (total = (subtotal - descuento))))
);


--
-- Name: nota_venta_id_nota_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.nota_venta ALTER COLUMN id_nota ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.nota_venta_id_nota_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: plano_linea; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.plano_linea (
    id_linea integer NOT NULL,
    id_sede integer NOT NULL,
    tipo character varying(15) NOT NULL,
    x1 numeric(5,1) NOT NULL,
    y1 numeric(5,1) NOT NULL,
    x2 numeric(5,1) NOT NULL,
    y2 numeric(5,1) NOT NULL,
    CONSTRAINT chk_planolinea_tipo CHECK (((tipo)::text = ANY ((ARRAY['entrada'::character varying, 'division'::character varying, 'pasillo'::character varying])::text[])))
);


--
-- Name: plano_linea_id_linea_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.plano_linea ALTER COLUMN id_linea ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.plano_linea_id_linea_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: producto; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.producto (
    id_producto integer NOT NULL,
    nombre character varying(150) NOT NULL,
    tipo character varying(50) NOT NULL,
    codigo character varying(50) NOT NULL,
    unidad_medida character varying(20) NOT NULL,
    precio_unitario numeric(10,2) NOT NULL,
    stock_actual integer DEFAULT 0 NOT NULL,
    stock_minimo integer DEFAULT 0 NOT NULL,
    fecha_vencimiento date,
    lote character varying(50),
    CONSTRAINT chk_producto_precio CHECK ((precio_unitario >= (0)::numeric)),
    CONSTRAINT chk_producto_stockactual CHECK ((stock_actual >= 0)),
    CONSTRAINT chk_producto_stockminimo CHECK ((stock_minimo >= 0))
);


--
-- Name: producto_id_producto_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.producto ALTER COLUMN id_producto ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.producto_id_producto_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: producto_presentacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.producto_presentacion (
    id_presentacion integer NOT NULL,
    id_producto integer NOT NULL,
    nombre character varying(30) NOT NULL,
    factor integer NOT NULL,
    precio numeric(10,2) NOT NULL,
    CONSTRAINT chk_productopresentacion_factor CHECK ((factor > 1)),
    CONSTRAINT chk_productopresentacion_nombre CHECK ((length(TRIM(BOTH FROM nombre)) > 0)),
    CONSTRAINT chk_productopresentacion_precio CHECK ((precio >= (0)::numeric))
);


--
-- Name: producto_presentacion_id_presentacion_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.producto_presentacion ALTER COLUMN id_presentacion ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.producto_presentacion_id_presentacion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: producto_ubicacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.producto_ubicacion (
    id integer NOT NULL,
    id_producto integer NOT NULL,
    id_ubicacion integer NOT NULL,
    cantidad_actual integer DEFAULT 0 NOT NULL,
    ultima_actualizacion timestamp(6) without time zone DEFAULT now() NOT NULL,
    CONSTRAINT chk_productoubicacion_cantidad CHECK ((cantidad_actual >= 0))
);


--
-- Name: producto_ubicacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.producto_ubicacion ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.producto_ubicacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: proveedor; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.proveedor (
    id_proveedor integer NOT NULL,
    nombre character varying(150) NOT NULL,
    ruc character varying(20) NOT NULL,
    contacto character varying(100),
    direccion character varying(200),
    celular character varying(15)
);


--
-- Name: proveedor_id_proveedor_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.proveedor ALTER COLUMN id_proveedor ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.proveedor_id_proveedor_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: reporte; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.reporte (
    id_reporte integer NOT NULL,
    tipo character varying(50) NOT NULL,
    fecha_generacion timestamp(6) without time zone DEFAULT now() NOT NULL,
    filtros_aplicados text,
    url_exportacion character varying(500),
    id_usuario integer NOT NULL,
    id_sede integer
);


--
-- Name: reporte_id_reporte_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.reporte ALTER COLUMN id_reporte ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.reporte_id_reporte_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: sede; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.sede (
    id_sede integer NOT NULL,
    nombre character varying(100) NOT NULL,
    direccion character varying(200) NOT NULL,
    ciudad character varying(80) DEFAULT 'Huamanga'::character varying NOT NULL,
    estado character varying(10) DEFAULT 'activo'::character varying NOT NULL,
    plano_ancho numeric(5,1),
    plano_alto numeric(5,1),
    telefono character varying(15),
    CONSTRAINT chk_sede_estado CHECK ((((estado)::text = 'activo'::text) OR ((estado)::text = 'inactivo'::text))),
    CONSTRAINT chk_sede_plano CHECK ((((plano_ancho IS NULL) AND (plano_alto IS NULL)) OR ((plano_ancho > (0)::numeric) AND (plano_alto > (0)::numeric))))
);


--
-- Name: sede_id_sede_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.sede ALTER COLUMN id_sede ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.sede_id_sede_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: traslado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.traslado (
    id_traslado integer NOT NULL,
    fecha timestamp(6) without time zone DEFAULT now() NOT NULL,
    id_usuario integer NOT NULL,
    id_sede integer NOT NULL,
    observaciones character varying(200)
);


--
-- Name: traslado_id_traslado_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.traslado ALTER COLUMN id_traslado ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.traslado_id_traslado_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ubicacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.ubicacion (
    id_ubicacion integer NOT NULL,
    codigo_estante character varying(20) NOT NULL,
    descripcion character varying(200),
    capacidad integer,
    id_sede integer NOT NULL,
    tipo character varying(15) DEFAULT 'almacenaje'::character varying NOT NULL,
    pos_x numeric(5,1),
    pos_y numeric(5,1),
    ancho numeric(5,1),
    alto numeric(5,1),
    CONSTRAINT chk_ubicacion_capacidad CHECK ((capacidad > 0)),
    CONSTRAINT chk_ubicacion_plano CHECK ((((pos_x IS NULL) AND (pos_y IS NULL) AND (ancho IS NULL) AND (alto IS NULL)) OR ((pos_x >= (0)::numeric) AND (pos_y >= (0)::numeric) AND (ancho > (0)::numeric) AND (alto > (0)::numeric)))),
    CONSTRAINT chk_ubicacion_tipo CHECK (((tipo)::text = ANY ((ARRAY['almacenaje'::character varying, 'recepcion'::character varying])::text[])))
);


--
-- Name: ubicacion_id_ubicacion_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.ubicacion ALTER COLUMN id_ubicacion ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.ubicacion_id_ubicacion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: usuario; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuario (
    id_usuario integer NOT NULL,
    nombre character varying(100) NOT NULL,
    rol character varying(20) NOT NULL,
    usuario character varying(50) NOT NULL,
    contrasena character varying(255) NOT NULL,
    estado character varying(10) DEFAULT 'activo'::character varying NOT NULL,
    fecha_creacion timestamp(6) without time zone DEFAULT now() NOT NULL,
    id_sede integer,
    foto_url character varying(255),
    CONSTRAINT chk_usuario_estado CHECK ((((estado)::text = 'activo'::text) OR ((estado)::text = 'inactivo'::text))),
    CONSTRAINT chk_usuario_rol CHECK ((((rol)::text = 'trabajador'::text) OR ((rol)::text = 'encargada'::text) OR ((rol)::text = 'duena'::text)))
);


--
-- Name: usuario_id_usuario_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.usuario ALTER COLUMN id_usuario ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.usuario_id_usuario_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ajuste_inventario ajuste_inventario_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ajuste_inventario
    ADD CONSTRAINT ajuste_inventario_pkey PRIMARY KEY (id_ajuste);


--
-- Name: alerta alerta_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.alerta
    ADD CONSTRAINT alerta_pkey PRIMARY KEY (id_alerta);


--
-- Name: camara camara_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.camara
    ADD CONSTRAINT camara_codigo_key UNIQUE (codigo);


--
-- Name: camara camara_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.camara
    ADD CONSTRAINT camara_pkey PRIMARY KEY (id_camara);


--
-- Name: cliente cliente_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cliente
    ADD CONSTRAINT cliente_pkey PRIMARY KEY (id_cliente);


--
-- Name: cliente cliente_ruc_dni_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cliente
    ADD CONSTRAINT cliente_ruc_dni_key UNIQUE (ruc_dni);


--
-- Name: detalle_ajuste detalle_ajuste_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_ajuste
    ADD CONSTRAINT detalle_ajuste_pkey PRIMARY KEY (id_detalle);


--
-- Name: detalle_movimiento detalle_movimiento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_movimiento
    ADD CONSTRAINT detalle_movimiento_pkey PRIMARY KEY (id_detalle);


--
-- Name: detalle_traslado detalle_traslado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_traslado
    ADD CONSTRAINT detalle_traslado_pkey PRIMARY KEY (id_detalle);


--
-- Name: evidencia evidencia_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.evidencia
    ADD CONSTRAINT evidencia_pkey PRIMARY KEY (id_evidencia);


--
-- Name: movimiento movimiento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movimiento
    ADD CONSTRAINT movimiento_pkey PRIMARY KEY (id_movimiento);


--
-- Name: negocio negocio_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.negocio
    ADD CONSTRAINT negocio_pkey PRIMARY KEY (id_negocio);


--
-- Name: nota_venta nota_venta_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.nota_venta
    ADD CONSTRAINT nota_venta_pkey PRIMARY KEY (id_nota);


--
-- Name: plano_linea plano_linea_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.plano_linea
    ADD CONSTRAINT plano_linea_pkey PRIMARY KEY (id_linea);


--
-- Name: producto producto_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.producto
    ADD CONSTRAINT producto_codigo_key UNIQUE (codigo);


--
-- Name: producto producto_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.producto
    ADD CONSTRAINT producto_pkey PRIMARY KEY (id_producto);


--
-- Name: producto_presentacion producto_presentacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.producto_presentacion
    ADD CONSTRAINT producto_presentacion_pkey PRIMARY KEY (id_presentacion);


--
-- Name: producto_ubicacion producto_ubicacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.producto_ubicacion
    ADD CONSTRAINT producto_ubicacion_pkey PRIMARY KEY (id);


--
-- Name: proveedor proveedor_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.proveedor
    ADD CONSTRAINT proveedor_pkey PRIMARY KEY (id_proveedor);


--
-- Name: proveedor proveedor_ruc_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.proveedor
    ADD CONSTRAINT proveedor_ruc_key UNIQUE (ruc);


--
-- Name: reporte reporte_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reporte
    ADD CONSTRAINT reporte_pkey PRIMARY KEY (id_reporte);


--
-- Name: sede sede_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sede
    ADD CONSTRAINT sede_pkey PRIMARY KEY (id_sede);


--
-- Name: traslado traslado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.traslado
    ADD CONSTRAINT traslado_pkey PRIMARY KEY (id_traslado);


--
-- Name: ubicacion ubicacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ubicacion
    ADD CONSTRAINT ubicacion_pkey PRIMARY KEY (id_ubicacion);


--
-- Name: detalle_ajuste uq_detalleajuste_producto; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_ajuste
    ADD CONSTRAINT uq_detalleajuste_producto UNIQUE (id_ajuste, id_producto, id_ubicacion);


--
-- Name: nota_venta uq_notaventa_movimiento; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.nota_venta
    ADD CONSTRAINT uq_notaventa_movimiento UNIQUE (id_movimiento);


--
-- Name: nota_venta uq_notaventa_numero; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.nota_venta
    ADD CONSTRAINT uq_notaventa_numero UNIQUE (serie, numero);


--
-- Name: producto_ubicacion uq_producto_ubicacion; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.producto_ubicacion
    ADD CONSTRAINT uq_producto_ubicacion UNIQUE (id_producto, id_ubicacion);


--
-- Name: producto_presentacion uq_productopresentacion_nombre; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.producto_presentacion
    ADD CONSTRAINT uq_productopresentacion_nombre UNIQUE (id_producto, nombre);


--
-- Name: ubicacion uq_ubicacion_sede_codigo; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ubicacion
    ADD CONSTRAINT uq_ubicacion_sede_codigo UNIQUE (id_sede, codigo_estante);


--
-- Name: usuario usuario_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario
    ADD CONSTRAINT usuario_pkey PRIMARY KEY (id_usuario);


--
-- Name: usuario usuario_usuario_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario
    ADD CONSTRAINT usuario_usuario_key UNIQUE (usuario);


--
-- Name: detalle_movimiento_id_movimiento_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX detalle_movimiento_id_movimiento_idx ON public.detalle_movimiento USING btree (id_movimiento);


--
-- Name: ajuste_inventario ajuste_inventario_id_sede_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ajuste_inventario
    ADD CONSTRAINT ajuste_inventario_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES public.sede(id_sede);


--
-- Name: ajuste_inventario ajuste_inventario_id_usuario_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ajuste_inventario
    ADD CONSTRAINT ajuste_inventario_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES public.usuario(id_usuario);


--
-- Name: alerta alerta_id_producto_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.alerta
    ADD CONSTRAINT alerta_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES public.producto(id_producto);


--
-- Name: alerta alerta_id_sede_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.alerta
    ADD CONSTRAINT alerta_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES public.sede(id_sede);


--
-- Name: alerta alerta_id_usuario_atiende_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.alerta
    ADD CONSTRAINT alerta_id_usuario_atiende_fkey FOREIGN KEY (id_usuario_atiende) REFERENCES public.usuario(id_usuario);


--
-- Name: camara camara_id_sede_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.camara
    ADD CONSTRAINT camara_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES public.sede(id_sede);


--
-- Name: camara camara_id_ubicacion_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.camara
    ADD CONSTRAINT camara_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES public.ubicacion(id_ubicacion);


--
-- Name: detalle_ajuste detalle_ajuste_id_ajuste_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_ajuste
    ADD CONSTRAINT detalle_ajuste_id_ajuste_fkey FOREIGN KEY (id_ajuste) REFERENCES public.ajuste_inventario(id_ajuste);


--
-- Name: detalle_ajuste detalle_ajuste_id_producto_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_ajuste
    ADD CONSTRAINT detalle_ajuste_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES public.producto(id_producto);


--
-- Name: detalle_ajuste detalle_ajuste_id_ubicacion_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_ajuste
    ADD CONSTRAINT detalle_ajuste_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES public.ubicacion(id_ubicacion);


--
-- Name: detalle_movimiento detalle_movimiento_id_movimiento_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_movimiento
    ADD CONSTRAINT detalle_movimiento_id_movimiento_fkey FOREIGN KEY (id_movimiento) REFERENCES public.movimiento(id_movimiento);


--
-- Name: detalle_movimiento detalle_movimiento_id_producto_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_movimiento
    ADD CONSTRAINT detalle_movimiento_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES public.producto(id_producto);


--
-- Name: detalle_movimiento detalle_movimiento_id_ubicacion_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_movimiento
    ADD CONSTRAINT detalle_movimiento_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES public.ubicacion(id_ubicacion);


--
-- Name: detalle_traslado detalle_traslado_destino_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_traslado
    ADD CONSTRAINT detalle_traslado_destino_fkey FOREIGN KEY (id_ubicacion_destino) REFERENCES public.ubicacion(id_ubicacion);


--
-- Name: detalle_traslado detalle_traslado_id_producto_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_traslado
    ADD CONSTRAINT detalle_traslado_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES public.producto(id_producto);


--
-- Name: detalle_traslado detalle_traslado_id_traslado_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_traslado
    ADD CONSTRAINT detalle_traslado_id_traslado_fkey FOREIGN KEY (id_traslado) REFERENCES public.traslado(id_traslado);


--
-- Name: detalle_traslado detalle_traslado_origen_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalle_traslado
    ADD CONSTRAINT detalle_traslado_origen_fkey FOREIGN KEY (id_ubicacion_origen) REFERENCES public.ubicacion(id_ubicacion);


--
-- Name: evidencia evidencia_id_camara_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.evidencia
    ADD CONSTRAINT evidencia_id_camara_fkey FOREIGN KEY (id_camara) REFERENCES public.camara(id_camara);


--
-- Name: evidencia evidencia_id_detalle_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.evidencia
    ADD CONSTRAINT evidencia_id_detalle_fkey FOREIGN KEY (id_detalle) REFERENCES public.detalle_movimiento(id_detalle);


--
-- Name: evidencia evidencia_id_movimiento_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.evidencia
    ADD CONSTRAINT evidencia_id_movimiento_fkey FOREIGN KEY (id_movimiento) REFERENCES public.movimiento(id_movimiento);


--
-- Name: movimiento movimiento_id_cliente_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movimiento
    ADD CONSTRAINT movimiento_id_cliente_fkey FOREIGN KEY (id_cliente) REFERENCES public.cliente(id_cliente);


--
-- Name: movimiento movimiento_id_proveedor_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movimiento
    ADD CONSTRAINT movimiento_id_proveedor_fkey FOREIGN KEY (id_proveedor) REFERENCES public.proveedor(id_proveedor);


--
-- Name: movimiento movimiento_id_sede_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movimiento
    ADD CONSTRAINT movimiento_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES public.sede(id_sede);


--
-- Name: movimiento movimiento_id_usuario_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movimiento
    ADD CONSTRAINT movimiento_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES public.usuario(id_usuario);


--
-- Name: nota_venta nota_venta_id_movimiento_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.nota_venta
    ADD CONSTRAINT nota_venta_id_movimiento_fkey FOREIGN KEY (id_movimiento) REFERENCES public.movimiento(id_movimiento);


--
-- Name: nota_venta nota_venta_id_usuario_anulacion_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.nota_venta
    ADD CONSTRAINT nota_venta_id_usuario_anulacion_fkey FOREIGN KEY (id_usuario_anulacion) REFERENCES public.usuario(id_usuario);


--
-- Name: plano_linea plano_linea_id_sede_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.plano_linea
    ADD CONSTRAINT plano_linea_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES public.sede(id_sede);


--
-- Name: producto_presentacion producto_presentacion_id_producto_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.producto_presentacion
    ADD CONSTRAINT producto_presentacion_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES public.producto(id_producto) ON DELETE CASCADE;


--
-- Name: producto_ubicacion producto_ubicacion_id_producto_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.producto_ubicacion
    ADD CONSTRAINT producto_ubicacion_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES public.producto(id_producto);


--
-- Name: producto_ubicacion producto_ubicacion_id_ubicacion_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.producto_ubicacion
    ADD CONSTRAINT producto_ubicacion_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES public.ubicacion(id_ubicacion);


--
-- Name: reporte reporte_id_sede_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reporte
    ADD CONSTRAINT reporte_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES public.sede(id_sede);


--
-- Name: reporte reporte_id_usuario_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reporte
    ADD CONSTRAINT reporte_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES public.usuario(id_usuario);


--
-- Name: traslado traslado_id_sede_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.traslado
    ADD CONSTRAINT traslado_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES public.sede(id_sede);


--
-- Name: traslado traslado_id_usuario_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.traslado
    ADD CONSTRAINT traslado_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES public.usuario(id_usuario);


--
-- Name: ubicacion ubicacion_id_sede_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ubicacion
    ADD CONSTRAINT ubicacion_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES public.sede(id_sede);


--
-- Name: usuario usuario_id_sede_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario
    ADD CONSTRAINT usuario_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES public.sede(id_sede);


--
-- PostgreSQL database dump complete
--

\unrestrict StSH9HfYE2FYx3D9tCfauTGfvuLVNUINXzG7RWUf2daiXCFXcgnRkcle7oIcV0S

