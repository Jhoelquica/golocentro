#!/usr/bin/env bash
# Paso 4: publicar el sistema en internet con Nginx (sitio aparte, las otras webs no se tocan)
# y ponerle el candado HTTPS gratis con Let's Encrypt.
source "$(dirname "$0")/comun.sh"
requiere_root
iniciar_registro "04-nginx-https"

paso "Sitio de Nginx para $DOMINIO"
cat > /etc/nginx/sites-available/golocentro <<NGINX
server {
    listen 80;
    server_name $DOMINIO;

    client_max_body_size 20m;           # fotos de evidencia (hasta 5 MB cada una)

    gzip on;                            # páginas más livianas para el celular
    gzip_types text/css application/javascript application/json image/svg+xml;

    location / {
        proxy_pass http://127.0.0.1:$PUERTO_APP;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
NGINX
ln -sf /etc/nginx/sites-available/golocentro /etc/nginx/sites-enabled/golocentro
nginx -t
systemctl reload nginx
ok "Sitio activo"
ls -1 /etc/nginx/sites-enabled

paso "Comprobar por Nginx"
curl -s -o /dev/null -w "HTTP %{http_code}\n" -H "Host: $DOMINIO" http://127.0.0.1/Account/Login

paso "¿El dominio apunta a este servidor?"
IP_SERVIDOR="$(curl -4 -s --max-time 5 https://ifconfig.me || true)"
IP_DOMINIO="$(getent ahostsv4 "$DOMINIO" | awk 'NR==1 {print $1}')"
echo "IP del servidor: ${IP_SERVIDOR:-?} · $DOMINIO apunta a: ${IP_DOMINIO:-nada}"
if [ -z "$IP_DOMINIO" ] || [ "$IP_DOMINIO" != "$IP_SERVIDOR" ]; then
    aviso "El dominio todavía no apunta aquí: el HTTPS se hace cuando el registro DNS esté listo (volver a correr este paso)."
    terminar
    exit 0
fi
ok "El dominio apunta a este servidor"

paso "Certificado HTTPS (Let's Encrypt)"
command -v certbot >/dev/null || apt-get install -y -q certbot python3-certbot-nginx
echo "Certbot pedirá un correo (para avisos de vencimiento) y aceptar sus condiciones."
certbot --nginx -d "$DOMINIO" --redirect </dev/tty
certbot certificates 2>/dev/null | grep -A4 "$DOMINIO" || true

paso "Comprobar por HTTPS"
curl -s -o /dev/null -w "HTTP %{http_code} por https\n" "https://$DOMINIO/Account/Login"
curl -s -o /dev/null -w "HTTP %{http_code} por http (debe redirigir a https)\n" "http://$DOMINIO/Account/Login"

terminar
