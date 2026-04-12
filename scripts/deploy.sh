#!/bin/bash
set -euo pipefail

echo "[deploy] Starting deployment at $(date)"
cd /opt/eaas

# Pull latest from dev
echo "[deploy] Pulling latest code..."
git fetch origin dev
git reset --hard origin/dev

# Remove local dev override file — it must never run in production.
# Docker Compose auto-merges docker-compose.override.yml if it exists,
# which overwrites SESSION_SECRET and NODE_ENV with dev values.
rm -f /opt/eaas/docker-compose.override.yml

# Restore .env from secure backup (never tracked in git)
if [ -f /root/.env.backup ]; then
  cp /root/.env.backup /opt/eaas/.env
fi

# Ensure SECURE_COOKIES is always set correctly.
# The site runs behind nginx SSL termination; the dashboard itself is plain HTTP.
# Secure=true on the cookie is not needed — nginx handles HTTPS.
# If the backup is missing this setting, the docker-compose default would incorrectly
# mark cookies as Secure, causing the browser to silently drop them → login loop.
sed -i '/^SECURE_COOKIES=/d' /opt/eaas/.env
echo "SECURE_COOKIES=false" >> /opt/eaas/.env

# Rebuild and restart application containers only (infra stays up)
echo "[deploy] Building Docker images..."
docker compose -f docker-compose.yml build --no-cache api worker webhook-processor dashboard

# Clean up stale containers from interrupted deploys
echo "[deploy] Cleaning up stale containers..."
docker compose -f docker-compose.yml down --remove-orphans api worker webhook-processor dashboard nginx 2>/dev/null || true

echo "[deploy] Restarting services..."
docker compose -f docker-compose.yml up -d --force-recreate api worker webhook-processor dashboard nginx

echo "[deploy] Waiting for health checks..."
sleep 20
docker compose -f docker-compose.yml ps

echo "[deploy] Deployment complete at $(date)"
