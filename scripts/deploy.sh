#!/bin/bash
set -e

echo "[deploy] Starting deployment at $(date)"
cd /opt/eaas

# Pull latest from dev
echo "[deploy] Pulling latest code..."
git fetch origin dev
git reset --hard origin/dev

# Restore .env (never tracked in git)
if [ -f /root/.env.backup ]; then
  cp /root/.env.backup /opt/eaas/.env
fi

# Rebuild and restart containers
echo "[deploy] Building Docker images..."
docker compose build --no-cache api worker webhook-processor dashboard

echo "[deploy] Restarting services..."
docker compose up -d --force-recreate api worker webhook-processor dashboard

echo "[deploy] Deployment complete at $(date)"
docker compose ps
