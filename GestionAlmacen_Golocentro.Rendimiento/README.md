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
| `volumen` | Genera el volumen de medición dos veces y compara una huella md5 para comprobar que es reproducible: 2,000 productos, 50,000 movimientos y 150,000 detalles en 2 sedes, más notas, stock, traslados, conteos y alertas |
| `medir` | Con ese volumen, mide cada pantalla (ver detalle abajo) |

Qué hace `medir` en cada pantalla:
- Mide con `Stopwatch` 1 corrida de calentamiento y 5 medidas, y da la mediana y el máximo.
- Cuenta las consultas SQL y cuánto tardan: ejecución más lectura de todas las filas.
- Saca `EXPLAIN (ANALYZE, BUFFERS)` de la consulta más lenta en las pantallas principales y en cualquiera cuya mediana pase de 200 ms.
- Marca los Seq Scan sobre tablas grandes (más de 10,000 filas).

Los datos de prueba los arma `Generador.cs`:
- son inventados y se cargan con `COPY`;
- son reproducibles: misma semilla (`20261002`) y mismo volumen dan los mismos datos;
- las fechas se cuentan hacia atrás desde el día en que se corre.
