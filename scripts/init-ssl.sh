#!/bin/bash
# init-ssl.sh — Generate self-signed cert so nginx can start on port 443,
# then obtain real Let's Encrypt cert via certbot.
#
# Run on VPS: bash scripts/init-ssl.sh

set -euo pipefail

DOMAIN="sendnex.xyz"
EMAIL="elyon4001@gmail.com"
SSL_DIR="./docker/nginx/ssl"
CERT_DIR="${SSL_DIR}/live/${DOMAIN}"

echo "=== SendNex SSL Setup ==="

# Step 1: Create directories
mkdir -p "${CERT_DIR}"
mkdir -p "./docker/nginx/certbot"

# Step 2: Generate self-signed cert (so nginx can start with 443)
if [ ! -f "${CERT_DIR}/fullchain.pem" ]; then
  echo ">> Generating temporary self-signed certificate..."
  openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
    -keyout "${CERT_DIR}/privkey.pem" \
    -out "${CERT_DIR}/fullchain.pem" \
    -subj "/CN=${DOMAIN}" 2>/dev/null
  echo ">> Self-signed cert created."
else
  echo ">> Certificate files already exist, skipping self-signed generation."
fi

# Step 3: Restart nginx so it picks up the cert and starts listening on 443
echo ">> Restarting nginx..."
docker compose restart nginx
sleep 3

# Step 4: Get real certificate from Let's Encrypt
echo ">> Requesting Let's Encrypt certificate..."
docker compose run --rm certbot certonly \
  --webroot \
  -w /var/www/certbot \
  -d "${DOMAIN}" \
  -d "www.${DOMAIN}" \
  --email "${EMAIL}" \
  --agree-tos \
  --non-interactive \
  --force-renewal

# Step 5: Restart nginx to use the real cert
echo ">> Restarting nginx with real certificate..."
docker compose restart nginx

echo "=== SSL setup complete! ==="
echo ">> https://${DOMAIN} should now be live."
echo ">> Certbot auto-renewal is handled by the certbot container."
