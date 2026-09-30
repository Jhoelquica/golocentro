#!/usr/bin/env bash
# Se corre en la PC (Git Bash). Publica el sistema y lo sube al servidor junto con los scripts.
#   bash deploy/subir-desde-pc.sh golocentro@IP_DEL_SERVIDOR [puerto_ssh] [--con-base]
# --con-base sube también la copia de la base (~/golocentro.sql, hecha con pg_dump en formato texto);
# antes se le quitan las líneas que solo entiende PostgreSQL 17/18, por si el servidor tiene una versión anterior.
set -euo pipefail
cd "$(dirname "$0")/.."

DESTINO="${1:?Falta el destino: golocentro@IP}"
PUERTO="${2:-22}"
LLAVE="$HOME/.ssh/golocentro_servidor"
SSH=(ssh -i "$LLAVE" -p "$PUERTO" -o StrictHostKeyChecking=accept-new "$DESTINO")
SCP=(scp -i "$LLAVE" -P "$PUERTO" -o StrictHostKeyChecking=accept-new -q)

echo "── Publicando (Release) ──"
rm -rf publish
dotnet publish GestionAlmacen_Golocentro.csproj -c Release -o publish --nologo -v q
du -sh publish

echo "── Subiendo el sistema ──"
"${SSH[@]}" "rm -rf /tmp/golocentro-nuevo ~/golocentro-deploy && mkdir -p ~/golocentro-deploy"
"${SCP[@]}" -r publish "$DESTINO:/tmp/golocentro-nuevo"

echo "── Subiendo los scripts ──"
"${SCP[@]}" deploy/*.sh Database/2026-09-28_arranque_real.sql "$DESTINO:golocentro-deploy/"

if [ "${3:-}" = "--con-base" ]; then
    echo "── Subiendo la copia de la base ──"
    COPIA="$HOME/golocentro.sql"
    [ -f "$COPIA" ] || { echo "No está $COPIA"; exit 1; }
    LIMPIA="$(mktemp)"
    grep -vE '^(\\restrict|\\unrestrict|SET transaction_timeout|COMMENT ON SCHEMA public)' "$COPIA" > "$LIMPIA"
    "${SCP[@]}" "$LIMPIA" "$DESTINO:/tmp/golocentro.sql"
    rm -f "$LIMPIA"
fi

"${SSH[@]}" "chmod +x ~/golocentro-deploy/*.sh && ls -1 ~/golocentro-deploy"
echo "✔ Listo. En el servidor: cd ~/golocentro-deploy"
