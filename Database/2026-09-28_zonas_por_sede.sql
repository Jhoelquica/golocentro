-- Zonas por sede (Golocentro)
--
-- El código de zona era único en TODA la base: una segunda sede no podía tener su propia
-- "A-01" ni su propia "R-01". Ahora el código es único dentro de cada sede.
-- No cambia ningún dato; solo la regla.
--
-- Se ejecuta UNA sola vez. Todo va dentro de una transacción: si algo falla, no cambia nada.

BEGIN;

-- La regla vieja puede existir como restricción o como índice, según cómo se creó la tabla
ALTER TABLE ubicacion DROP CONSTRAINT IF EXISTS ubicacion_codigo_estante_key;
DROP INDEX IF EXISTS ubicacion_codigo_estante_key;

ALTER TABLE ubicacion
    ADD CONSTRAINT uq_ubicacion_sede_codigo UNIQUE (id_sede, codigo_estante);

COMMIT;

-- Comprobación: debe aparecer uq_ubicacion_sede_codigo y ya no ubicacion_codigo_estante_key
SELECT conname, pg_get_constraintdef(oid) AS definicion
FROM pg_constraint
WHERE conrelid = 'ubicacion'::regclass AND contype = 'u';
