#!/usr/bin/env bash
# Paso 7: comprobar que todo quedó funcionando (no cambia nada, no necesita sudo)
source "$(dirname "$0")/comun.sh"
iniciar_registro "07-verificacion"

paso "Servicios"
for s in golocentro nginx postgresql; do printf "%-12s %s\n" "$s" "$(systemctl is-active "$s" 2>/dev/null || true)"; done
systemctl show golocentro -p ActiveEnterTimestamp -p NRestarts

paso "Respuesta del sistema por internet"
curl -s -o /dev/null -w "https://$DOMINIO → HTTP %{http_code} en %{time_total} s\n" "https://$DOMINIO/Account/Login" || aviso "Sin HTTPS todavía"
curl -s -o /dev/null -w "http://$DOMINIO  → HTTP %{http_code} (redirige a %{redirect_url})\n" "http://$DOMINIO/Account/Login" || true

paso "Cabeceras de seguridad"
curl -sI "https://$DOMINIO/Account/Login" | grep -Ei "^(HTTP|strict-transport|x-content-type|x-frame|referrer-policy)" || true

paso "Fotos protegidas (sin sesión deben mandar al login)"
curl -s -o /dev/null -w "HTTP %{http_code} → %{redirect_url}\n" "https://$DOMINIO/evidencias/prueba.jpg" || true

paso "Certificado HTTPS"
echo | openssl s_client -servername "$DOMINIO" -connect "$DOMINIO:443" 2>/dev/null | openssl x509 -noout -issuer -dates 2>/dev/null || aviso "Sin certificado todavía"

paso "Recursos del servidor"
uptime
free -h
ps -o pid,rss,etime,cmd -C dotnet | awk 'NR==1 {print; next} {printf "%s %.0f MB %s %s\n", $1, $2/1024, $3, $4" "$5}'
df -h /

paso "Respaldos"
ls -lh "$DIR_RESPALDOS" 2>/dev/null || aviso "Sin permiso para ver $DIR_RESPALDOS (normal sin sudo)"

terminar
