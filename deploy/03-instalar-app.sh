#!/usr/bin/env bash
# Paso 3: compilar el sistema desde este repositorio y dejarlo como servicio que arranca solo.
# También sirve para actualizar (git pull y volver a correrlo): las fotos subidas y las llaves de sesión no se tocan.
source "$(dirname "$0")/comun.sh"
requiere_root
iniciar_registro "03-instalar-app"
REPO="$(cd "$AQUI/.." && pwd)"
NUEVO="$(mktemp -d /tmp/golocentro-nuevo-XXXXXX)"
trap 'rm -rf "$NUEVO"' EXIT

[ -f "$DIR_CONFIG/golocentro.env" ] || falla "Falta $DIR_CONFIG/golocentro.env (paso 2)"

paso "Versión que se instala"
git -C "$REPO" log -1 --format='Commit %h · %ad · %s' --date=format:'%d/%m/%Y %H:%M' || true

paso "Compilar (dotnet publish, Release)"
chown "$USUARIO_REAL": "$NUEVO"
sudo -u "$USUARIO_REAL" -H env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1     dotnet publish "$REPO/GestionAlmacen_Golocentro.csproj" -c Release -o "$NUEVO" --nologo
[ -f "$NUEVO/GestionAlmacen_Golocentro.dll" ] || falla "La compilación no generó el sistema"
ok "Compilado"

paso "Copiar la versión nueva"
rsync -a --delete --exclude 'wwwroot/evidencias/' --exclude 'wwwroot/uploads/' "$NUEVO/" "$DIR_APP/"
mkdir -p "$DIR_APP/wwwroot/evidencias" "$DIR_APP/wwwroot/uploads/perfiles"
chown -R root:root "$DIR_APP"
chown -R www-data:www-data "$DIR_APP/wwwroot/evidencias" "$DIR_APP/wwwroot/uploads"
du -sh "$DIR_APP"
ok "Sistema copiado en $DIR_APP"

paso "Servicio golocentro (systemd)"
cat > /etc/systemd/system/golocentro.service <<UNIT
[Unit]
Description=Golocentro - gestión de almacén y ventas
After=network.target postgresql.service

[Service]
WorkingDirectory=$DIR_APP
ExecStart=/usr/bin/dotnet $DIR_APP/GestionAlmacen_Golocentro.dll
Restart=always
RestartSec=10
User=www-data
EnvironmentFile=$DIR_CONFIG/golocentro.env
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:$PUERTO_APP
Environment=Golocentro__CarpetaLlaves=$DIR_LLAVES
Environment=Logging__LogLevel__Default=Warning
Environment=TZ=$ZONA_HORARIA
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1

[Install]
WantedBy=multi-user.target
UNIT
systemctl daemon-reload
systemctl enable golocentro
systemctl restart golocentro
ok "Servicio creado y arrancado"

paso "Comprobar que responde"
codigo="000"
for _ in $(seq 1 30); do
    codigo="$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$PUERTO_APP/Account/Login" || true)"
    [ "$codigo" = "200" ] && break
    sleep 1
done
systemctl status golocentro --no-pager | head -n 12
if [ "$codigo" = "200" ]; then
    ok "El sistema responde (HTTP 200) en el puerto interno $PUERTO_APP"
else
    journalctl -u golocentro -n 40 --no-pager
    falla "El sistema no respondió (HTTP $codigo)"
fi

paso "Memoria que usa el sistema"
ps -o pid,rss,cmd -C dotnet | awk 'NR==1 {print; next} {printf "%s %.0f MB %s\n", $1, $2/1024, $3" "$4}'

terminar
