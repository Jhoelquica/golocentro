# Golocentro: poner el sistema en un servidor

Guía para instalar Golocentro en un servidor pequeño con **Ubuntu 22.04 o 24.04**, con Nginx delante y PostgreSQL en la misma máquina.

**Tamaño mínimo:** 1 vCPU, 1 GB de RAM y 20 GB de disco. Con 1 GB de RAM conviene activar 1 GB de swap (paso 1).

- El sistema usa unos 150 MB.
- PostgreSQL usa unos 100 MB.
- Nginx usa unos 10 MB.

> Las contraseñas (base de datos, cuentas) las escribes tú en el servidor.
> Nunca van en el código ni en GitHub.

---

## Forma rápida: clonar el repositorio y correr los scripts de `deploy/`

Los scripts hacen los pasos de esta guía y guardan un registro de cada uno en `~/evidencias` del servidor (sirve de evidencia).

Sirven también en un **servidor compartido**:
- no actualizan todo el sistema;
- no cambian la hora del servidor, porque la hora de Perú va solo en el servicio de Golocentro (`TZ`) y en su conexión a la base;
- no tocan el firewall;
- no tocan las otras webs de Nginx.

**Antes de empezar se necesitan dos cosas:**
- **El dominio** apuntando a la IP pública del servidor. Con DuckDNS: se entra a duckdns.org, se crea el subdominio (por ejemplo `golocentro`) y en *current ip* se pone la IP del servidor.
- **La copia de la base** (`golocentro.sql`). La saca la dueña del sistema en su PC y la envía por privado; **no está en el repositorio porque tiene las cuentas y los datos del negocio**. Te pedirá la contraseña de su PostgreSQL local:
  ```powershell
  & "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe" -h localhost -U postgres --no-owner --no-privileges -d golocentro -f "$env:USERPROFILE\golocentro.sql"
  ```

**En el servidor:**

```bash
git clone https://github.com/Jhoelquica/golocentro.git
cd golocentro/deploy
nano config.sh          # revisar DOMINIO (y el puerto interno si 5080 ya está ocupado)
```

Luego se corren en orden:

