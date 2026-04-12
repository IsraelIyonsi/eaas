#!/bin/bash
set -euo pipefail

# Two-phase deploy: Phase 1 pulls code, Phase 2 re-execs updated script
if [ "${DEPLOY_PHASE:-1}" = "1" ]; then
  echo "[deploy] Starting deployment at $(date)"
  cd /opt/eaas

  # Pull latest from dev
  echo "[deploy] Pulling latest code..."
  git fetch origin dev
  git reset --hard origin/dev

  # Re-execute the updated script (Phase 2) so migration/config changes take effect
  echo "[deploy] Re-executing updated deploy script..."
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

# Stop and remove application containers (including stale renamed orphans from interrupted deploys)
echo "[deploy] Stopping application containers..."
for svc in eaas-api eaas-worker eaas-webhook-processor eaas-dashboard eaas-nginx; do
  # Remove the primary container
  docker rm -f "$svc" 2>/dev/null || true
  # Remove any orphaned containers with hash-prefixed names (e.g., 4f65fa308e51_eaas-api)
  docker ps -a --filter "name=${svc}" --format '{{.ID}}' | xargs -r docker rm -f 2>/dev/null || true
done

echo "[deploy] Starting services..."
docker compose -f docker-compose.yml up -d api worker webhook-processor dashboard nginx

# Run idempotent database migrations that may not have been applied
# (Docker init scripts only run on first DB creation; these handle schema drift)
# Only include migrations that are safe to re-run (idempotent with IF NOT EXISTS guards)
echo "[deploy] Running database migrations..."
docker exec -i eaas-postgres psql -U eaas_app -d eaas < "/opt/eaas/scripts/migrate_review_gate.sql" 2>&1 \
  | tail -3 || echo "[deploy] Warning: migrate_review_gate.sql had issues"

echo "[deploy] Waiting for health checks..."
sleep 20
docker compose -f docker-compose.yml ps

echo "[deploy] Deployment complete at $(date)"
