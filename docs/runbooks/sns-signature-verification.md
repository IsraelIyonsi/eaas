# Runbook: SNS Signature Verification

Owner: Platform / Webhooks
Alerts: `SnsSignatureVerificationDisabled`, `SnsCertFetchFailures`, `SnsSignatureMismatchSpike`, `SnsVerificationDroppedToZero`
Source: `src/EaaS.WebhookProcessor/Handlers/SnsSignatureVerifier.cs`
Metrics: `src/EaaS.WebhookProcessor/Handlers/SnsMetrics.cs`
Alert rules: `infrastructure/prometheus/alerts/sns-webhook.yml`

## Overview

The Webhook Processor verifies every inbound AWS SNS notification against the
`SigningCertURL` (AWS-owned host, allowlisted per region). Verified payloads are then
deduped by `MessageId` in Redis and dispatched to the bounce/complaint/delivery/inbound
handlers. Configuration lives under the `Sns` section and is bound via
`IOptionsMonitor<SnsWebhookOptions>` — **config changes hot-reload; no restart required.**

Key knobs (`SnsWebhookOptions`):

- `SignatureVerificationEnabled` (default `true`) — master kill switch.
- `MaxClockSkew` (default `00:15:00`) — replay window on SNS `Timestamp`.
- `ReplayDedupTtl` (default `01:00:00`) — Redis NX TTL on `MessageId`.
- `CacheTtl` (default `01:00:00`) — signing-cert positive cache TTL.
- `NegativeCacheTtl` (default `00:01:00`) — cert-fetch failure short-circuit TTL.
- `MaxBodyBytes` (default `150_000`) — hard cap; 413 above this.

---

## Kill switch

Alert: `SnsSignatureVerificationDisabled` (critical, pages immediately).

When to use: only when AWS certificate infrastructure is broken (extreme outage, cert
rotation we didn't anticipate) AND downstream idempotence + dedup give us enough
defense-in-depth to keep accepting unverified payloads. Default answer is **don't**.

### Flip OFF (accept unverified)

1. Edit the running config source (env var / Kubernetes ConfigMap / App Configuration):
   `Sns__SignatureVerificationEnabled=false`.
2. Since we use `IOptionsMonitor`, the flip takes effect on the next config-reload cycle
   (seconds, no process restart). There is no rolling restart, no dropped in-flight requests.
3. Within 1 minute the `SnsSignatureVerificationDisabled` alert will page — **this is
   intentional**: an unverified-webhook window must not go unnoticed.

### Flip ON (resume verification — normal state)

1. Set `Sns__SignatureVerificationEnabled=true` in the same config source.
2. Confirm via metrics: `sns_signature_verifications_total{result="disabled"}` stops
   increasing; `{result="success"}` resumes. Alert auto-resolves.

---

## Redis outage

Alert: typically pairs with general Redis health alerts, not a dedicated SNS alert.

During a Redis outage the dedup path **fails open** (warn log +
`sns_dedup_unavailable_total`; request proceeds). Consequences:

- **Signature verification is unaffected** — it does not touch Redis.
- The replay window effectively opens from `ReplayDedupTtl` (1h) down to `MaxClockSkew`
  (15m). An attacker replaying a captured-but-valid SNS payload has at most 15m.
- Downstream handlers are idempotent: SES bounce and complaint handlers dedupe by SES
  `messageId` / `feedback-id` in the DB; delivery events are upserts. A duplicate SNS
  delivery during the outage therefore cannot corrupt state — it's a wasted write.

Action: no SNS-specific action. Restore Redis and watch `sns_dedup_unavailable_total`
return to zero.

---

## Cert fetch failures

Alert: `SnsCertFetchFailures` (high).

Meaning: `sns_cert_fetch_failures_total` is trickling above 0.1/s. Possible causes in
likelihood order:

1. AWS SNS regional outage — check AWS Health Dashboard.
2. Egress DNS / proxy problem — confirm our nodes can resolve and HTTPS GET
   `sns.<region>.amazonaws.com`.
3. A new `SigningCertURL` that isn't valid PEM (rare — would also spike
   `reason="parse_error"`).

Mitigation: the negative cache (60s TTL) prevents us from hammering AWS during the
outage; verification rejects affected messages with `reason=cert_fetch_failed` until
AWS recovers. If sustained and business-critical, the kill switch is an option (see
above) — but only after weighing the unverified-ingest risk.

---

## Signature mismatch

Alert: `SnsSignatureMismatchSpike` (high).

Most likely cause: **cert rotation**. AWS rotates signing certs roughly yearly. Rotation
almost always changes the `SigningCertURL`, which our URL-keyed cache handles naturally:
the new URL is a cache miss, we fetch fresh, and verification succeeds. A same-URL
rotation would transiently fail for up to `CacheTtl` (1h) until the positive cache
entry expires. If you see a sustained same-URL mismatch spike, manually evict by
restarting the pod (cleanest) or lowering `CacheTtl` via hot-reload.

Less likely but must be ruled out: spoofed-payload attack. Cross-check the source IPs
on nginx access logs and the `reason="bad_cert_url"` counter (attacker-supplied URLs
are rejected earlier and tagged there, not here).

---

## Silent failure

Alert: `SnsVerificationDroppedToZero` (critical).

Meaning: `sns_signature_verifications_total` has no samples for 15m — the counter that
should tick on every request has gone quiet. Causes:

1. Webhook Processor crashed or not receiving traffic (check `ServiceDown` alert,
   `up{job="eaas-webhook-processor"}`, nginx upstream health).
2. Prometheus scrape broken (check `/metrics` endpoint directly from the scrape target).
3. Meter export wedged (rare; usually affects all meters simultaneously).

This is a catch-all backstop for scenarios where the other alerts can't fire because
the counters aren't producing samples. If the service is up and receiving traffic,
escalate to the Platform on-call.

---

## Cert rotation cadence

AWS rotates signing certs approximately yearly, usually by issuing a new
`SigningCertURL`. Our cache is URL-keyed, so natural rotation is handled without any
human action — new URL, cache miss, fetch, cache hit thereafter. The 1h positive-cache
TTL bounds the staleness of any same-URL in-place rotation. No scheduled maintenance
is required.

## MessageId dedup TTL

`ReplayDedupTtl` defaults to 1h, which covers the documented AWS SNS retry window. If
AWS later extends retry beyond 1h, extend `ReplayDedupTtl` via config (hot-reloaded).
Current practice is fine at 1h.
