# Despliegue de Golocentro en VPS (Hostinger, Ubuntu 20.04, PostgreSQL)

Arquitectura: **Nginx (80/443, HTTPS)** → **app ASP.NET Core 8 en 127.0.0.1:5050 (systemd)** → **PostgreSQL en 127.0.0.1:5432**.

La app se publica *self-contained* (lleva .NET dentro), así que en el servidor **no hace falta instalar .NET**.

| Archivo de este repo | Destino en el servidor |
|---|---|
| `deploy/golocentro.service` | `/etc/systemd/system/golocentro.service` |
| `deploy/nginx-golocentro.conf` | `/etc/nginx/sites-available/golocentro` |
| `appsettings.Example.json` (viene en la publicación) | `/var/www/golocentro/appsettings.Production.json` (con datos reales) |

En los comandos, reemplaza `usuario`, `IP_DEL_VPS`, `tudominio.com` y el `17` de PostgreSQL por los valores reales.

---

## 0. Antes de empezar (con tu amigo)

1. **Snapshot**: en hPanel → VPS → *Snapshots y copias de seguridad*, crear un snapshot. Si algo sale mal, se vuelve atrás.
2. **¿Qué está usando los puertos?**
   ```bash
   sudo ss -tlnp | grep -E ':(80|443|5432|5050)\b'
   ```
   - Si ya hay algo en **80/443** (Apache, un panel como CloudPanel/CyberPanel, Docker/Traefik), **detente**: el paso 4 se adapta a lo que ya existe.
   - Si ya hay algo en **5432**, tu amigo ya tiene PostgreSQL. Mira su versión con `sudo -u postgres psql -c 'SELECT version();'`. Si es igual o mayor que la de tu PC, sáltate el paso 2.1 y usa esa.
3. **RAM**: PostgreSQL es liviano (unos 100–300 MB para este sistema). Con un 24% de uso no hay problema.
4. **Ubuntu 20.04** ya no recibe actualizaciones de seguridad gratuitas (fin de soporte estándar en 2025). Funciona para este despliegue, pero conviene que tu amigo planee subir a 22.04/24.04 más adelante (con un snapshot antes).

---

## 1. En tu PC (Windows)

### 1.1 Versión de PostgreSQL de tu PC
En pgAdmin, sobre tu base: `SELECT version();`. Anota el número principal (por ejemplo **17**). El servidor debe tener **la misma versión o una mayor**, o el respaldo no se podrá restaurar.

### 1.2 Respaldo de la base (esquema + datos reales)
**pgAdmin**: clic derecho en la base → *Backup…*
- *Format*: **Custom**. *Filename*: `golocentro.dump`.
- En la pestaña de opciones, en *Do not save*, marca **Owner** y **Privileges**.

O por consola (PowerShell; ajusta la versión y el nombre de tu base):
```powershell
& "C:\Program Files\PostgreSQL\17\bin\pg_dump.exe" -U postgres -h localhost -Fc --no-owner --no-privileges -f golocentro.dump golocentro
```

### 1.3 Publicar la app
En PowerShell, dentro de la carpeta del proyecto:
```powershell
dotnet publish -c Release -r linux-x64 --self-contained true -o publish
```
Verifica que exista `publish\wwwroot\lib\bootstrap` (Bootstrap/jQuery no están en Git; salen de tu PC). Luego:
```powershell
tar -czf golocentro.tar.gz -C publish .
scp golocentro.tar.gz golocentro.dump usuario@IP_DEL_VPS:/tmp/
scp -r deploy usuario@IP_DEL_VPS:/tmp/
```

---

## 2. PostgreSQL en el VPS

