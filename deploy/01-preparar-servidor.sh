#!/usr/bin/env bash
# Paso 1: instalar lo que el sistema necesita (.NET 8 SDK para compilarlo, PostgreSQL, Nginx) y crear sus carpetas.
# Es un servidor compartido: no actualiza todo el sistema, no cambia la hora ni el firewall,
# y lo que ya está instalado se deja como está.
source "$(dirname "$0")/comun.sh"
requiere_root
iniciar_registro "01-preparar-servidor"
export DEBIAN_FRONTEND=noninteractive

paso "Lista de paquetes"
apt-get update -q

paso ".NET 8 SDK (compila el sistema desde el repositorio; trae el runtime de ASP.NET Core)"
if dotnet --list-sdks 2>/dev/null | grep -q "^8\."; then
    ok "Ya estaba instalado"
else
    if ! apt-cache show dotnet-sdk-8.0 >/dev/null 2>&1; then
        # Debian o Ubuntu sin el paquete: repositorio oficial de Microsoft
        . /etc/os-release
        wget -q "https://packages.microsoft.com/config/$ID/$VERSION_ID/packages-microsoft-prod.deb" -O /tmp/packages-microsoft-prod.deb
        dpkg -i /tmp/packages-microsoft-prod.deb
        rm -f /tmp/packages-microsoft-prod.deb
        apt-get update -q
    fi
    apt-get install -y -q dotnet-sdk-8.0
fi
dotnet --list-sdks
dotnet --list-runtimes

paso "PostgreSQL"
if command -v psql >/dev/null && esta_activo postgresql; then
    ok "Ya estaba instalado y activo: se usará con una base y un usuario propios"
else
    apt-get install -y -q postgresql
    systemctl enable --now postgresql
fi
psql --version

paso "Nginx"
if esta_activo apache2; then
    aviso "Apache está activo en este servidor: Nginx no se instala para no chocar con él en el puerto 80."
    aviso "Avisar antes de seguir: habría que publicar el sistema a través de Apache."
elif command -v nginx >/dev/null; then
    ok "Ya estaba instalado"
    systemctl enable --now nginx
else
    apt-get install -y -q nginx
    systemctl enable --now nginx
fi
nginx -v 2>&1 || true

paso "Herramientas (git, rsync, curl)"
apt-get install -y -q git rsync curl

paso "Carpetas del sistema"
mkdir -p "$DIR_APP" "$DIR_LLAVES" "$DIR_CONFIG" "$DIR_RESPALDOS"
chown www-data:www-data "$DIR_LLAVES"
chmod 700 "$DIR_LLAVES" "$DIR_CONFIG" "$DIR_RESPALDOS"
ls -ld "$DIR_APP" "$DIR_LLAVES" "$DIR_CONFIG" "$DIR_RESPALDOS"

paso "Memoria"
free -h
if [ "$(free -m | awk '/Mem:/{print $2}')" -lt 1500 ] && [ -z "$(swapon --show)" ]; then
    aviso "Menos de 1.5 GB de RAM y sin swap: conviene que el dueño del servidor active 1 GB de swap."
else
    ok "Memoria suficiente"
fi

terminar
