# Medición de rendimiento (apartado 2.3)

Herramienta de consola. **No es parte del sistema ni de las pruebas.**

- Cada paso levanta su propio PostgreSQL 18 desechable en Docker (`postgres:18-alpine`) y le carga `Database/esquema.sql`.
- Al terminar, el contenedor se borra.
- **Nunca toca `golocentro_pg`** y no usa contraseñas de nadie: el contenedor trae credenciales propias y temporales.

Requisito: Docker Desktop abierto ("Engine running").

```powershell
dotnet run -c Release --project GestionAlmacen_Golocentro.Rendimiento -- inventario
```

| Paso | Qué hace |
|---|---|
| `inventario` | Tablas, índices (de la base y del modelo de EF) y claves foráneas, marcando cuáles no tienen índice |
| `auditoria` | Cuenta las consultas SQL de cada pantalla de lista o reporte con dos volúmenes de datos (el segundo 10 veces mayor). Si el número crece con los datos, hay N+1 |

Los datos de prueba los arma `Generador.cs`:
- son inventados y se cargan con `COPY`;
- son reproducibles: misma semilla (`20261002`) y mismo volumen dan los mismos datos;
- las fechas se cuentan hacia atrás desde el día en que se corre.
