# Admin Auth (C1) — Rollback Runbook

Purpose: document the exact MTTR path if the C1 signed-proxy-token change breaks
production. Each scenario is a one-file config flip plus a reload verification.
If the reload doesn't take effect, the escape hatch is always a pod redeploy.

Scope: the `AdminSession` scheme in `EaaS.Api` (`AdminSessionAuthHandler.cs`)
and its options bound from `Authentication:AdminSession`.

Primary signals:
- Counter `admin_auth.proxy_token_missing_total` (meter `EaaS.Api.Auth`),
  tagged `outcome="allowed_during_grace"` or `outcome="rejected"`.
- Log message: `AdminSession:RequireProxyToken=false — accepted UNSIGNED
  X-Admin-User-Id=… on <path>. FLIP FLAG TO TRUE IMMEDIATELY.`

---

## Scenario 1 — Signed tokens stop validating (dashboard/API drift)

Symptom: admin UI returns 401 on every request after a dashboard or API deploy,
and the counter shows `outcome="rejected"` climbing.

Action:
1. Edit `src/EaaS.Api/appsettings.Production.json`:
   ```json
   "Authentication": {
     "AdminSession": {
       "RequireProxyToken": false
     }
   }
   ```
2. Save and commit. `IOptionsMonitor<AdminSessionAuthSchemeOptions>` reloads
   the section on file change — no restart required.
3. Verify reload: issue one admin request through the dashboard. Observe
   `admin_auth.proxy_token_missing_total{outcome="allowed_during_grace"}`
   increment by 1 in Prometheus/Grafana.
4. If the counter does NOT move within 30 seconds, the options monitor has
   not picked up the change. Redeploy the API pod
   (`kubectl rollout restart deployment/eaas-api`) and re-verify.

Follow-up: diagnose the drift (rotated `SESSION_SECRET`, dashboard signing
bug, clock skew) before flipping `RequireProxyToken` back to `true`.

---

## Scenario 2 — `EnforceAfter` was tripped early and is rejecting all

Symptom: counter shows `outcome="rejected"` and the legacy unsigned path is
being blocked even though operators intended to stay in the grace window.

Action:
1. Edit `appsettings.Production.json`; set `EnforceAfter` to a far-future
   instant OR null:
   ```json
   "Authentication": {
     "AdminSession": {
       "EnforceAfter": "2099-01-01T00:00:00Z"
     }
   }
   ```
2. Save. `IOptionsMonitor` reloads automatically.
3. Verify reload: issue one admin request without a signed token. Observe
   `admin_auth.proxy_token_missing_total{outcome="allowed_during_grace"}`
   increment. If the counter does not move, redeploy the API pod.

---

## Scenario 3 — Rotate `SESSION_SECRET`

HARD cutover. All admin cookies and all in-flight proxy tokens invalidate at
the moment the new secret is read. There is no dual-secret acceptance path
today (scheduled follow-up).

Recommended window: lowest admin traffic (late night UTC), with a maintenance
banner visible to admins.

Steps:
1. Update `SESSION_SECRET` in the secret store (Kubernetes secret / vault).
   The new value MUST be at least 32 bytes when `RequireProxyToken=true`.
2. Redeploy BOTH pods in this order (or in parallel — either is fine, since
   all tokens invalidate anyway):
   - API: `kubectl rollout restart deployment/eaas-api`
   - Dashboard: `kubectl rollout restart deployment/eaas-dashboard`
3. Confirm both pods loaded the new value:
   - Preferred: `GET /healthz` on each pod if it exposes a config hash.
   - Fallback: grep pod logs for the startup line that echoes the redacted
     options (`AdminSessionAuthSchemeOptions { SessionSecret=***, … }`) and
     confirm both pods started AFTER the secret was updated.
4. Force all admins to re-login (expected — their old cookies are dead).

Known limitation: no dual-secret acceptance path. A rolling key-rotation
mechanism (accept OLD and NEW for a window) is a scheduled follow-up.

---

## Verify-in-staging checklist (run BEFORE shipping this runbook)

Each scenario must be rehearsed in staging with screenshot evidence stored
alongside the change ticket.

- [ ] Scenario 1: flip `RequireProxyToken=false`, capture counter increment
      with `outcome="allowed_during_grace"` in Grafana.
- [ ] Scenario 1: negative path — confirm counter does NOT move when the
      change is saved but the options monitor is disabled; confirm pod
      redeploy recovers.
- [ ] Scenario 2: set `EnforceAfter` to a future instant, capture counter
      increment with `outcome="allowed_during_grace"`.
- [ ] Scenario 2: set `EnforceAfter` to null, confirm same behavior.
- [ ] Scenario 3: rotate `SESSION_SECRET` in staging, confirm all admin
      sessions invalidate simultaneously and re-login works on both pods.
- [ ] Capture log lines for each scenario and attach to the cutover ticket.
