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

# Rebuild and restart application containers only (infra stays up)
echo "[deploy] Building Docker images..."
docker compose -f docker-compose.yml build --no-cache api worker webhook-processor dashboard

echo "[deploy] Restarting services..."
docker compose -f docker-compose.yml up -d --force-recreate api worker webhook-processor dashboard

echo "[deploy] Waiting for health checks..."
sleep 20
docker compose -f docker-compose.yml ps

echo "[deploy] Deployment complete at $(date)"
