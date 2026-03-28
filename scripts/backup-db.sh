#!/usr/bin/env bash
# =============================================================================
# EaaS PostgreSQL Backup Script
# Usage: backup-db.sh [daily|weekly]
# =============================================================================

set -euo pipefail

BACKUP_TYPE="${1:-daily}"
APP_DIR="/opt/eaas"
BACKUP_DIR="${APP_DIR}/backups/${BACKUP_TYPE}"
TIMESTAMP=$(date +%Y-%m-%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/eaas_${TIMESTAMP}.sql.gz"
LOG_PREFIX="[$(date -u '+%Y-%m-%d %H:%M:%S UTC')]"

# Load environment variables
if [ -f "${APP_DIR}/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  source "${APP_DIR}/.env"
  set +a
fi

DB_USER="${POSTGRES_USER:-eaas}"
DB_NAME="${POSTGRES_DB:-eaas}"
DB_CONTAINER="eaas-postgres"

# Validate backup type
if [[ "${BACKUP_TYPE}" != "daily" && "${BACKUP_TYPE}" != "weekly" ]]; then
  echo "${LOG_PREFIX} ERROR: Invalid backup type '${BACKUP_TYPE}'. Use 'daily' or 'weekly'."
  exit 1
fi

echo "${LOG_PREFIX} =========================================="
echo "${LOG_PREFIX} Starting ${BACKUP_TYPE} PostgreSQL backup"
echo "${LOG_PREFIX} =========================================="

# Ensure backup directory exists
mkdir -p "${BACKUP_DIR}"

# Verify the database container is running
if ! docker ps --format '{{.Names}}' | grep -q "^${DB_CONTAINER}$"; then
  echo "${LOG_PREFIX} ERROR: Container '${DB_CONTAINER}' is not running."
  exit 1
fi

# Perform the dump
echo "${LOG_PREFIX} Dumping database '${DB_NAME}'..."
if docker exec "${DB_CONTAINER}" pg_dump \
  -U "${DB_USER}" \
  -d "${DB_NAME}" \
  --no-owner \
  --no-privileges \
  --clean \
  --if-exists \
  | gzip > "${BACKUP_FILE}"; then
  BACKUP_SIZE=$(du -sh "${BACKUP_FILE}" | cut -f1)
  echo "${LOG_PREFIX} Backup created: ${BACKUP_FILE} (${BACKUP_SIZE})"
else
  echo "${LOG_PREFIX} ERROR: pg_dump failed."
  rm -f "${BACKUP_FILE}"
  exit 1
fi

# Verify backup integrity (non-empty gzip file)
if ! gzip -t "${BACKUP_FILE}" 2>/dev/null; then
  echo "${LOG_PREFIX} ERROR: Backup file is corrupt."
  rm -f "${BACKUP_FILE}"
  exit 1
fi

echo "${LOG_PREFIX} Backup integrity verified."

# Upload to S3-compatible storage (optional)
# Uncomment and configure if using Hetzner Object Storage, Backblaze B2, or AWS S3
# S3_BUCKET="s3://eaas-backups/${BACKUP_TYPE}/"
# if command -v aws &>/dev/null; then
#   echo "${LOG_PREFIX} Uploading to S3..."
#   aws s3 cp "${BACKUP_FILE}" "${S3_BUCKET}" --quiet
#   echo "${LOG_PREFIX} Uploaded to ${S3_BUCKET}"
# fi

# Rotate old backups
if [ "${BACKUP_TYPE}" = "daily" ]; then
  RETENTION_DAYS=7
else
  RETENTION_DAYS=28
fi

echo "${LOG_PREFIX} Rotating backups older than ${RETENTION_DAYS} days..."

DELETED_COUNT=0
while IFS= read -r -d '' old_backup; do
  rm -f "${old_backup}"
  DELETED_COUNT=$((DELETED_COUNT + 1))
  echo "${LOG_PREFIX}   Deleted: $(basename "${old_backup}")"
done < <(find "${BACKUP_DIR}" -name "eaas_*.sql.gz" -mtime +${RETENTION_DAYS} -print0)

echo "${LOG_PREFIX} Rotated ${DELETED_COUNT} old backup(s)."

# List remaining backups
REMAINING=$(find "${BACKUP_DIR}" -name "eaas_*.sql.gz" | wc -l)
TOTAL_SIZE=$(du -sh "${BACKUP_DIR}" 2>/dev/null | cut -f1)

echo "${LOG_PREFIX} =========================================="
echo "${LOG_PREFIX} Backup Summary"
echo "${LOG_PREFIX}   Type:      ${BACKUP_TYPE}"
echo "${LOG_PREFIX}   File:      $(basename "${BACKUP_FILE}")"
echo "${LOG_PREFIX}   Size:      ${BACKUP_SIZE}"
echo "${LOG_PREFIX}   Remaining: ${REMAINING} backup(s) (${TOTAL_SIZE} total)"
echo "${LOG_PREFIX}   Status:    SUCCESS"
echo "${LOG_PREFIX} =========================================="

exit 0
