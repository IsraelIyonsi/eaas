#!/usr/bin/env bash
# =============================================================================
# Restore-drill: restores a backup into a throwaway postgres container to
# verify the dump is loadable. Does NOT touch production data.
#
# Usage:
#   ./scripts/test-restore.sh /root/backups/eaas-20260414_030000.sql.gz
#
# Exit 0 on success, non-zero on any failure. Intended to be wired into a
# monthly cron entry — if it fails, the backup chain is broken.
# =============================================================================

set -euo pipefail

BACKUP_FILE="${1:-}"
if [ -z "${BACKUP_FILE}" ] || [ ! -f "${BACKUP_FILE}" ]; then
  echo "usage: $0 <path-to-eaas-*.sql.gz>" >&2
  exit 2
fi

CONTAINER_NAME="eaas-restore-test-$$"
PG_IMAGE="${PG_IMAGE:-postgres:17-alpine}"
PG_PASSWORD="restore_test_$(date +%s)"

log() {
  echo "[$(date -u '+%Y-%m-%d %H:%M:%S UTC')] [test-restore] $*"
}

cleanup() {
  log "Cleaning up container ${CONTAINER_NAME}..."
  docker rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

log "Starting throwaway postgres: ${CONTAINER_NAME} (${PG_IMAGE})"
docker run -d --rm \
  --name "${CONTAINER_NAME}" \
  -e POSTGRES_PASSWORD="${PG_PASSWORD}" \
  -e POSTGRES_DB=eaas \
  "${PG_IMAGE}" >/dev/null

# Wait for readiness (up to 60s)
for i in $(seq 1 60); do
  if docker exec "${CONTAINER_NAME}" pg_isready -U postgres -d eaas >/dev/null 2>&1; then
    break
  fi
  if [ "$i" -eq 60 ]; then
    log "ERROR: postgres did not become ready in 60s"
    exit 1
  fi
  sleep 1
done
log "Postgres ready."

log "Restoring ${BACKUP_FILE}..."
if ! gunzip -c "${BACKUP_FILE}" | docker exec -i "${CONTAINER_NAME}" psql -U postgres -d eaas -v ON_ERROR_STOP=1 >/tmp/restore.log 2>&1; then
  log "ERROR: psql restore failed. Last 40 lines:"
  tail -n 40 /tmp/restore.log >&2
  exit 1
fi

# Sanity check: expect the tenants table to exist and be queryable.
TABLE_COUNT=$(docker exec "${CONTAINER_NAME}" psql -U postgres -d eaas -t -A -c \
  "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'")

log "Restored schema contains ${TABLE_COUNT} public tables."
if [ "${TABLE_COUNT}" -lt 5 ]; then
  log "ERROR: restored database looks empty (<5 tables) — backup is suspect"
  exit 1
fi

log "Restore-drill PASSED for ${BACKUP_FILE}"
exit 0
