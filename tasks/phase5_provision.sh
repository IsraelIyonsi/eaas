#!/bin/bash
set -e
CHARS='ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'
gen_key() {
  local rnd=""
  for i in $(seq 1 40); do
    idx=$(( RANDOM % ${#CHARS} ))
    rnd+="${CHARS:$idx:1}"
  done
  echo "snx_live_${rnd}"
}
CT_KEY=$(gen_key)
EV_KEY=$(gen_key)
CT_HASH=$(printf '%s' "$CT_KEY" | sha256sum | awk '{print $1}')
EV_HASH=$(printf '%s' "$EV_KEY" | sha256sum | awk '{print $1}')
CT_PREFIX=${CT_KEY:0:8}
EV_PREFIX=${EV_KEY:0:8}
STAMP=$(date -u +%Y%m%d)

echo "CASHTRACK_KEY=${CT_KEY}"
echo "EVNTRAA_KEY=${EV_KEY}"

docker exec -i eaas-postgres psql -U eaas_app -d eaas <<SQL
INSERT INTO api_keys (id, tenant_id, name, key_hash, prefix, status, created_at, is_service_key)
VALUES
 (gen_random_uuid(), '9be989d2-f187-44d3-9db6-3db04b5ca0db', 'integration-test-${STAMP}', '${CT_HASH}', '${CT_PREFIX}', 'active', now(), false),
 (gen_random_uuid(), 'c5e45f27-587f-461b-9b81-2eea3d29047c', 'integration-test-${STAMP}', '${EV_HASH}', '${EV_PREFIX}', 'active', now(), false)
RETURNING id, tenant_id, name, prefix, status;
SQL
