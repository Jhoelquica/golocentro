-- Ventas con nota de venta (Golocentro)
--
-- Cada venta (movimiento de tipo Salida) tiene una nota de venta con número correlativo,
-- montos (subtotal, descuento, total) y método de pago. El cliente puede ser
-- "Público en general" o uno identificado con celular para enviarle la nota por WhatsApp.
--
-- Se ejecuta UNA sola vez. Todo va dentro de una transacción: si algo falla, no cambia nada.

BEGIN;

-- 1) Celular del cliente (para WhatsApp). Se copia de "contacto" cuando ahí hay un celular peruano.
ALTER TABLE cliente ADD COLUMN celular varchar(15);
UPDATE cliente SET celular = contacto WHERE contacto ~ '^9[0-9]{8}$';

-- 2) Cliente genérico para ventas sin identificar (chk_cliente_rucdni exige 8 a 11 caracteres)
INSERT INTO cliente (nombre, ruc_dni)
SELECT 'Público en general', '00000000'
WHERE NOT EXISTS (SELECT 1 FROM cliente WHERE ruc_dni = '00000000');

-- 3) Nota de venta: una por venta, con numeración correlativa por serie
CREATE TABLE nota_venta (
    id_nota       integer GENERATED ALWAYS AS IDENTITY,
    id_movimiento integer       NOT NULL,
    serie         varchar(4)    NOT NULL DEFAULT 'NV01',
    numero        integer       NOT NULL,
    subtotal      numeric(10,2) NOT NULL,
    descuento     numeric(10,2) NOT NULL DEFAULT 0,
    total         numeric(10,2) NOT NULL,
    metodo_pago   varchar(15)   NOT NULL,
    CONSTRAINT nota_venta_pkey PRIMARY KEY (id_nota),
    CONSTRAINT nota_venta_id_movimiento_fkey FOREIGN KEY (id_movimiento) REFERENCES movimiento (id_movimiento),
    CONSTRAINT uq_notaventa_movimiento UNIQUE (id_movimiento),
    CONSTRAINT uq_notaventa_numero UNIQUE (serie, numero),
    CONSTRAINT chk_notaventa_montos CHECK (
        subtotal >= 0 AND descuento >= 0 AND descuento <= subtotal AND total = subtotal - descuento),
    CONSTRAINT chk_notaventa_metodo CHECK (metodo_pago IN ('efectivo', 'yape', 'plin', 'transferencia', 'tarjeta'))
);

COMMIT;

-- Comprobación: debe mostrar el cliente genérico y los clientes con celular copiado
SELECT id_cliente, nombre, ruc_dni, contacto, celular FROM cliente ORDER BY id_cliente;
