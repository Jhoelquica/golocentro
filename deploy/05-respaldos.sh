#!/usr/bin/env bash
# Paso 5: copia de seguridad diaria de la base de datos y de las fotos (se guardan 14 días)
source "$(dirname "$0")/comun.sh"
requiere_root
iniciar_registro "05-respaldos"

paso "Tarea diaria de respaldo"
cat > /etc/cron.daily/golocentro-respaldo <<CRON
#!/bin/sh
# Golocentro: base de datos y fotos, cada día; se guardan las de los últimos 14 días
set -e
D=$DIR_RESPALDOS
mkdir -p "\$D"
sudo -u postgres pg_dump -Fc $BD > "\$D/bd-\$(date +%F).dump"
tar -czf "\$D/fotos-\$(date +%F).tar.gz" -C $DIR_APP/wwwroot evidencias uploads
find "\$D" -type f -mtime +14 -delete
CRON
chmod 755 /etc/cron.daily/golocentro-respaldo
ok "Tarea creada: /etc/cron.daily/golocentro-respaldo"

paso "Primer respaldo (prueba)"
/etc/cron.daily/golocentro-respaldo
ls -lh "$DIR_RESPALDOS"
ok "Respaldo funcionando"

terminar
