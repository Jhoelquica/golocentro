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
