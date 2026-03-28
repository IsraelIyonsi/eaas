#!/usr/bin/env bash
# =============================================================================
# EaaS Rollback Script
# Usage: rollback.sh <version-tag>
# Example: rollback.sh v1.0.0
# =============================================================================

set -euo pipefail

# --- Configuration ---
APP_DIR="/opt/eaas"
REGISTRY="ghcr.io"
IMAGE_OWNER="israeliyonsi"
IMAGE_PREFIX="${REGISTRY}/${IMAGE_OWNER}/eaas"
HEALTH_URL="http://localhost:5000/health"
HEALTH_RETRIES=6
HEALTH_DELAY=10

# --- Validate Arguments ---
if [ $# -lt 1 ]; then
  echo "Usage: rollback.sh <version-tag>"
  echo "Example: rollback.sh v1.0.0"
  echo ""
  echo "Available images:"
  docker images --format "table {{.Repository}}\t{{.Tag}}\t{{.CreatedAt}}" | grep "eaas" || echo "  No EaaS images found locally."
  exit 1
fi

VERSION="${1}"
TIMESTAMP=$(date -u '+%Y-%m-%d %H:%M:%S UTC')

echo "============================================"
echo " EaaS Rollback to ${VERSION}"
echo " ${TIMESTAMP}"
echo "============================================"

cd "${APP_DIR}"

# --- Load environment ---
if [ -f "${APP_DIR}/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  source "${APP_DIR}/.env"
  set +a
fi

# --- Save current state for potential re-rollback ---
echo ""
echo "[1/5] Saving current container state..."

CURRENT_IMAGES=$(docker compose ps -q 2>/dev/null | xargs -r docker inspect --format='{{.Config.Image}}' 2>/dev/null || echo "none")
echo "  Current images:"
echo "${CURRENT_IMAGES}" | sed 's/^/    /'

# --- Pull target version images ---
echo ""
echo "[2/5] Pulling images for version ${VERSION}..."

SERVICES=("api" "dashboard" "worker" "webhook")
for service in "${SERVICES[@]}"; do
  IMAGE="${IMAGE_PREFIX}-${service}:${VERSION}"
  echo "  Pulling ${IMAGE}..."
  if ! docker pull "${IMAGE}"; then
    echo "  ERROR: Failed to pull ${IMAGE}"
    echo "  Rollback aborted. No changes were made."
    exit 1
  fi
done

echo "  All images pulled successfully."

# --- Stop current containers ---
echo ""
echo "[3/5] Stopping current containers..."

docker compose stop api dashboard worker webhook-processor 2>/dev/null || true

echo "  Containers stopped."

# --- Start with target version ---
echo ""
echo "[4/5] Starting containers with version ${VERSION}..."

export EAAS_VERSION="${VERSION}"
docker compose up -d --force-recreate api dashboard worker webhook-processor

echo "  Containers started. Waiting for startup..."
sleep 10

# --- Verify health ---
echo ""
echo "[5/5] Running health checks..."

HEALTHY=false
for i in $(seq 1 ${HEALTH_RETRIES}); do
  HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${HEALTH_URL}" 2>/dev/null || echo "000")

  if [ "${HTTP_CODE}" = "200" ]; then
    HEALTHY=true
    echo "  Health check PASSED on attempt ${i}/${HEALTH_RETRIES} (HTTP ${HTTP_CODE})"
    break
  fi

  echo "  Health check attempt ${i}/${HEALTH_RETRIES}: HTTP ${HTTP_CODE}"
  sleep ${HEALTH_DELAY}
done

echo ""
echo "============================================"

if [ "${HEALTHY}" = true ]; then
  echo " Rollback to ${VERSION} SUCCESSFUL"
  echo ""
  echo " Running containers:"
  docker compose ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}" 2>/dev/null || docker compose ps
  echo "============================================"
  exit 0
else
  echo " Rollback to ${VERSION} FAILED"
  echo ""
  echo " Health check did not pass after ${HEALTH_RETRIES} attempts."
  echo " Container status:"
  docker compose ps
  echo ""
  echo " Container logs (last 50 lines):"
  docker compose logs --tail=50 api 2>/dev/null || true
  echo ""
  echo " Manual intervention required."
  echo "============================================"
  exit 1
fi
