-- Datos del negocio (Golocentro)
--
-- La nota de venta tenía el nombre del negocio fijo en el código y no mostraba RUC ni teléfono.
-- Ahora la dueña los edita desde la app:
--   * negocio: una sola fila con nombre comercial, razón social, RUC, teléfono, correo y el
--     mensaje del pie de la nota.
--   * sede: teléfono propio de cada sede (si no tiene, la nota usa el del negocio).
--
-- Se ejecuta UNA sola vez. Todo va dentro de una transacción: si algo falla, no cambia nada.

BEGIN;

CREATE TABLE negocio (
    id_negocio       integer      NOT NULL DEFAULT 1,
    nombre_comercial varchar(100) NOT NULL,
    razon_social     varchar(150),
    ruc              varchar(11),
    telefono         varchar(15),
    correo           varchar(100),
    mensaje_nota     varchar(120),
    CONSTRAINT negocio_pkey PRIMARY KEY (id_negocio),
    -- Una sola fila: el negocio es uno aunque tenga varias sedes
    CONSTRAINT chk_negocio_unico CHECK (id_negocio = 1),
    CONSTRAINT chk_negocio_nombre CHECK (length(trim(nombre_comercial)) > 0),
    -- RUC peruano: 11 dígitos que empiezan con 10, 15, 17 o 20
    CONSTRAINT chk_negocio_ruc CHECK (ruc IS NULL OR ruc ~ '^(10|15|17|20)[0-9]{9}$')
);

INSERT INTO negocio (id_negocio, nombre_comercial, mensaje_nota)
VALUES (1, 'Distribuidora Golocentro', '¡Gracias por su compra!');

ALTER TABLE sede ADD COLUMN telefono varchar(15);

COMMIT;

-- Comprobación: debe mostrar la fila del negocio y las sedes con la columna telefono (vacía)
SELECT * FROM negocio;
SELECT id_sede, nombre, direccion, ciudad, telefono FROM sede ORDER BY id_sede;
