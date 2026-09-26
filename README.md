# Gestión de Almacén – Golocentro

Aplicación web ASP.NET Core MVC (.NET 8) con Entity Framework Core y PostgreSQL.

## Requisitos

- **.NET 8 SDK**, o Visual Studio 2022 con la carga de trabajo *Desarrollo de ASP.NET y web* (ya trae el SDK).
- **PostgreSQL** 16 (o una versión reciente), con la contraseña del usuario `postgres`.
- Git, o descargar el ZIP desde GitHub.

Para comprobarlos, abre PowerShell y ejecuta:

```
dotnet --list-sdks        debe aparecer una versión 8.x
Get-Service *postgres*    debe aparecer un servicio postgresql-... en estado Running
```

## Puesta en marcha en otra PC

### 1. Descargar el código

El repositorio es privado: inicia sesión en GitHub cuando te lo pida.

```
git clone https://github.com/Jhoelquica/golocentro.git
cd golocentro
```

Si el código que quieres probar está en otra rama, agrega `-b nombre-de-la-rama` al `git clone`.

Si descargas el ZIP, al descomprimirlo Windows suele dejar una carpeta dentro de otra con el mismo nombre. Entra hasta la que contiene `GestionAlmacen_Golocentro.csproj`.

### 2. Crear la base de datos

Ejecuta en PowerShell, dentro de la carpeta del proyecto. `psql` pedirá la contraseña del usuario `postgres` en cada comando:

```powershell
$psql = (Get-ChildItem "C:\Program Files\PostgreSQL\*\bin\psql.exe" | Select-Object -Last 1).FullName
& $psql -h localhost -U postgres -c "CREATE DATABASE golocentro;"
& $psql -h localhost -U postgres -d golocentro -v ON_ERROR_STOP=1 -f Database/instalacion/01_esquema.sql
& $psql -h localhost -U postgres -d golocentro -v ON_ERROR_STOP=1 -f Database/instalacion/02_datos_prueba.sql
```

- `01_esquema.sql` crea todas las tablas, sus restricciones y el cliente «Público en general», que las ventas necesitan.
- `02_datos_prueba.sql` crea una sede con el plano del almacén y sus 20 zonas, un proveedor y los usuarios de prueba. No lo ejecutes sobre la base real.

También puedes hacerlo con pgAdmin: crea la base `golocentro`, abre la *Query Tool* sobre ella y ejecuta los dos archivos en ese orden.

Los scripts con fecha de `Database/` (por ejemplo, `2026-09-25_nota_venta.sql`) sirven para actualizar una base que ya existía. Una base creada con `Database/instalacion/` ya los incluye: no los ejecutes sobre ella.

### 3. Configurar la conexión

`appsettings.json` no se sube a Git porque guarda la contraseña de la base de datos. Copia la plantilla:

```
copy appsettings.Example.json appsettings.json
```

Abre `appsettings.json` y cambia `TU_CONTRASENA` por la contraseña del usuario `postgres`. Si usaste otro nombre de base, cambia también `Database=`.

### 4. Ejecutar

- **Visual Studio:** abre `GestionAlmacen_Golocentro.sln` y pulsa F5.
- **Terminal**, dentro de la carpeta del proyecto: `dotnet run`, y abre http://localhost:5252

La primera vez descarga los paquetes NuGet, así que necesita internet.

### 5. Iniciar sesión

Con los datos de prueba, la contraseña de los tres usuarios es `admin123`:

| Usuario | Rol | Qué puede hacer |
|---|---|---|
| `admin` | dueña | Todo, en todas las sedes, incluida la gestión de usuarios |
| `encargada1` | encargada | Además de vender y contar, gestiona productos, clientes, proveedores y reportes de su sede |
| `trabajador1` | trabajador | Vende, registra entradas, anula ventas, mueve mercadería y cuenta inventario en su sede |

## Problemas comunes

| Síntoma | Causa y solución |
|---|---|
| Error 500: *The ConnectionString property has not been initialized* | Falta `appsettings.json` (paso 3). |
| *password authentication failed for user "postgres"* | La contraseña de `appsettings.json` no es la del usuario `postgres`. |
| *database "golocentro" does not exist* | No se creó la base (paso 2), o `Database=` no coincide con su nombre. |
| *Failed to connect to 127.0.0.1:5432* | El servicio de PostgreSQL está detenido: revisa `Get-Service *postgres*`. |
| *psql no se reconoce como nombre de un cmdlet* | Usa la variable `$psql` del paso 2, que busca `psql.exe` en la carpeta de PostgreSQL. |
| *relation "cliente" already exists* | La base ya tenía las tablas. Para empezar de cero: `DROP DATABASE golocentro;` y repite el paso 2. |
| No se ven los íconos, o la nota de venta no se descarga en PDF | Los íconos, la fuente y la librería del PDF se cargan desde internet. |

> En una PC compartida (por ejemplo, la del instituto), al terminar cierra tu sesión de GitHub y borra la carpeta del proyecto si contiene datos reales.
