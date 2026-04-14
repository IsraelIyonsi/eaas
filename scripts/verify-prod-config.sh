#!/usr/bin/env bash
# -----------------------------------------------------------------------------
# verify-prod-config.sh
#
# CI guard that asserts the production configuration enforces admin proxy-token
# verification. It protects against accidental regressions of the security
# posture shipped in C1 rev-4 (RequireProxyToken=true on prod).
#
# Rules:
#   1. If src/EaaS.Api/appsettings.Production.json exists, it MUST contain
#      "RequireProxyToken": true. Missing or =false -> exit 1.
#   2. src/EaaS.Api/appsettings.json MUST NOT contain
#      "RequireProxyToken": false under Authentication.AdminSession.
#
# Intended usage:
#   - Invoked from .github/workflows/ci.yml before `dotnet test`.
#   - Safe to run locally: `bash scripts/verify-prod-config.sh`.
#
# Exit codes:
#   0 = configuration is safe
#   1 = regression detected (see stderr for the offending file)
# -----------------------------------------------------------------------------
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROD_FILE="${REPO_ROOT}/src/EaaS.Api/appsettings.Production.json"
BASE_FILE="${REPO_ROOT}/src/EaaS.Api/appsettings.json"

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

# Rule 1 — production must opt in to proxy token verification.
if [[ -f "${PROD_FILE}" ]]; then
  if ! grep -Eq '"RequireProxyToken"[[:space:]]*:[[:space:]]*true' "${PROD_FILE}"; then
    fail "${PROD_FILE} is missing \"RequireProxyToken\": true."
  fi
  echo "OK: appsettings.Production.json enforces RequireProxyToken=true."
else
  echo "WARN: ${PROD_FILE} not found (skipping prod-file check)."
fi

# Rule 2 — base config must not downgrade the default.
if [[ -f "${BASE_FILE}" ]]; then
  if grep -Eq '"RequireProxyToken"[[:space:]]*:[[:space:]]*false' "${BASE_FILE}"; then
    fail "${BASE_FILE} contains \"RequireProxyToken\": false — refuse to regress."
  fi
  echo "OK: appsettings.json does not disable RequireProxyToken."
fi

echo "verify-prod-config: all checks passed."
