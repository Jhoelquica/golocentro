-- Anulación de ventas (Golocentro)
--
-- Una nota de venta hecha por error o una venta cancelada se anula; no se borra.
-- La nota queda marcada como anulada con quién, cuándo y por qué, y la app devuelve el stock
-- a las mismas zonas de donde salió. Las anuladas no suman en los totales de ventas y su
-- número no se vuelve a usar.
--
-- Se ejecuta UNA sola vez. Todo va dentro de una transacción: si algo falla, no cambia nada.

BEGIN;

ALTER TABLE nota_venta
    ADD COLUMN estado               varchar(10) NOT NULL DEFAULT 'emitida',
    ADD COLUMN fecha_anulacion      timestamp(6) without time zone,
    ADD COLUMN id_usuario_anulacion integer,
    ADD COLUMN motivo_anulacion     varchar(200),
    ADD CONSTRAINT nota_venta_id_usuario_anulacion_fkey
        FOREIGN KEY (id_usuario_anulacion) REFERENCES usuario (id_usuario),
    ADD CONSTRAINT chk_notaventa_estado CHECK (estado IN ('emitida', 'anulada')),
    -- Una anulada siempre dice quién, cuándo y por qué; una emitida no tiene nada de eso
    ADD CONSTRAINT chk_notaventa_anulacion CHECK (
        (estado = 'emitida'
            AND fecha_anulacion IS NULL AND id_usuario_anulacion IS NULL AND motivo_anulacion IS NULL)
        OR
        (estado = 'anulada'
            AND fecha_anulacion IS NOT NULL AND id_usuario_anulacion IS NOT NULL
            AND length(trim(motivo_anulacion)) > 0)
    );

COMMIT;

-- Comprobación: deben aparecer las 4 columnas nuevas al final (estado con default 'emitida')
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_name = 'nota_venta'
ORDER BY ordinal_position;
