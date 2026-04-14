#!/usr/bin/env bash
# =============================================================================
# EaaS Postgres backup — runs every 6 hours from cron.
#
# RPO target: 6 hours (one backup per 6h window)
# RTO target: 1 hour  (see scripts/test-restore.sh for the restore drill)
#
# What it does:
#   1. pg_dump the 'eaas' database from the 'eaas-postgres' container
#   2. gzip the dump and write to $BACKUP_DIR with an ISO-8601 timestamp
#   3. Verify the gzip is intact (gzip -t)
#   4. Optionally upload to $BACKUP_S3_BUCKET (requires awscli) with a 7-day
#      lifecycle tag applied at the bucket level by infra-as-code
#   5. Prune local backups older than $RETENTION_DAYS (default 30)
#
# Exit codes:
#   0 — backup created and verified
#   1 — dump, gzip, upload, or prune failed (alert on this)
# =============================================================================

set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/root/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
DB_CONTAINER="${DB_CONTAINER:-eaas-postgres}"
DB_USER="${POSTGRES_USER:-postgres}"
DB_NAME="${POSTGRES_DB:-eaas}"
S3_BUCKET="${BACKUP_S3_BUCKET:-}"

TIMESTAMP="$(date -u +%Y%m%d_%H%M%S)"
BACKUP_FILE="${BACKUP_DIR}/eaas-${TIMESTAMP}.sql.gz"

log() {
  echo "[$(date -u '+%Y-%m-%d %H:%M:%S UTC')] [backup-postgres] $*"
}

fail() {
  log "ERROR: $*"
  exit 1
}

log "Starting backup -> ${BACKUP_FILE}"
mkdir -p "${BACKUP_DIR}"

if ! docker ps --format '{{.Names}}' | grep -q "^${DB_CONTAINER}$"; then
  fail "container '${DB_CONTAINER}' not running"
fi

# pg_dump + gzip as a pipeline; fail if either end fails.
if ! docker exec "${DB_CONTAINER}" pg_dump \
      -U "${DB_USER}" \
      -d "${DB_NAME}" \
      --no-owner \
      --no-privileges \
      --clean \
      --if-exists \
    | gzip -9 > "${BACKUP_FILE}"; then
  rm -f "${BACKUP_FILE}"
  fail "pg_dump | gzip failed"
fi

if ! gzip -t "${BACKUP_FILE}" 2>/dev/null; then
  rm -f "${BACKUP_FILE}"
  fail "backup file failed gzip integrity check"
fi

SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
log "Backup created (${SIZE})"

# Off-host copy
if [ -n "${S3_BUCKET}" ]; then
  if ! command -v aws >/dev/null 2>&1; then
    log "WARN: BACKUP_S3_BUCKET set but aws CLI not installed — skipping upload"
  else
    log "Uploading to s3://${S3_BUCKET}/..."
    if aws s3 cp "${BACKUP_FILE}" "s3://${S3_BUCKET}/$(basename "${BACKUP_FILE}")" --only-show-errors; then
      log "Uploaded to s3://${S3_BUCKET}/$(basename "${BACKUP_FILE}")"
    else
      log "ERROR: S3 upload failed — local backup retained"
      # Don't exit 1: the local copy is still valid. Alerting should monitor
      # S3 success separately.
    fi
  fi
fi

# Prune local backups older than retention
log "Pruning local backups older than ${RETENTION_DAYS} days..."
DELETED=0
while IFS= read -r -d '' old; do
  rm -f "${old}"
  DELETED=$((DELETED + 1))
done < <(find "${BACKUP_DIR}" -maxdepth 1 -type f -name 'eaas-*.sql.gz' -mtime "+${RETENTION_DAYS}" -print0)
log "Pruned ${DELETED} old backup(s)"

REMAINING=$(find "${BACKUP_DIR}" -maxdepth 1 -type f -name 'eaas-*.sql.gz' | wc -l)
log "Backup complete. ${REMAINING} backup(s) retained locally."
exit 0
