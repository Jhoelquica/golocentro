-- Plano del almacén y "Mover mercadería" (Golocentro)
--
-- Las ubicaciones son zonas de piso (no hay estantes): cada una tiene un código fijo
-- y una posición en el plano. Las coordenadas van en "cuadritos" de un plano de 30 x 20.
--
-- Se ejecuta UNA sola vez. Todo va dentro de una transacción: si algo falla, no cambia nada.

BEGIN;

-- 1) Posición de cada zona en el plano y su tipo (almacenaje normal o recepción temporal)
ALTER TABLE ubicacion
    ADD COLUMN tipo  varchar(15) NOT NULL DEFAULT 'almacenaje',
    ADD COLUMN pos_x numeric(5,1),
    ADD COLUMN pos_y numeric(5,1),
    ADD COLUMN ancho numeric(5,1),
    ADD COLUMN alto  numeric(5,1),
    ADD CONSTRAINT chk_ubicacion_tipo CHECK (tipo IN ('almacenaje', 'recepcion')),
    ADD CONSTRAINT chk_ubicacion_plano CHECK (
        (pos_x IS NULL AND pos_y IS NULL AND ancho IS NULL AND alto IS NULL)
        OR (pos_x >= 0 AND pos_y >= 0 AND ancho > 0 AND alto > 0));

-- En el piso se mezclan productos: una capacidad en unidades no tiene sentido
ALTER TABLE ubicacion ALTER COLUMN capacidad DROP NOT NULL;

-- 2) Tamaño del plano de cada sede
ALTER TABLE sede
    ADD COLUMN plano_ancho numeric(5,1),
    ADD COLUMN plano_alto  numeric(5,1),
    ADD CONSTRAINT chk_sede_plano CHECK (
        (plano_ancho IS NULL AND plano_alto IS NULL) OR (plano_ancho > 0 AND plano_alto > 0));

-- 3) Líneas de referencia del plano: entrada, división entre cuartos y pasillos
CREATE TABLE plano_linea (
    id_linea integer GENERATED ALWAYS AS IDENTITY,
    id_sede  integer     NOT NULL,
    tipo     varchar(15) NOT NULL,
    x1       numeric(5,1) NOT NULL,
    y1       numeric(5,1) NOT NULL,
    x2       numeric(5,1) NOT NULL,
    y2       numeric(5,1) NOT NULL,
    CONSTRAINT plano_linea_pkey PRIMARY KEY (id_linea),
    CONSTRAINT plano_linea_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede),
    CONSTRAINT chk_planolinea_tipo CHECK (tipo IN ('entrada', 'division', 'pasillo'))
);

-- 4) Mover mercadería entre zonas (ordenar, reubicar). Tabla propia para no mezclarlo
--    con entradas/salidas, que cuentan en reportes y en el Dashboard.
CREATE TABLE traslado (
    id_traslado   integer GENERATED ALWAYS AS IDENTITY,
    fecha         timestamp(6) without time zone NOT NULL DEFAULT now(),
    id_usuario    integer NOT NULL,
    id_sede       integer NOT NULL,
    observaciones varchar(200),
    CONSTRAINT traslado_pkey PRIMARY KEY (id_traslado),
    CONSTRAINT traslado_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES usuario (id_usuario),
    CONSTRAINT traslado_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede)
);

CREATE TABLE detalle_traslado (
    id_detalle           integer GENERATED ALWAYS AS IDENTITY,
    id_traslado          integer NOT NULL,
    id_producto          integer NOT NULL,
    cantidad             integer NOT NULL,
    id_ubicacion_origen  integer NOT NULL,
    id_ubicacion_destino integer NOT NULL,
    CONSTRAINT detalle_traslado_pkey PRIMARY KEY (id_detalle),
    CONSTRAINT detalle_traslado_id_traslado_fkey FOREIGN KEY (id_traslado) REFERENCES traslado (id_traslado),
    CONSTRAINT detalle_traslado_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES producto (id_producto),
    CONSTRAINT detalle_traslado_origen_fkey FOREIGN KEY (id_ubicacion_origen) REFERENCES ubicacion (id_ubicacion),
    CONSTRAINT detalle_traslado_destino_fkey FOREIGN KEY (id_ubicacion_destino) REFERENCES ubicacion (id_ubicacion),
    CONSTRAINT chk_detalletraslado_cantidad CHECK (cantidad > 0),
    CONSTRAINT chk_detalletraslado_ubicaciones CHECK (id_ubicacion_origen <> id_ubicacion_destino)
);

