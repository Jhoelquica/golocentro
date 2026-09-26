# Gestión de Almacén – Golocentro

Aplicación web ASP.NET Core MVC (.NET 8) con Entity Framework Core y SQL Server.

## Requisitos

- **.NET 8 SDK**, o Visual Studio 2022 con la carga de trabajo *Desarrollo de ASP.NET y web* (ya trae el SDK).
- **SQL Server**: Express, Developer o LocalDB (LocalDB se instala con Visual Studio).
- **SSMS** (SQL Server Management Studio) para ejecutar los scripts. También sirve el *Explorador de objetos de SQL Server* de Visual Studio.
- Git, o descargar el ZIP desde GitHub.

Para comprobarlos, abre una terminal (cmd o PowerShell) y ejecuta:

```
dotnet --list-sdks      debe aparecer una versión 8.x
sqllocaldb info         si aparece MSSQLLocalDB, tienes LocalDB
git --version
```

## Puesta en marcha en otra PC

### 1. Descargar el código

El repositorio es privado: inicia sesión en GitHub cuando te lo pida.

```
git clone https://github.com/Jhoelquica/golocentro.git
cd golocentro
```

### 2. Crear la base de datos

La base de datos no está en el repositorio. Elige una opción:

**Opción A: base nueva para pruebas.** En SSMS conéctate a tu servidor y ejecuta, en este orden (abrir el archivo y pulsar F5):

1. `database/01_crear_bd.sql`: crea la BD `GestionAlmacen_Golocentro` y sus tablas.
2. `database/02_datos_prueba.sql`: crea una sede, dos ubicaciones, un proveedor, un cliente y los usuarios de prueba.

**Opción B: llevar la base real con sus datos.** En la PC original, en SSMS: clic derecho sobre la base → *Tareas* → *Copia de seguridad…* y guarda el `.bak`. En la otra PC: clic derecho en *Bases de datos* → *Restaurar base de datos…* → *Dispositivo* → elige el `.bak`. Las fotos subidas (`wwwroot/uploads` y `wwwroot/evidencias`) tampoco están en Git: cópialas a mano si las necesitas.

### 3. Configurar la conexión

`appsettings.json` no se sube a Git porque guarda la cadena de conexión. Copia la plantilla:

```
copy appsettings.Example.json appsettings.json
```

Edita `Server=` según tu SQL Server (en JSON la barra invertida se escribe doble, `\\`):

| Tu SQL Server | Valor de `Server` |
|---|---|
| LocalDB (viene con Visual Studio) | `(localdb)\\MSSQLLocalDB` |
| SQL Server Express | `localhost\\SQLEXPRESS` |
| Instancia por defecto | `localhost` |

Si no sabes cuál es, usa el nombre que aparece en *Nombre del servidor* al conectarte con SSMS. Si usaste la opción B con otro nombre de base, cambia también `Database=`.

### 4. Ejecutar

- **Visual Studio:** abre `GestionAlmacen_Golocentro.sln` y pulsa F5.
- **Terminal**, dentro de la carpeta del proyecto: `dotnet run`, y abre http://localhost:5252

### 5. Iniciar sesión

Con los datos de prueba (opción A), la contraseña de los tres usuarios es `admin123`:

| Usuario | Rol | Acceso |
|---|---|---|
| `admin` | dueña | Todas las sedes |
| `encargada1` | encargada | Solo Sede Principal |
| `trabajador1` | trabajador | Solo Sede Principal; sin reportes y sin crear, editar ni borrar productos |

En el login elige **Sede Principal**.

## Problemas comunes

| Síntoma | Causa y solución |
|---|---|
| Error 500: *The ConnectionString property has not been initialized* | Falta `appsettings.json` (paso 3). |
| *The certificate chain was issued by an authority that is not trusted* | Falta `TrustServerCertificate=True` en la cadena de conexión. |
| *A network-related or instance-specific error…* | El valor de `Server=` no coincide con tu SQL Server, o su servicio está detenido. |
| *Cannot open database "GestionAlmacen_Golocentro"* | No se ejecutó `01_crear_bd.sql`, o `Database=` no coincide con el nombre de tu base. |
| El combo *Sede* del login está vacío | La tabla `Sede` no tiene filas: ejecuta `02_datos_prueba.sql`. |
| *Usuario o contraseña incorrectos* | Las contraseñas deben estar guardadas como hash BCrypt, como en `02_datos_prueba.sql`. |
| No se ven los íconos | Los íconos (Bootstrap Icons) y la fuente se cargan desde internet. |
| *Clientes*, *Proveedores* y *Mapa Almacén* dan 404 | Esas pantallas aún no están implementadas; no es un problema de instalación. |

> En una PC compartida (por ejemplo, la del instituto), al terminar cierra tu sesión de GitHub y borra la carpeta del proyecto si contiene datos reales.
