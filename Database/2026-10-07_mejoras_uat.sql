-- Mejoras pedidas en la UAT del 06/10/2026 (Golocentro)
--
-- 1) Celular del proveedor: para los botones de WhatsApp y llamar.
-- 2) Vencimiento y lote por línea de entrada: cada entrada guarda los de su mercadería.
-- 3) Presentaciones de venta y compra:
--    - Cada producto puede tener presentaciones con su cantidad en unidades base y su propio precio.
--      Ejemplos: Doritos, bolsa = 8 tiras; galleta X, caja = N packs.
--    - La unidad base sigue siendo producto.unidad_medida, con su precio producto.precio_unitario.
--    - El stock se sigue contando en la unidad base.
--    - Cada línea de movimiento guarda con qué presentación se vendió o compró y cuántas unidades base
--      trae cada una (factor). La cantidad de la línea sigue estando en unidades base.
--    - El precio de la línea es el de la presentación vendida.
--
-- No cambia ni borra datos:
--   - Las líneas que ya existen quedan con factor 1 (unidad base), sin vencimiento ni lote.
--   - Los proveedores quedan sin celular.
-- Se puede correr más de una vez: lo que ya existe no se vuelve a crear.
-- Todo va dentro de una transacción: si algo falla, no cambia nada.

BEGIN;

-- 1) Celular del proveedor (9 dígitos; la aplicación lo valida igual que el del cliente)
ALTER TABLE public.proveedor
    ADD COLUMN IF NOT EXISTS celular character varying(15);

-- 2) Vencimiento y lote de cada línea de entrada (en las ventas quedan vacíos)
ALTER TABLE public.detalle_movimiento
    ADD COLUMN IF NOT EXISTS fecha_vencimiento date,
    ADD COLUMN IF NOT EXISTS lote character varying(50);

-- 3a) Presentaciones de cada producto
CREATE TABLE IF NOT EXISTS public.producto_presentacion (
    id_presentacion integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_producto integer NOT NULL REFERENCES public.producto (id_producto) ON DELETE CASCADE,
    nombre character varying(30) NOT NULL,
    factor integer NOT NULL,
    precio numeric(10,2) NOT NULL,
    CONSTRAINT chk_productopresentacion_nombre CHECK (length(TRIM(BOTH FROM nombre)) > 0),
    CONSTRAINT chk_productopresentacion_factor CHECK (factor > 1),
    CONSTRAINT chk_productopresentacion_precio CHECK (precio >= 0),
    -- También sirve de índice para buscar las presentaciones de un producto
    CONSTRAINT uq_productopresentacion_nombre UNIQUE (id_producto, nombre)
);

-- 3b) Con qué presentación se vendió o compró cada línea
ALTER TABLE public.detalle_movimiento
    ADD COLUMN IF NOT EXISTS presentacion character varying(30),
    ADD COLUMN IF NOT EXISTS factor integer NOT NULL DEFAULT 1;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_detallemovimiento_factor') THEN
        -- La cantidad (en unidades base) es siempre un número entero de presentaciones
        ALTER TABLE public.detalle_movimiento
            ADD CONSTRAINT chk_detallemovimiento_factor CHECK (factor >= 1 AND cantidad % factor = 0);
    END IF;
END $$;

COMMIT;

-- Comprobación 1: deben salir 5 filas
--   proveedor.celular y detalle_movimiento con fecha_vencimiento, lote, presentacion y factor (default 1)
SELECT table_name, column_name, data_type, column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND ((table_name = 'proveedor' AND column_name = 'celular')
    OR (table_name = 'detalle_movimiento' AND column_name IN ('fecha_vencimiento', 'lote', 'presentacion', 'factor')))
ORDER BY table_name, column_name;

-- Comprobación 2: la tabla nueva con sus restricciones
--   Debe salir: 3 CHECK, 1 FOREIGN KEY, 1 PRIMARY KEY y 1 UNIQUE.
SELECT conname, pg_get_constraintdef(oid) AS definicion
FROM pg_constraint
WHERE conrelid = 'public.producto_presentacion'::regclass
  AND contype <> 'n'
ORDER BY conname;

-- Comprobación 3: todas las líneas que ya existían quedaron con factor 1
--   Las dos cifras deben ser iguales.
SELECT count(*) AS lineas, count(*) FILTER (WHERE factor = 1) AS con_factor_1
FROM public.detalle_movimiento;