### 2.1 Instalar (la misma versión principal que tu PC)
```bash
sudo apt-get install -y curl ca-certificates gnupg
curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | sudo gpg --dearmor -o /usr/share/keyrings/postgresql.gpg

# Ubuntu 20.04 ya salió de soporte: si el repositorio principal ya no la tiene, se usa el archivo histórico
REPO=https://apt.postgresql.org/pub/repos/apt
curl -fsI $REPO/dists/focal-pgdg/Release >/dev/null || REPO=https://apt-archive.postgresql.org/pub/repos/apt
echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] $REPO focal-pgdg main" | sudo tee /etc/apt/sources.list.d/pgdg.list

sudo apt-get update
sudo apt-get install -y postgresql-17     # el mismo número que en tu PC
pg_lsclusters                             # muestra la versión y el PUERTO (normalmente 5432)
```
- Si `apt` no encuentra tu versión (por ejemplo, la 18 no se publicó para Ubuntu 20.04), no sigas: se resuelve con Docker o actualizando Ubuntu.
- Si tu amigo ya tenía otro PostgreSQL, el nuevo queda en el puerto **5433**. En ese caso agrega `-p 5433` a los comandos `psql`, `createdb` y `pg_restore`, y pon `Port=5433` en la configuración de la app.
- Por defecto en Ubuntu, PostgreSQL **solo escucha en local** (127.0.0.1). No abras el puerto 5432 en el firewall.

### 2.2 Crear el usuario de la app, la base y restaurar el respaldo
```bash
sudo -u postgres createuser --pwprompt golocentro_app      # pide la clave del usuario de la app
sudo -u postgres createdb -O golocentro_app golocentro
# Hora de Perú para los now() de la base (el VPS suele estar en UTC)
sudo -u postgres psql -c "ALTER DATABASE golocentro SET timezone TO 'America/Lima';"
sudo -u postgres pg_restore --no-owner --role=golocentro_app -d golocentro /tmp/golocentro.dump
```
Si aparecen avisos sobre el esquema `public`, se pueden ignorar. Para comprobar que llegaron los datos:
```bash
sudo -u postgres psql -d golocentro -c "SELECT count(*) FROM usuario;"
```

---

## 3. Instalar la app

```bash
sudo apt-get install -y nginx rsync fontconfig fonts-dejavu-core   # las fuentes son para exportar a Excel
sudo mkdir -p /var/www/golocentro /var/www/golocentro-keys

rm -rf /tmp/golocentro-new && mkdir /tmp/golocentro-new
tar -xzf /tmp/golocentro.tar.gz -C /tmp/golocentro-new
sudo rsync -a --chown=root:root \
  --exclude 'wwwroot/uploads/' --exclude 'wwwroot/evidencias/' --exclude 'appsettings.Production.json' \
  /tmp/golocentro-new/ /var/www/golocentro/
sudo chmod +x /var/www/golocentro/GestionAlmacen_Golocentro

# Carpetas donde la app guarda fotos y evidencias (el usuario www-data escribe ahí)
sudo mkdir -p /var/www/golocentro/wwwroot/uploads/perfiles /var/www/golocentro/wwwroot/evidencias
sudo chown -R www-data:www-data /var/www/golocentro/wwwroot/uploads /var/www/golocentro/wwwroot/evidencias /var/www/golocentro-keys
```

Configuración de producción (pon la clave real de `golocentro_app`):
```bash
sudo cp /var/www/golocentro/appsettings.Example.json /var/www/golocentro/appsettings.Production.json
sudo nano /var/www/golocentro/appsettings.Production.json
sudo chown root:www-data /var/www/golocentro/appsettings.Production.json
sudo chmod 640 /var/www/golocentro/appsettings.Production.json
```

Servicio:
```bash
sudo cp /tmp/deploy/golocentro.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now golocentro
sudo systemctl status golocentro
curl -I http://127.0.0.1:5050/   # debe responder 302 (redirige al login)
# Si luego el login da error, es la conexión a la BD: sudo journalctl -u golocentro -n 100
```

---

## 4. Nginx

