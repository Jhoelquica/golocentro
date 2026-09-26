-- =====================================================================
-- Datos de prueba para una base recién creada con 01_esquema.sql.
-- SOLO PARA PROBAR: no lo ejecutes sobre la base real.
--
-- Crea:
--   * la sede «Sede Principal» con el plano del almacén (30 x 20) y sus
--     20 zonas, en las mismas posiciones que Database/2026-09-24_plano_almacen.sql;
--   * un proveedor de prueba;
--   * tres usuarios, todos con la contraseña admin123:
--       admin        dueña        ve todas las sedes
--       encargada1   encargada    Sede Principal
--       trabajador1  trabajador   Sede Principal
--
-- No crea productos: agrégalos desde la app y dales stock con una entrada.
-- Todo va dentro de una transacción: si algo falla, no se inserta nada.
-- =====================================================================

SET client_encoding = 'UTF8';

BEGIN;

INSERT INTO sede (nombre, direccion, plano_ancho, plano_alto)
VALUES ('Sede Principal', 'Dirección de prueba', 30, 20);

INSERT INTO ubicacion (codigo_estante, descripcion, tipo, id_sede, pos_x, pos_y, ancho, alto)
SELECT v.codigo, v.descripcion, v.tipo, s.id_sede, v.x, v.y, v.w, v.h
FROM (SELECT id_sede FROM sede WHERE nombre = 'Sede Principal') AS s,
(VALUES
    ('A-01', 'Cuarto 1, pared del fondo',       'almacenaje',  5,   0,    4,  2.5),
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
FROM (SELECT id_sede FROM sede WHERE nombre = 'Sede Principal') AS s,
(VALUES
    ('entrada',   0, 1.5,   0,   5),
    ('division',  4,   5,   4,  13),
    ('division',  4,  13,  30,  13),
    ('pasillo',  22, 3.7,   0, 3.7),
    ('pasillo',   3,   5,   3, 16.5),
    ('pasillo',   3, 16.5, 21, 16.5)
) AS v(tipo, x1, y1, x2, y2);

INSERT INTO proveedor (nombre, ruc) VALUES ('Proveedor de prueba', '20123456789');

-- Hash BCrypt de "admin123" (la app valida las contraseñas con BCrypt.Net-Next).
-- La dueña no tiene sede: ve todas.
INSERT INTO usuario (nombre, rol, usuario, contrasena, id_sede)
SELECT v.nombre, v.rol, v.usuario,
       '$2a$11$2/6pcEtRtMXPsJY99mLsheOzkdljINXgJvY1byphIH5p0qS7A5AbO',
       CASE WHEN v.rol = 'duena' THEN NULL ELSE s.id_sede END
FROM (SELECT id_sede FROM sede WHERE nombre = 'Sede Principal') AS s,
(VALUES
    ('Administradora de prueba', 'duena',      'admin'),
    ('Encargada de prueba',      'encargada',  'encargada1'),
    ('Trabajador de prueba',     'trabajador', 'trabajador1')
) AS v(nombre, rol, usuario);

COMMIT;
