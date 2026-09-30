#!/usr/bin/env bash
# Paso 2: crear la base de datos del sistema, cargar la copia que viene de la PC (/tmp/golocentro.sql)
# y guardar la conexión en /etc/golocentro/golocentro.env (solo la lee el administrador).
# La contraseña la escribe la persona: no se muestra, no queda en el registro ni en el historial.
source "$(dirname "$0")/comun.sh"
requiere_root
iniciar_registro "02-base-de-datos"
COPIA="/tmp/golocentro.sql"

[ -f "$COPIA" ] || falla "No está $COPIA (la copia de la base que se sube desde la PC)"

paso "Comprobar que no exista ya una base $BD"
if [ "$(sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='$BD'")" = "1" ]; then
    falla "Ya existe una base llamada $BD en este servidor: no se toca. Revisar antes de seguir."
fi
ok "No existe: se crea nueva"

paso "Contraseña del usuario $USUARIO_BD"
echo "Escribe una contraseña nueva para la base de datos (mínimo 12 caracteres: letras, números y . _ - @ # %)."
echo "No se ve al escribir y no se guarda en este registro."
while true; do
    read -rs -p "Contraseña: " CLAVE </dev/tty; echo
    read -rs -p "Repítela:   " CLAVE2 </dev/tty; echo
    if [ "$CLAVE" != "$CLAVE2" ]; then echo "No coinciden, otra vez."; continue; fi
    if [[ ! "$CLAVE" =~ ^[A-Za-z0-9._@#%-]{12,}$ ]]; then echo "Mínimo 12 caracteres, solo letras, números y . _ - @ # %"; continue; fi
    break
done
unset CLAVE2
ok "Contraseña aceptada"

paso "Crear usuario y base de datos"
sudo -u postgres psql -v ON_ERROR_STOP=1 -q <<SQL
DO \$\$ BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$USUARIO_BD') THEN
        CREATE ROLE $USUARIO_BD LOGIN;
    END IF;
END \$\$;
ALTER ROLE $USUARIO_BD WITH LOGIN PASSWORD '$CLAVE';
CREATE DATABASE $BD OWNER $USUARIO_BD ENCODING 'UTF8' TEMPLATE template0;
SQL
ok "Usuario $USUARIO_BD y base $BD creados"

paso "Cargar la copia de la base (tablas y datos)"
PGPASSWORD="$CLAVE" psql -h localhost -U "$USUARIO_BD" -d "$BD" -v ON_ERROR_STOP=1 -q -f "$COPIA" >/dev/null
ok "Copia cargada"

paso "Verificación"
PGPASSWORD="$CLAVE" psql -h localhost -U "$USUARIO_BD" -d "$BD" -P pager=off <<'SQL'
SELECT count(*) AS tablas FROM information_schema.tables WHERE table_schema = 'public';
SELECT 'Sedes' AS que, count(*) AS cantidad FROM sede
UNION ALL SELECT 'Zonas', count(*) FROM ubicacion
UNION ALL SELECT 'Usuarios', count(*) FROM usuario
UNION ALL SELECT 'Productos', count(*) FROM producto
UNION ALL SELECT 'Clientes', count(*) FROM cliente
UNION ALL SELECT 'Proveedores', count(*) FROM proveedor;
SQL

paso "Guardar la conexión para el sistema"
umask 077
cat > "$DIR_CONFIG/golocentro.env" <<ENV
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=$BD;Username=$USUARIO_BD;Password=$CLAVE;Timezone=$ZONA_HORARIA
ENV
chmod 600 "$DIR_CONFIG/golocentro.env"
unset CLAVE
ls -l "$DIR_CONFIG/golocentro.env"
ok "Conexión guardada (solo la puede leer el administrador)"

paso "Borrar la copia subida"
rm -f "$COPIA"
ok "Copia eliminada de /tmp"

terminar
