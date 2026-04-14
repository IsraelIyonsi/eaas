#!/usr/bin/env bash
# =============================================================================
# Let's Encrypt deploy hook — reloads the nginx container after a successful
# certificate renewal so the new cert is served without downtime.
#
# Installed at /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh by
# scripts/provision-ssl-hooks.sh. Certbot invokes every script in the
# deploy-hooks directory once per successful renewal.
#
# Idempotent and safe to run multiple times. Exits 0 on success, non-zero
# (but does not abort certbot) on failure — we want certbot to keep going
# even if nginx is briefly unavailable.
# =============================================================================

set -u
LOG_TAG="[letsencrypt-deploy-hook]"
NGINX_CONTAINER="${NGINX_CONTAINER:-eaas-nginx}"

timestamp() {
  date -u '+%Y-%m-%d %H:%M:%S UTC'
}

log() {
  echo "$(timestamp) ${LOG_TAG} $*"
}

log "Renewal hook triggered for: ${RENEWED_LINEAGE:-<unknown>} (domains: ${RENEWED_DOMAINS:-<unknown>})"

if ! command -v docker >/dev/null 2>&1; then
  log "ERROR: docker CLI not found — cannot reload nginx"
  exit 1
fi

if ! docker ps --format '{{.Names}}' | grep -q "^${NGINX_CONTAINER}$"; then
  log "WARN: container '${NGINX_CONTAINER}' is not running — skipping reload"
  exit 0
fi

# Test config before reloading to avoid taking nginx down on a bad cert path
if docker exec "${NGINX_CONTAINER}" nginx -t >/dev/null 2>&1; then
  if docker exec "${NGINX_CONTAINER}" nginx -s reload; then
    log "nginx reloaded successfully"
    exit 0
  else
    log "ERROR: nginx -s reload failed"
    exit 1
  fi
else
  log "ERROR: nginx -t failed — NOT reloading. New cert NOT activated."
  exit 1
fi
