#!/usr/bin/env bash
# Paso 0: mirar el servidor antes de tocar nada (no cambia nada, no necesita sudo)
source "$(dirname "$0")/comun.sh"
iniciar_registro "00-diagnostico"

paso "Sistema operativo"
(lsb_release -ds 2>/dev/null || grep PRETTY_NAME /etc/os-release | cut -d= -f2 | tr -d '"')
echo "Kernel: $(uname -r) · Arquitectura: $(uname -m)"

paso "Procesador, memoria y disco"
echo "Núcleos: $(nproc)"
free -h
swapon --show || true
df -h /

paso "Hora del servidor"
timedatectl 2>/dev/null | grep -Ei "local time|time zone" || date

paso "Programas que ya tiene"
for s in nginx apache2 postgresql; do
    printf "%-12s " "$s"; if esta_activo "$s"; then echo "activo"; else echo "no activo / no instalado"; fi
done
command -v nginx >/dev/null && nginx -v 2>&1 || true
command -v psql >/dev/null && psql --version || echo "psql: no instalado"
command -v dotnet >/dev/null && dotnet --list-runtimes || echo "dotnet: no instalado"
command -v certbot >/dev/null && certbot --version 2>&1 || echo "certbot: no instalado"

paso "Sitios web que ya atiende Nginx"
ls -1 /etc/nginx/sites-enabled 2>/dev/null || echo "(ninguno o sin Nginx)"

paso "Puertos que ya están en uso"
ss -ltn | awk 'NR==1 || /LISTEN/'
if ss -ltn | grep -q ":$PUERTO_APP "; then aviso "El puerto $PUERTO_APP ya está ocupado: hay que elegir otro en config.sh"; else ok "Puerto $PUERTO_APP libre para el sistema"; fi

paso "Firewall"
if esta_activo ufw; then echo "ufw activo (revisar con: sudo ufw status)"; else echo "ufw no activo"; fi

paso "IP pública"
curl -4 -s --max-time 5 https://ifconfig.me || hostname -I
echo

paso "Dominio $DOMINIO"
getent ahostsv4 "$DOMINIO" | head -n1 || aviso "$DOMINIO todavía no apunta a ninguna IP"

terminar
