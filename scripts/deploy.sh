#!/bin/bash
set -euo pipefail

# Prevent concurrent deploys — use a lockfile
LOCKFILE="/tmp/eaas-deploy.lock"
if [ "${DEPLOY_PHASE:-1}" = "1" ]; then
  exec 200>"$LOCKFILE"
  if ! flock -n 200; then
    echo "[deploy] Another deploy is already running. Waiting..."
    flock 200
  fi
fi

# Two-phase deploy: Phase 1 pulls code, Phase 2 re-execs updated script
if [ "${DEPLOY_PHASE:-1}" = "1" ]; then
  echo "[deploy] Starting deployment at $(date)"
  cd /opt/eaas

  # Pull latest from prod (production branch)
  echo "[deploy] Pulling latest code..."
  git fetch origin prod
  git reset --hard origin/prod

  # Re-execute the updated script (Phase 2) so migration/config changes take effect
  echo "[deploy] Re-executing updated deploy script..."
  chmod +x /opt/eaas/scripts/deploy.sh
  DEPLOY_PHASE=2 exec /opt/eaas/scripts/deploy.sh
fi

echo "[deploy] Phase 2: Running updated deploy script..."
cd /opt/eaas

# Remove local dev override file — it must never run in production.
# Docker Compose auto-merges docker-compose.override.yml if it exists,
# which overwrites SESSION_SECRET and NODE_ENV with dev values.
rm -f /opt/eaas/docker-compose.override.yml

# Restore .env from secure backup (never tracked in git)
if [ -f /root/.env.backup ]; then
  cp /root/.env.backup /opt/eaas/.env
fi

# Cookie Secure flag is now derived from NODE_ENV in the dashboard
# (see dashboard/src/lib/auth/cookie-flags.ts). The legacy SECURE_COOKIES
# override is removed here so it cannot regress production security.
# nginx terminates TLS and forwards to the Next.js container; NODE_ENV=production
# is set by the Next.js image build, so the browser receives Secure cookies.
sed -i '/^SECURE_COOKIES=/d' /opt/eaas/.env

# Rebuild and restart application containers only (infra stays up)
echo "[deploy] Building Docker images..."
docker compose -f docker-compose.yml build --no-cache api worker webhook-processor

# Stop and remove application containers (including stale renamed orphans from interrupted deploys)
echo "[deploy] Stopping application containers..."
for svc in eaas-api eaas-worker eaas-webhook-processor eaas-nginx; do
  # Remove the primary container
  docker rm -f "$svc" 2>/dev/null || true
  # Remove any orphaned containers with hash-prefixed names (e.g., 4f65fa308e51_eaas-api)
  docker ps -a --filter "name=${svc}" --format '{{.ID}}' | xargs -r docker rm -f 2>/dev/null || true
done

echo "[deploy] Starting services..."
docker compose -f docker-compose.yml up -d --force-recreate api worker webhook-processor nginx

# Run idempotent database migrations that may not have been applied
# (Docker init scripts only run on first DB creation; these handle schema drift)
# Only include migrations that are safe to re-run (idempotent with IF NOT EXISTS guards)
echo "[deploy] Running database migrations..."
docker exec -i eaas-postgres psql -U eaas_app -d eaas < "/opt/eaas/scripts/migrate_review_gate.sql" 2>&1 \
  | tail -3 || echo "[deploy] Warning: migrate_review_gate.sql had issues"
docker exec -i eaas-postgres psql -U eaas_app -d eaas < "/opt/eaas/scripts/migrate_service_keys.sql" 2>&1 \
  | tail -3 || echo "[deploy] Warning: migrate_service_keys.sql had issues"

echo "[deploy] Waiting for health checks..."
sleep 20
docker compose -f docker-compose.yml ps

# Install certbot if not already present
if ! command -v certbot &>/dev/null; then
  echo "[deploy] Installing certbot..."
  apt-get update -qq && apt-get install -y -qq certbot >/dev/null 2>&1 || true
fi

# SSL: Obtain certificate if DNS is ready and cert doesn't exist yet.
# Certbot writes certs to /opt/eaas/docker/nginx/ssl/ which is mounted into the
# nginx container at /etc/letsencrypt/ — so the paths in nginx-ssl.conf resolve correctly.
SSL_DIR="/opt/eaas/docker/nginx/ssl"
CERT_PATH="$SSL_DIR/live/sendnex.xyz/fullchain.pem"

if [ ! -f "$CERT_PATH" ]; then
  echo "[deploy] Checking DNS for sendnex.xyz..."
  RESOLVED_IP=$(dig +short sendnex.xyz A 2>/dev/null | head -1)
  SERVER_IP=$(curl -s ifconfig.me 2>/dev/null || echo "unknown")
  if [ "$RESOLVED_IP" = "$SERVER_IP" ]; then
    echo "[deploy] DNS verified. Obtaining SSL certificate..."
    mkdir -p /var/www/certbot
    certbot certonly --webroot -w /var/www/certbot \
      --config-dir "$SSL_DIR" \
      --work-dir /tmp/certbot-work \
      --logs-dir /tmp/certbot-logs \
      -d sendnex.xyz -d www.sendnex.xyz \
      --non-interactive --agree-tos --email iyonsiisrael@gmail.com \
      && echo "[deploy] SSL certificate obtained! Activating HTTPS..." \
      && cp /opt/eaas/docker/nginx/nginx-ssl.conf /opt/eaas/docker/nginx/nginx.conf \
      && docker exec eaas-nginx nginx -s reload \
      || echo "[deploy] SSL certificate request failed (will retry next deploy)"
  else
    echo "[deploy] DNS not ready yet (resolved=$RESOLVED_IP, expected=$SERVER_IP)"
  fi
else
  echo "[deploy] SSL certificate already exists, renewing if needed..."
  certbot renew --config-dir "$SSL_DIR" --quiet || true
fi

echo "[deploy] Deployment complete at $(date)"
