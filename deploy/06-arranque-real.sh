#!/usr/bin/env bash
# Paso 6: borrar el historial de prueba y dejar el stock en cero (ver Database/2026-09-28_arranque_real.sql).
# Se mantienen negocio, sedes, zonas, usuarios, productos, clientes y proveedores.
source "$(dirname "$0")/comun.sh"
requiere_root
iniciar_registro "06-arranque-real"
SCRIPT="$AQUI/2026-09-28_arranque_real.sql"

[ -f "$SCRIPT" ] || falla "No está $SCRIPT"

paso "Confirmación"
echo "Se borrarán TODAS las ventas, entradas, traslados, conteos, alertas y el stock de la base $BD."
read -r -p "Escribe BORRAR para continuar: " RESPUESTA </dev/tty
[ "$RESPUESTA" = "BORRAR" ] || falla "Cancelado: no se borró nada"

paso "Arranque real"
cp "$SCRIPT" /tmp/arranque_real.sql && chmod 644 /tmp/arranque_real.sql
sudo -u postgres psql -d "$BD" -v ON_ERROR_STOP=1 -P pager=off -f /tmp/arranque_real.sql
rm -f /tmp/arranque_real.sql
ok "Historial de prueba borrado; la primera venta real será NV01-000001"

paso "Reiniciar el sistema (alertas al día)"
systemctl restart golocentro
sleep 3
systemctl is-active golocentro

terminar
