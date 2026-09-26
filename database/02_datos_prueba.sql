-- =====================================================================
-- Datos minimos para iniciar sesion y probar el sistema en una BD
-- recien creada con 01_crear_bd.sql. SOLO PARA PRUEBAS: no lo ejecutes
-- sobre la BD real.
--
-- Usuarios que crea (la contrasena de los tres es: admin123)
--     admin        rol duena        ve todas las sedes
--     encargada1   rol encargada    solo Sede Principal
--     trabajador1  rol trabajador   solo Sede Principal
--
-- La app no tiene pantallas para crear sedes, ubicaciones, usuarios ni
-- proveedores, por eso se insertan aqui. El login exige elegir una sede,
-- asi que debe existir al menos una.
--
-- (Archivo sin tildes a proposito: asi se lee igual con cualquier
-- codificacion.)
-- =====================================================================

USE GestionAlmacen_Golocentro;
GO

INSERT INTO Sede (nombre, direccion) VALUES (N'Sede Principal', N'Direccion de prueba');
DECLARE @sede int = SCOPE_IDENTITY();

INSERT INTO Ubicacion (codigo_estante, descripcion, capacidad, id_sede) VALUES
    (N'A-01', N'Estante A, nivel 1', 100, @sede),
    (N'A-02', N'Estante A, nivel 2', 100, @sede);

-- Hash BCrypt de "admin123" (la app valida las contrasenas con BCrypt.Net-Next).
DECLARE @hash nvarchar(255) = N'$2a$11$2/6pcEtRtMXPsJY99mLsheOzkdljINXgJvY1byphIH5p0qS7A5AbO';
-- El rol debe ser exactamente "duena" con enie; NCHAR(241) es esa letra.
DECLARE @duena nvarchar(20) = N'due' + NCHAR(241) + N'a';

INSERT INTO Usuario (nombre, rol, usuario, contrasena, id_sede) VALUES
    (N'Administradora de prueba', @duena,        N'admin',       @hash, NULL),
    (N'Encargada de prueba',      N'encargada',  N'encargada1',  @hash, @sede),
    (N'Trabajador de prueba',     N'trabajador', N'trabajador1', @hash, @sede);

INSERT INTO Proveedor (nombre, ruc) VALUES (N'Proveedor de prueba', N'20123456789');
INSERT INTO Cliente (nombre, ruc_dni) VALUES (N'Cliente de prueba', N'12345678');
GO