```bash
sudo cp /tmp/deploy/nginx-golocentro.conf /etc/nginx/sites-available/golocentro
sudo nano /etc/nginx/sites-available/golocentro      # cambiar tudominio.com
sudo ln -s /etc/nginx/sites-available/golocentro /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

Firewall: si tu amigo usa el **Firewall de hPanel**, debe permitir 80 y 443. Si usa `ufw` (`sudo ufw status`), ejecuta `sudo ufw allow 'Nginx Full'`. No actives `ufw` sin antes permitir `OpenSSH`, o se pierde el acceso.

---

## 5. Dominio y HTTPS

1. En el panel donde compraste el dominio → DNS, crea:
   - Registro **A**, nombre `@`, valor `IP_DEL_VPS`
   - Registro **A**, nombre `www`, valor `IP_DEL_VPS`
2. Espera a que propague (de minutos a unas horas). Comprueba con `ping tudominio.com`.
3. Certificado gratis de Let's Encrypt (se renueva solo):
   ```bash
   sudo snap install --classic certbot
   sudo ln -s /snap/bin/certbot /usr/bin/certbot
   sudo certbot --nginx -d tudominio.com -d www.tudominio.com
   ```

---

## 6. Actualizar a una nueva versión

En tu PC: repetir **1.3** (publish, tar y scp del `.tar.gz`). En el VPS:
```bash
rm -rf /tmp/golocentro-new && mkdir /tmp/golocentro-new
tar -xzf /tmp/golocentro.tar.gz -C /tmp/golocentro-new
sudo rsync -a --chown=root:root \
  --exclude 'wwwroot/uploads/' --exclude 'wwwroot/evidencias/' --exclude 'appsettings.Production.json' \
  /tmp/golocentro-new/ /var/www/golocentro/
sudo chmod +x /var/www/golocentro/GestionAlmacen_Golocentro
sudo systemctl restart golocentro
```
Los `--exclude` evitan borrar las fotos/evidencias subidas y la configuración de producción.

**Si la versión nueva trae un script en `Database/`** (por ejemplo `2026-09-28_zonas_por_sede.sql`), súbelo y ejecútalo **con el usuario de la app** (así las tablas nuevas quedan a su nombre):
```bash
psql -h localhost -U golocentro_app -d golocentro -f /tmp/NOMBRE_DEL_SCRIPT.sql
```

---

## 7. Copias de seguridad

Hay que respaldar **dos cosas**: la base y las carpetas `wwwroot/uploads` + `wwwroot/evidencias`.
```bash
sudo -u postgres pg_dump -Fc -f /var/lib/postgresql/golocentro-$(date +%F).dump golocentro
sudo tar -czf /root/golocentro-archivos-$(date +%F).tar.gz -C /var/www/golocentro/wwwroot uploads evidencias
```
Descarga esos archivos a tu PC de vez en cuando (`scp`). Si quieres que sea automático, se programa con `cron`.

---

## 8. Problemas comunes

| Síntoma | Causa probable |
|---|---|
| **502 Bad Gateway** | La app no está corriendo: `sudo journalctl -u golocentro -n 100` |
| **413 Request Entity Too Large** al subir fotos | Falta `client_max_body_size` en Nginx |
| Sitio sin estilos | Faltó `wwwroot/lib` en la publicación (paso 1.3) |
| Error de conexión a la BD | Revisar `appsettings.Production.json` (base, usuario, clave y puerto: `pg_lsclusters`) |
| `pg_restore: unsupported version` | El PostgreSQL del servidor es más antiguo que el de tu PC (paso 1.1) |
| Fechas u horas corridas 5 horas | Falta `TZ=America/Lima` en el servicio o el `ALTER DATABASE ... timezone` del paso 2.2 |
| Se cierran las sesiones al reiniciar | Falta `DataProtection:KeysPath` o permisos de `/var/www/golocentro-keys` |
| Error al guardar fotos/evidencias | Permisos: `sudo chown -R www-data:www-data /var/www/golocentro/wwwroot/uploads /var/www/golocentro/wwwroot/evidencias` |
