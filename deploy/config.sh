# Datos del despliegue. Es lo único que se cambia de un servidor a otro.
DOMINIO="sistema.golocentro.com"      # dirección con la que se entra al sistema
PUERTO_APP=5080                       # puerto interno del sistema (solo lo ve Nginx)
BD="golocentro"                       # base de datos
USUARIO_BD="golocentro_app"           # usuario de la base de datos (su contraseña la escribe la persona al instalar)
DIR_APP="/var/www/golocentro"         # carpeta del sistema
DIR_LLAVES="/var/lib/golocentro/llaves"
DIR_CONFIG="/etc/golocentro"
DIR_RESPALDOS="/var/backups/golocentro"
ZONA_HORARIA="America/Lima"           # solo para el sistema: la hora del servidor no se toca