-- 5) Plano de la sede donde está la zona A-01 (Sede Principal)
UPDATE sede SET plano_ancho = 30, plano_alto = 20
WHERE id_sede = (SELECT id_sede FROM ubicacion WHERE codigo_estante = 'A-01');

-- A-01 ya existe y conserva su stock: solo se ubica en el plano
UPDATE ubicacion
SET descripcion = 'Cuarto 1, pared del fondo', pos_x = 5, pos_y = 0, ancho = 4, alto = 2.5
WHERE codigo_estante = 'A-01';

INSERT INTO ubicacion (codigo_estante, descripcion, tipo, id_sede, pos_x, pos_y, ancho, alto)
SELECT v.codigo, v.descripcion, v.tipo, s.id_sede, v.x, v.y, v.w, v.h
FROM (SELECT id_sede FROM ubicacion WHERE codigo_estante = 'A-01') AS s,
(VALUES
    ('A-02', 'Cuarto 1, pared del fondo',       'almacenaje',  9,   0,    9,  2.5),
    ('A-03', 'Cuarto 1, pared del fondo',       'almacenaje', 18,   0,    9,  2.5),
    ('A-04', 'Cuarto 1, pared derecha',         'almacenaje', 27,   0,    3,  6),
    ('A-05', 'Cuarto 1, pared derecha',         'almacenaje', 27,   6,    3,  4),
    ('C-01', 'Cuarto 1, junto a la división',   'almacenaje',  4,   5,    4,  3),
    ('C-02', 'Cuarto 1, junto a la división',   'almacenaje',  4,   8,    4,  5),
    ('C-03', 'Cuarto 1, junto a la división',   'almacenaje',  8,  10,   12,  3),
    ('C-04', 'Cuarto 1, junto a la división',   'almacenaje', 20,  10,   10,  3),
    ('R-01', 'Recepción: lo que llega, antes de ordenar', 'recepcion', 9, 4.5, 17, 5),
    ('B-01', 'Cuarto 2, pared izquierda',       'almacenaje',  0,   5,    2,  3),
    ('B-02', 'Cuarto 2, pared izquierda',       'almacenaje',  0,   8,    2,  4),
    ('B-03', 'Cuarto 2, pared izquierda',       'almacenaje',  0,  12,    2,  8),
    ('B-04', 'Cuarto 2, junto a la división',   'almacenaje',  4,  13,    4,  2),
    ('B-05', 'Cuarto 2, junto a la división',   'almacenaje',  8,  13,    7,  2),
    ('B-06', 'Cuarto 2, junto a la división',   'almacenaje', 15,  13,    6,  2),
    ('B-07', 'Cuarto 2, pared de abajo',        'almacenaje',  2,  18,  3.5,  2),
    ('B-08', 'Cuarto 2, pared de abajo',        'almacenaje', 5.5, 18,  9.5,  2),
    ('B-09', 'Cuarto 2, pared de abajo',        'almacenaje', 15,  18,    6,  2),
    ('B-10', 'Cuarto 2, esquina del fondo',     'almacenaje', 21,  13,    9,  7)
) AS v(codigo, descripcion, tipo, x, y, w, h);

INSERT INTO plano_linea (id_sede, tipo, x1, y1, x2, y2)
SELECT s.id_sede, v.tipo, v.x1, v.y1, v.x2, v.y2
FROM (SELECT id_sede FROM ubicacion WHERE codigo_estante = 'A-01') AS s,
(VALUES
    ('entrada',   0, 1.5,   0,   5),
    ('division',  4,   5,   4,  13),
    ('division',  4,  13,  30,  13),
    ('pasillo',  22, 3.7,   0, 3.7),
    ('pasillo',   3,   5,   3, 16.5),
    ('pasillo',   3, 16.5, 21, 16.5)
) AS v(tipo, x1, y1, x2, y2);

COMMIT;

-- Comprobación: debe listar 20 zonas con posición (A-01..A-05, C-01..C-04, R-01, B-01..B-10)
SELECT codigo_estante, tipo, pos_x, pos_y, ancho, alto
FROM ubicacion
ORDER BY codigo_estante;
