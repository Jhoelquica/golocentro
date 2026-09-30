# Funciones que usan todos los scripts. Cada script deja su registro en ~/evidencias
# (con fecha y hora de cada paso) para el informe del despliegue.
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=config.sh
source "$AQUI/config.sh"

USUARIO_REAL="${SUDO_USER:-$USER}"
CASA="$(getent passwd "$USUARIO_REAL" | cut -d: -f6)"
EVIDENCIAS="$CASA/evidencias"
mkdir -p "$EVIDENCIAS"

iniciar_registro() {
    REGISTRO="$EVIDENCIAS/$1.log"
    exec > >(tee -a "$REGISTRO") 2>&1
    echo "=================================================================="
    echo " Golocentro · $1"
    echo " Servidor: $(hostname) · $(date '+%d/%m/%Y %H:%M:%S %Z') · usuario: $USUARIO_REAL"
    echo "=================================================================="
}

paso()  { echo; echo "── [$(date '+%H:%M:%S')] $* ──"; }
ok()    { echo "✔ $*"; }
aviso() { echo "⚠ $*"; }
falla() { echo "✘ $*"; exit 1; }

terminar() {
    chown -R "$USUARIO_REAL": "$EVIDENCIAS" 2>/dev/null || true
    echo
    echo "── [$(date '+%H:%M:%S')] Terminado. Registro: $REGISTRO ──"
}

requiere_root() {
    [ "$(id -u)" -eq 0 ] || { echo "Este paso necesita permisos de administrador: sudo bash $0"; exit 1; }
}

esta_activo() { systemctl is-active --quiet "$1" 2>/dev/null; }
