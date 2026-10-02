-- Índice para leer el detalle de un movimiento (Golocentro · informe EFSRT IV, apartado 2.3, paso e)
--
-- Por qué:
--   La tabla detalle_movimiento no tiene índice en id_movimiento. PostgreSQL no indexa las claves foráneas
--   por su cuenta.
--   La lista de Movimientos y el Reporte de entradas cuentan los productos y suman las unidades de cada
--   movimiento. Sin el índice, PostgreSQL recorre detalle_movimiento entera dos veces por cada movimiento
--   de la lista.
--
-- Qué se midió:
--   Se usó una base desechable con 50,000 movimientos y 150,000 detalles de prueba.
--   - La lista de Movimientos de 3 meses tardó 47.8 s (mediana de 5 corridas).
--   - El Reporte de entradas del mes tardó 1.3 s.
--   La medición antes/después está en la herramienta GestionAlmacen_Golocentro.Rendimiento, paso "indice".
--
-- Qué cambia:
--   Solo agrega un índice: no cambia datos, columnas ni código.
--   Se puede correr más de una vez (IF NOT EXISTS).
--   Con los datos de hoy tarda menos de un segundo; mientras se crea, las escrituras en detalle_movimiento
--   esperan.

CREATE INDEX IF NOT EXISTS detalle_movimiento_id_movimiento_idx
    ON public.detalle_movimiento (id_movimiento);

-- Estadísticas al día para que el planificador use el índice desde ya
ANALYZE public.detalle_movimiento;

-- Comprobación: debe aparecer detalle_movimiento_id_movimiento_idx junto a la clave primaria
SELECT indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public' AND tablename = 'detalle_movimiento'
ORDER BY indexname;
