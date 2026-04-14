#!/usr/bin/env bash
# =============================================================================
# One-time provisioning script: installs the Let's Encrypt deploy hook on the
# production VPS so nginx is reloaded automatically after cert renewal.
#
# USAGE (run locally from repo root):
#   VPS_HOST=root@178.104.141.21 ./scripts/provision-ssl-hooks.sh
#
# Safe to re-run — overwrites the hook in place and leaves everything else
# alone. Requires ssh key access to the VPS.
# =============================================================================

set -euo pipefail

VPS_HOST="${VPS_HOST:-root@178.104.141.21}"
HOOK_SRC="$(cd "$(dirname "$0")/.." && pwd)/infrastructure/letsencrypt/reload-nginx.sh"
HOOK_DEST="/etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh"

if [ ! -f "${HOOK_SRC}" ]; then
  echo "ERROR: hook source not found: ${HOOK_SRC}" >&2
  exit 1
fi

echo "[provision-ssl-hooks] Copying ${HOOK_SRC} -> ${VPS_HOST}:${HOOK_DEST}"

ssh "${VPS_HOST}" 'mkdir -p /etc/letsencrypt/renewal-hooks/deploy'
scp "${HOOK_SRC}" "${VPS_HOST}:${HOOK_DEST}"
ssh "${VPS_HOST}" "chmod 755 '${HOOK_DEST}' && chown root:root '${HOOK_DEST}'"

echo "[provision-ssl-hooks] Verifying hook is executable..."
ssh "${VPS_HOST}" "ls -l '${HOOK_DEST}'"

echo "[provision-ssl-hooks] Done. Test with: ssh ${VPS_HOST} 'certbot renew --dry-run'"
