-- Corregir el pasillo superior del plano de la Sede Principal (Golocentro)
--
-- El script 2026-09-24_plano_almacen.sql creó el pasillo de (22, 3.7) a (0, 3.7). El editor del plano,
-- hecho después, solo acepta posiciones en pasos de medio cuadrito (3.5 o 4, no 3.7), así que el plano
-- de la Sede Principal no se puede volver a guardar tal como está ("Una línea se sale del plano.").
-- Este script lo mueve a 3.5: en el Mapa sube 0.2 de cuadrito, casi imperceptible.
--
-- Se puede correr más de una vez: si el pasillo ya está en 3.5, el UPDATE no encuentra la fila
-- y no cambia nada. Todo va dentro de una transacción.

BEGIN;

-- 1) La fila del pasillo (antes): debe salir una sola, con y1 = y2 = 3.7 (o 3.5 si ya se corrigió)
SELECT pl.id_linea, s.nombre AS sede, pl.tipo, pl.x1, pl.y1, pl.x2, pl.y2
FROM plano_linea pl
JOIN sede s ON s.id_sede = pl.id_sede
WHERE s.nombre = 'Sede Principal'
  AND pl.tipo = 'pasillo' AND pl.x1 = 22 AND pl.x2 = 0
  AND pl.y1 = pl.y2 AND pl.y1 IN (3.7, 3.5);

-- 2) Corrección: solo si sigue en 3.7
UPDATE plano_linea pl
SET y1 = 3.5, y2 = 3.5
FROM sede s
WHERE s.id_sede = pl.id_sede
  AND s.nombre = 'Sede Principal'
  AND pl.tipo = 'pasillo' AND pl.x1 = 22 AND pl.x2 = 0
  AND pl.y1 = 3.7 AND pl.y2 = 3.7;

-- 3) La misma fila (después): debe decir 3.5
SELECT pl.id_linea, s.nombre AS sede, pl.tipo, pl.x1, pl.y1, pl.x2, pl.y2
FROM plano_linea pl
JOIN sede s ON s.id_sede = pl.id_sede
WHERE s.nombre = 'Sede Principal'
  AND pl.tipo = 'pasillo' AND pl.x1 = 22 AND pl.x2 = 0
  AND pl.y1 = pl.y2 AND pl.y1 IN (3.7, 3.5);

-- 4) Revisión de todas las sedes: líneas, zonas o tamaños de plano que NO están en medios cuadritos.
--    Debe salir vacío; si aparece algo, el editor tampoco dejará guardar ese plano.
SELECT 'linea' AS que, s.nombre AS sede, pl.tipo || ' (' || pl.x1 || ', ' || pl.y1 || ') -> (' || pl.x2 || ', ' || pl.y2 || ')' AS detalle
FROM plano_linea pl
JOIN sede s ON s.id_sede = pl.id_sede
WHERE pl.x1 * 2 <> trunc(pl.x1 * 2) OR pl.y1 * 2 <> trunc(pl.y1 * 2)
   OR pl.x2 * 2 <> trunc(pl.x2 * 2) OR pl.y2 * 2 <> trunc(pl.y2 * 2)
UNION ALL
SELECT 'zona', s.nombre, u.codigo_estante || ' (' || u.pos_x || ', ' || u.pos_y || ') ' || u.ancho || ' x ' || u.alto
FROM ubicacion u
JOIN sede s ON s.id_sede = u.id_sede
WHERE u.pos_x IS NOT NULL
  AND (u.pos_x * 2 <> trunc(u.pos_x * 2) OR u.pos_y * 2 <> trunc(u.pos_y * 2)
       OR u.ancho * 2 <> trunc(u.ancho * 2) OR u.alto * 2 <> trunc(u.alto * 2))
UNION ALL
SELECT 'plano', s.nombre, s.plano_ancho || ' x ' || s.plano_alto
FROM sede s
WHERE s.plano_ancho IS NOT NULL
  AND (s.plano_ancho * 2 <> trunc(s.plano_ancho * 2) OR s.plano_alto * 2 <> trunc(s.plano_alto * 2));

COMMIT;