| Comando | Qué hace |
|---|---|
| `bash 00-diagnostico.sh` | Revisa el servidor sin cambiar nada |
| `sudo bash 01-preparar-servidor.sh` | Instala .NET 8 SDK, PostgreSQL y Nginx (solo lo que falte) y crea las carpetas |
| `sudo bash 02-base-de-datos.sh /ruta/golocentro.sql` | Crea la base y carga la copia; pide una contraseña nueva para la base y al final borra la copia |
| `sudo bash 03-instalar-app.sh` | Compila el sistema y lo instala como servicio |
| `sudo bash 04-nginx-https.sh` | Publica el sistema con el dominio y pone HTTPS (pide un correo para Let's Encrypt) |
| `sudo bash 05-respaldos.sh` | Copia de seguridad diaria |
| `sudo bash 06-arranque-real.sh` | Borra el historial de prueba (pide escribir BORRAR) |
| `bash 07-verificacion.sh` | Comprueba que todo funcione y junta los registros en `~/evidencias-golocentro-FECHA.tar.gz` |

Si algún paso falla, el registro de ese paso queda en `~/evidencias/`.

**Para actualizar** a una versión nueva:

```bash
cd golocentro
git pull
sudo bash deploy/03-instalar-app.sh
```

Si la versión nueva trae un script en `Database/`, se corre antes.

Lo que sigue es la guía paso a paso, por si se hace a mano.

---

## 1. Preparar el servidor (una sola vez)

```bash
sudo apt update && sudo apt upgrade -y

# Hora de Perú: el sistema guarda la fecha de ventas y entradas con la hora del servidor
sudo timedatectl set-timezone America/Lima

# .NET 8 (solo el runtime de ASP.NET Core), PostgreSQL y Nginx
sudo apt install -y aspnetcore-runtime-8.0 postgresql nginx

# Swap de 1 GB (solo si el servidor tiene 1 GB de RAM o menos)
sudo fallocate -l 1G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

En Ubuntu 22.04, si `aspnetcore-runtime-8.0` no aparece, primero agrega el repositorio de Microsoft.

> ⚠️ **La zona horaria es importante.** Sin ella, las ventas de la tarde quedarían con fecha del día siguiente.

## 2. Base de datos

Crea el usuario y la base. Pon tu propia contraseña donde dice `CONTRASEÑA_BD`:

```bash
sudo -u postgres psql -c "CREATE USER golocentro_app WITH PASSWORD 'CONTRASEÑA_BD';"
sudo -u postgres psql -c "CREATE DATABASE golocentro OWNER golocentro_app;"
```

Copia la base de tu PC al servidor. Tiene las tablas, el catálogo, las sedes, las zonas y los usuarios.

1. **En tu PC (Windows, PowerShell)**, desde la carpeta `bin` de PostgreSQL. Cambia `NOMBRE_BD_LOCAL` por el nombre de tu base local:
   ```powershell
   .\pg_dump.exe -h localhost -U postgres -Fc --no-owner --no-privileges -d NOMBRE_BD_LOCAL -f golocentro.dump
   scp golocentro.dump usuario@IP_DEL_SERVIDOR:/tmp/
   ```
2. **En el servidor:**
   ```bash
   pg_restore -h localhost -U golocentro_app --no-owner --no-privileges -d golocentro /tmp/golocentro.dump
   rm /tmp/golocentro.dump
   ```

## 3. Carpetas y configuración

```bash
sudo mkdir -p /var/www/golocentro /var/lib/golocentro/llaves /etc/golocentro
sudo chown www-data:www-data /var/lib/golocentro/llaves
sudo chmod 700 /var/lib/golocentro/llaves

sudo nano /etc/golocentro/golocentro.env
```

Contenido de `golocentro.env`, en una sola línea, con tu contraseña:

```
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=golocentro;Username=golocentro_app;Password=CONTRASEÑA_BD
```

Luego deja el archivo legible solo para el administrador:

```bash
sudo chmod 600 /etc/golocentro/golocentro.env
```

## 4. Publicar el sistema

1. **En tu PC**, desde la carpeta del proyecto:
   ```powershell
   dotnet publish GestionAlmacen_Golocentro.csproj -c Release -o publish
   scp -r publish usuario@IP_DEL_SERVIDOR:/tmp/golocentro-nuevo
   ```
   El `appsettings.json` de tu PC no se copia a propósito: tiene la contraseña de tu base local. En el servidor, la conexión sale de `golocentro.env`.
2. **En el servidor:**
   ```bash
   sudo rsync -a --delete --exclude 'wwwroot/evidencias/' --exclude 'wwwroot/uploads/' /tmp/golocentro-nuevo/ /var/www/golocentro/
   sudo mkdir -p /var/www/golocentro/wwwroot/evidencias /var/www/golocentro/wwwroot/uploads/perfiles
   sudo chown -R www-data:www-data /var/www/golocentro/wwwroot/evidencias /var/www/golocentro/wwwroot/uploads
   rm -rf /tmp/golocentro-nuevo
   ```

## 5. Servicio (arranca solo y se reinicia si falla)

```bash
sudo nano /etc/systemd/system/golocentro.service
```

```ini
[Unit]
Description=Golocentro
After=network.target postgresql.service

[Service]
WorkingDirectory=/var/www/golocentro
ExecStart=/usr/bin/dotnet /var/www/golocentro/GestionAlmacen_Golocentro.dll
Restart=always
RestartSec=10
User=www-data
EnvironmentFile=/etc/golocentro/golocentro.env
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=Golocentro__CarpetaLlaves=/var/lib/golocentro/llaves
Environment=Logging__LogLevel__Default=Warning
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now golocentro
sudo systemctl status golocentro        # debe decir "active (running)"
```

## 6. Nginx (la puerta de entrada)

```bash
sudo nano /etc/nginx/sites-available/golocentro
```

```nginx
server {
    listen 80;
    server_name TU_DOMINIO;             # p. ej. sistema.golocentro.pe (o la IP si aún no tienes dominio)

    client_max_body_size 20m;           # fotos de evidencia (hasta 5 MB cada una)

    gzip on;                            # páginas más livianas para el celular
    gzip_types text/css application/javascript application/json image/svg+xml;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/golocentro /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx
```

## 7. HTTPS (candado)

Sin HTTPS, la contraseña viaja sin cifrar por internet. Necesitas un dominio que apunte a la IP del servidor. Luego:

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d TU_DOMINIO
```

El certificado se renueva solo.

## 8. Copias de seguridad diarias

```bash
sudo nano /etc/cron.daily/golocentro-respaldo
```

```sh
#!/bin/sh
# Base de datos y fotos, cada día; se guardan las de los últimos 14 días
set -e
D=/var/backups/golocentro
mkdir -p "$D"
sudo -u postgres pg_dump -Fc golocentro > "$D/bd-$(date +%F).dump"
tar -czf "$D/fotos-$(date +%F).tar.gz" -C /var/www/golocentro/wwwroot evidencias uploads
find "$D" -type f -mtime +14 -delete
```

```bash
sudo chmod +x /etc/cron.daily/golocentro-respaldo
```

> Descarga una copia a tu PC cada semana, por ejemplo con `scp`. Si el servidor se daña, las copias que están dentro de él se pierden con él.

## 9. Arranque real (el primer día)

1. **Borra el historial de prueba.** Corre `Database/2026-09-28_arranque_real.sql` en la base del servidor:
   ```bash
   psql -h localhost -U golocentro_app -d golocentro -f 2026-09-28_arranque_real.sql
   ```
   - Primero muestra cuánto va a borrar. Al final todo debe salir en 0.
   - Mantiene los datos del negocio, las sedes (también "Sede norte"), las zonas, los usuarios, los productos, los clientes y los proveedores.
2. **Entra como dueña → Negocio:**
   - Carga el RUC, la razón social, la dirección y el teléfono reales. Salen en cada nota de venta.
   - Revisa las sedes y sus zonas, y el plano de cada una.
3. **Usuarios:**
   - Revisa las cuentas y desactiva las de prueba.
   - Cada persona debe cambiar su contraseña al entrar (menú de su nombre → Cambiar contraseña).
4. **Productos, clientes y proveedores:**
   - Revisa precios, stock mínimo y vencimientos.
   - Borra lo que haya sido de prueba.
5. **Stock inicial:**
   - Ve zona por zona a **Conteo de inventario → Contar**, con el motivo **"Conteo inicial"**.
   - Agrega cada producto que encuentres, con su cantidad.
   - Al terminar todas las zonas, el stock del sistema es el real y el kardex empieza desde ahí.
6. La primera venta real saldrá como **NV01-000001**.

---

## Actualizar a una versión nueva

Repite el paso 4 y reinicia el servicio:

```bash
sudo systemctl restart golocentro
```

Las fotos (`evidencias`, `uploads`) y las llaves de sesión no se tocan.

Si la versión trae un script nuevo en `Database/`, córrelo **antes** de reiniciar. Primero haz una copia:

```bash
sudo /etc/cron.daily/golocentro-respaldo
```

## Si algo falla

| Qué pasa | Qué revisar |
|---|---|
| La página no carga (502) | `sudo systemctl status golocentro` y `sudo journalctl -u golocentro -n 50` |
| "Algo salió mal" con un código | Busca ese código en el log: `sudo journalctl -u golocentro \| grep CODIGO` |
| Las fotos no se suben | `client_max_body_size` en Nginx y los permisos de `wwwroot/evidencias` (paso 4) |
| Las fechas salen con otra hora | `timedatectl` debe decir `America/Lima`, y luego `sudo systemctl restart golocentro` |
| Todos pierden la sesión al reiniciar | Falta la línea `Golocentro__CarpetaLlaves`, o la carpeta de llaves no es de `www-data` |
| "Demasiados intentos" al entrar | Son más de 10 intentos en un minuto desde el mismo equipo; se libera solo en un minuto |
