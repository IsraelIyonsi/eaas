# Runbook: Rotating `SESSION_SECRET`

**Audience:** on-call engineer / platform operator.
**Impact:** rotation invalidates EVERY signed artefact derived from the shared secret:

- all admin and tenant dashboard session cookies (`sendnex_session`)
- all in-flight admin proxy tokens (`X-Admin-Proxy-Token`)
- any cached API-side session cookies (`eaas_admin_session`)

Every user — admin and tenant — will be logged out and MUST log in again. There is NO graceful overlap window in the current design.

Rotate when: suspected secret exposure, after personnel off-boarding with secret access, or on the scheduled key-rotation calendar (quarterly).

---

## Preconditions

- You can deploy both the .NET API and the Next.js dashboard in the same maintenance window.
- A replacement 32-byte secret is generated out-of-band:
  ```bash
  node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"
  ```
- Customers have been notified via status page if the rotation is planned.

---

## Rotation Procedure (coordinated restart)

1. **Announce maintenance.** Post to status page: "All admin and dashboard users will be logged out. Re-login required."

2. **Stage the new secret** in the secret store (e.g. Azure Key Vault, AWS Secrets Manager) under a NEW version. Do NOT overwrite the live version yet.

3. **Update dashboard environment.** Point the dashboard container's `SESSION_SECRET` env var at the new secret version. Do NOT restart yet.

4. **Update API environment.** Point the API container's `AdminSession:SessionSecret` configuration at the new secret version. Do NOT restart yet.

5. **Coordinated restart** — both services within the same 30-second window:
   - Restart the .NET API (all instances, rolling OK but complete within the window).
   - Restart the Next.js dashboard (all instances).
   The rolling order does not matter; what matters is that BOTH are pointing at the same new secret before either accepts user traffic again.

6. **Verify.**
   - Confirm `/health` on the API returns 200.
   - Confirm the dashboard home page renders (unauthenticated state).
   - Sign in as a known admin; verify:
     - dashboard cookie is accepted,
     - a signed proxy token round-trips against a read-only admin endpoint,
     - an admin write operation (e.g. toggling a feature flag) succeeds.
   - Check API logs for `Invalid admin session cookie signature` warnings — a burst is expected (stale cookies from before rotation). Confirm the burst subsides within ~10 minutes as users re-login.

7. **Retire the old secret version** in the secret store (mark disabled, do not delete for 30 days in case rollback is needed).

8. **Close maintenance window.** Update status page.

---

## Rollback

If step 6 verification fails:

1. Re-point both services at the OLD secret version.
2. Restart both again in coordinated fashion.
3. File an incident; investigate which component did not pick up the new secret before re-attempting rotation.

---

## Notes

- Domain-separation prefixes (`eaas.cookie.v1\n`, `eaas.proxy.v1\n`) are versioned in code, not configuration. Changing them is a separate exercise and also invalidates all signed artefacts.
- The 60-second proxy-token freshness window means that within ~60s after restart, stale tokens will fail signature verification AND age verification. Either failure produces a 401, which the dashboard surfaces as a re-auth redirect.
- If `AdminSession:RequireProxyToken` is currently `false` (rollout escape hatch), rotating the secret does NOT affect the unsigned-header fallback. Confirm the flag is back to `true` before considering rotation complete.
