-- Conteo de inventario / ajustes (Golocentro)
--
-- Registra conteos físicos por zona: el stock de la zona pasa a ser lo contado y queda
-- la auditoría de quién contó, cuándo, por qué, y el antes/después de cada producto.
-- No toca movimiento/detalle_movimiento: un ajuste no es compra ni venta.
--
-- Se ejecuta UNA sola vez. Todo va dentro de una transacción: si algo falla, no cambia nada.

BEGIN;

CREATE TABLE ajuste_inventario (
    id_ajuste     integer GENERATED ALWAYS AS IDENTITY,
    fecha         timestamp(6) without time zone NOT NULL DEFAULT now(),
    id_usuario    integer     NOT NULL,
    id_sede       integer     NOT NULL,
    motivo        varchar(20) NOT NULL,
    observaciones varchar(200),
    CONSTRAINT ajuste_inventario_pkey PRIMARY KEY (id_ajuste),
    CONSTRAINT ajuste_inventario_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES usuario (id_usuario),
    CONSTRAINT ajuste_inventario_id_sede_fkey FOREIGN KEY (id_sede) REFERENCES sede (id_sede),
    CONSTRAINT chk_ajusteinventario_motivo CHECK (motivo IN ('conteo_inicial', 'conteo', 'merma', 'correccion'))
);

-- Una fila por producto contado en la zona, aunque coincida con el sistema
-- (así queda constancia de que se verificó).
CREATE TABLE detalle_ajuste (
    id_detalle        integer GENERATED ALWAYS AS IDENTITY,
    id_ajuste         integer NOT NULL,
    id_producto       integer NOT NULL,
    id_ubicacion      integer NOT NULL,
    cantidad_anterior integer NOT NULL,
    cantidad_nueva    integer NOT NULL,
    CONSTRAINT detalle_ajuste_pkey PRIMARY KEY (id_detalle),
    CONSTRAINT detalle_ajuste_id_ajuste_fkey FOREIGN KEY (id_ajuste) REFERENCES ajuste_inventario (id_ajuste),
    CONSTRAINT detalle_ajuste_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES producto (id_producto),
    CONSTRAINT detalle_ajuste_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES ubicacion (id_ubicacion),
    CONSTRAINT uq_detalleajuste_producto UNIQUE (id_ajuste, id_producto, id_ubicacion),
    CONSTRAINT chk_detalleajuste_cantidades CHECK (cantidad_anterior >= 0 AND cantidad_nueva >= 0)
);

COMMIT;

-- Comprobación: deben aparecer las dos tablas
SELECT table_name FROM information_schema.tables
WHERE table_name IN ('ajuste_inventario', 'detalle_ajuste')
ORDER BY table_name;
