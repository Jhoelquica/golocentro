-- =====================================================================================
-- ARRANQUE REAL: borra el historial de PRUEBA y deja el stock en cero.
-- Correr UNA sola vez, en la base del SERVIDOR, justo antes de empezar a trabajar de verdad.
--
-- Se borra:    ventas y notas de venta, entradas, traslados, conteos, registros de fotos
--              de evidencia, alertas y el stock de todas las zonas.
-- Se mantiene: datos del negocio, sedes (incluida "Sede norte"), zonas y plano, usuarios,
--              productos, clientes y proveedores.
--
-- La numeración de las notas vuelve a empezar: la primera venta real será NV01-000001.
-- Después hay que cargar el stock real con un conteo por zona
-- (Conteo de inventario → Contar → motivo "Conteo inicial"). Ver DESPLIEGUE.md, paso 9.
-- =====================================================================================

-- 1) Vista previa (solo mira, no borra nada): lo que se va a eliminar
SELECT 'Notas de venta'           AS que, count(*) AS cantidad FROM nota_venta
UNION ALL SELECT 'Entradas y ventas', count(*) FROM movimiento
UNION ALL SELECT 'Traslados',         count(*) FROM traslado
UNION ALL SELECT 'Conteos',           count(*) FROM ajuste_inventario
UNION ALL SELECT 'Fotos de evidencia', count(*) FROM evidencia
UNION ALL SELECT 'Alertas',           count(*) FROM alerta
UNION ALL SELECT 'Filas de stock',    count(*) FROM producto_ubicacion;

-- 2) Borrado. Si alguna otra tabla dependiera de estas, TRUNCATE da error y no se borra nada.
BEGIN;

TRUNCATE evidencia, nota_venta, detalle_movimiento, movimiento,
         detalle_traslado, traslado,
         detalle_ajuste, ajuste_inventario,
         alerta, reporte,
         producto_ubicacion
    RESTART IDENTITY;

-- 3) Verificación: todo debe salir en 0
SELECT 'Notas de venta'           AS que, count(*) AS cantidad FROM nota_venta
UNION ALL SELECT 'Entradas y ventas', count(*) FROM movimiento
UNION ALL SELECT 'Traslados',         count(*) FROM traslado
UNION ALL SELECT 'Conteos',           count(*) FROM ajuste_inventario
UNION ALL SELECT 'Fotos de evidencia', count(*) FROM evidencia
UNION ALL SELECT 'Alertas',           count(*) FROM alerta
UNION ALL SELECT 'Filas de stock',    count(*) FROM producto_ubicacion;

COMMIT;

-- 4) Lo que queda, para revisarlo en el sistema (productos, clientes o cuentas de prueba se borran o desactivan desde la app)
SELECT 'Sedes' AS que, count(*) AS cantidad FROM sede
UNION ALL SELECT 'Zonas',       count(*) FROM ubicacion
UNION ALL SELECT 'Usuarios',    count(*) FROM usuario
UNION ALL SELECT 'Productos',   count(*) FROM producto
UNION ALL SELECT 'Clientes',    count(*) FROM cliente
UNION ALL SELECT 'Proveedores', count(*) FROM proveedor;
