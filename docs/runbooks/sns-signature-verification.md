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

## Endpoint URLs

Two distinct endpoints are registered on the Webhook Processor. They are served by
the `webhook-processor-1` / `webhook-processor-2` upstream pool (`:8081` internally)
and fronted by nginx on the public host.

- Public host: `https://email.israeliyonsi.dev`
  (from `infrastructure/nginx/conf.d/api.conf`, `server_name`)
- Internal upstream: `webhook_backend` → `webhook-processor-{1,2}:8081`
  (from `infrastructure/nginx/nginx.conf`)

Routes (registered in `src/EaaS.WebhookProcessor/Program.cs`):

| Purpose                                         | Full URL                                                   | Verb | Handler               |
|-------------------------------------------------|------------------------------------------------------------|------|-----------------------|
| SES bounce / complaint / delivery notifications | `https://email.israeliyonsi.dev/webhooks/sns`              | POST | `SnsMessageHandler`   |
| SES inbound email receipt notifications         | `https://email.israeliyonsi.dev/webhooks/sns/inbound`      | POST | `SnsInboundHandler`   |

Nginx routes both via the `location /webhooks/sns { ... }` prefix match (burst
rate-limit `webhook_limit`, burst=20, 16k proxy buffers for SubscriptionConfirmation
payloads). The paths are literal and static — no environment variable controls the
route registration; they are always mounted at these paths.

### AWS SNS subscription setup

When creating or re-pointing an SNS subscription in the AWS console (or via
`aws sns subscribe`):

- **Protocol:** `HTTPS`
- **Endpoint (outbound notifications topic):** `https://email.israeliyonsi.dev/webhooks/sns`
- **Endpoint (inbound email topic):** `https://email.israeliyonsi.dev/webhooks/sns/inbound`
- **Verb:** AWS always POSTs.
- **Content-Type:** AWS sends `text/plain; charset=UTF-8` (per AWS SNS spec — the
  body is JSON but the header is `text/plain`). Do not require `application/json`
  on the nginx side; the handler deserializes from the raw body.
- **Raw message delivery:** leave **disabled**. The handlers expect the AWS SNS
  envelope (Type/MessageId/SigningCertURL/Signature/...). Raw delivery strips the
  envelope and would fail signature verification on the first request.
- **Subscription confirmation:** after `aws sns subscribe`, AWS sends a
  `SubscriptionConfirmation` message; `SnsMessageHandler.HandleSubscriptionConfirmation`
  auto-confirms by HTTP GET to `SubscribeURL` (SSRF-guarded + anchored host
  allowlist — see `IsValidSubscribeUrl` in `SnsValidation.cs`). No manual confirm.

---

## Signature verification flow

Implemented in `SnsSignatureVerifier.VerifyAsync`. Every field check that fails
records a `sns_signature_rejections_total{reason=...}` counter and returns `false`;
the handler then responds `403` to the caller.

1. **`SigningCertURL` allowlist** (`SnsValidation.IsValidSigningCertUrl`):
   - Must be `https://` scheme.
   - Host must match the anchored regex `^sns\.[a-z0-9-]+\.amazonaws\.com$`
     (rejects `sns.evil.amazonaws.com`, `sns.us-east-1.amazonaws.com.evil.com`,
     `sns.a.b.us-east-1.amazonaws.com`).
   - Path must match the anchored regex
     `^/SimpleNotificationService-[A-Za-z0-9]+\.pem$` — full literal shape with a
     non-empty hash segment. A suffix-only `.pem` check would let
     `/attacker.pem` or `/…/evil` through.
   - Failure → `reason=bad_cert_url`, verifier returns `false`, handler → **403**.
2. **Required fields present:** `Signature`, `SignatureVersion`, `Type`,
   `MessageId`, `Timestamp` (missing → `reason=missing_field`, handler → **403**).
3. **Signature version:** only `"1"` or `"2"` are accepted (any other value →
   `reason=missing_field`, handler → **403**).
4. **Timestamp parse + clock skew:** parsed as UTC. If `|now - timestamp| > MaxClockSkew`
   (default 15 min) → `reason=timestamp_skew`, handler → **403**. This is the replay
   window for captured-but-valid payloads.
5. **Canonical string build** (`BuildCanonicalString`): alphabetical key order,
   `key\nvalue\n` concatenation; different field sets for `Notification` vs
   `SubscriptionConfirmation` / `UnsubscribeConfirmation`. Missing `SubscribeURL`
   or `Token` on confirmation messages → `reason=missing_field`.
6. **Signature base64 decode:** invalid base64 → `reason=missing_field`, → **403**.
7. **Certificate fetch** (`GetCertificateAsync`) via the `SnsSigningCert`
   named `HttpClient`:
   - **SSRF-guarded** primary handler (blocks RFC1918, loopback, IMDS at connect).
   - **10s timeout**, **64 KB response cap** (AWS PEM certs are ~2 KB).
   - **Positive LRU cache**: URL-keyed, capacity **32**, TTL `CacheTtl`
     (default 1h). MRU on hit. Evicted entries are not `Dispose`d (concurrent
     verifiers may still hold the `X509Certificate2` reference).
   - **Negative cache**: on fetch failure, URL is negatively cached for
     `NegativeCacheTtl` (default 60s) with reason tag (`http_error`,
     `parse_error`, `other`). Cancellation is not negatively cached.
   - **In-flight coalescing**: `ConcurrentDictionary<Lazy<Task>>` ensures N
     concurrent misses for the same URL trigger exactly one outbound GET.
   - Failure → `reason=cert_fetch_failed`, handler → **403**.
8. **Algorithm selection — v1 vs v2:** The `SignatureVersion` (`"1"` or `"2"`)
   selects the **canonical string layout**, not the crypto algorithm. The
   algorithm is driven by the certificate's public-key type:
   - If `cert.GetRSAPublicKey()` returns non-null → **RSA-SHA256 / PKCS#1 v1.5**
     (`SignatureVersion=1`, classic AWS SNS topics, and v2 on FIFO today).
   - Otherwise if `cert.GetECDsaPublicKey()` returns non-null →
     **ECDSA-SHA256** (reserved for future AWS-issued EC certs; no production
     topics use this yet).
   - Both return non-null → prefer RSA.
   - Neither → `reason=bad_cert`, handler → **403**.
9. **Verification result:** `rsa.VerifyData(payload, signature, SHA256, Pkcs1)`
   or `ecdsa.VerifyData(payload, signature, SHA256)`. Mismatch →
   `reason=signature_mismatch`, handler → **403**.

**Body cap:** enforced *before* the verifier runs, in `EnforceSnsBodyLimitAsync`
(Program.cs). `Content-Length > MaxBodyBytes` (default **150 KB**) → **413**
immediately; chunked bodies are capped via `LengthLimitingStream` and throw
`PayloadTooLargeException` → **413**.

**Replay dedup:** after signature passes, `sns:msgid:{MessageId}` is `SET NX`
in Redis with TTL `ReplayDedupTtl` (default 1h). A dupe → **200 OK** (idempotent,
so SNS stops retrying). Redis outage → fail-open warn log + `sns_dedup_unavailable_total`.

**Kill switch env var:** `Sns__SignatureVerificationEnabled` (see Kill switch
section below).

---

## Black-box test recipe

All cases target the live endpoint. Replace `$BASE` with
`https://email.israeliyonsi.dev`. Expected HTTP codes match the verifier +
handler logic above; note that **bad cert URL and signature mismatch both
return 403** (not 400) — the handler deliberately returns 403 for
signature-adjacent failures so scanners get no useful signal.

### (a) Legitimate AWS-signed payload (captured fixture)

Canonical source for a captured payload: the fixture JSON files used in
`tests/EaaS.WebhookProcessor.Tests/` (look for `SnsSignatureVerifierTests`
fixtures). These are real SNS envelopes captured off the subscribed topic.

```bash
curl -sS -i -X POST "$BASE/webhooks/sns" \
  -H "Content-Type: text/plain; charset=UTF-8" \
  -H "x-amz-sns-message-type: Notification" \
  --data-binary @tests/fixtures/sns/legitimate-bounce.json
# Expected: HTTP/1.1 200 OK
# Metric:   sns_signature_verifications_total{result="success"} +1
```

### (b) Forged `SigningCertURL` pointing off-AWS

Take a legitimate fixture and replace `SigningCertURL` with
`https://attacker.example.com/SimpleNotificationService-abc.pem`.

```bash
jq '.SigningCertURL="https://attacker.example.com/SimpleNotificationService-abc.pem"' \
   tests/fixtures/sns/legitimate-bounce.json > /tmp/forged-cert-url.json

curl -sS -i -X POST "$BASE/webhooks/sns" \
  -H "Content-Type: text/plain; charset=UTF-8" \
  --data-binary @/tmp/forged-cert-url.json
# Expected: HTTP/1.1 403 Forbidden
# Metric:   sns_signature_rejections_total{reason="bad_cert_url"} +1
```

Variants that must also be rejected with `reason=bad_cert_url`:

- Host `sns.us-east-1.amazonaws.com.evil.com` (suffix attack).
- Host `sns.evil.amazonaws.com` (missing region shape).
- Path `/attacker.pem` or `/SimpleNotificationService-abc.pem/../evil`
  (path must match the anchored literal regex).
- `http://` instead of `https://`.

### (c) Tampered signature

Take a legitimate fixture and flip one base64 character in `Signature`.

```bash
jq '.Signature |= (. | sub("A"; "B"))' tests/fixtures/sns/legitimate-bounce.json \
   > /tmp/tampered-sig.json

curl -sS -i -X POST "$BASE/webhooks/sns" \
  -H "Content-Type: text/plain; charset=UTF-8" \
  --data-binary @/tmp/tampered-sig.json
# Expected: HTTP/1.1 403 Forbidden
# Metric:   sns_signature_rejections_total{reason="signature_mismatch"} +1
```

### (d) Expired timestamp (>15 min skew)

```bash
OLD_TS=$(date -u -d '30 minutes ago' +"%Y-%m-%dT%H:%M:%S.000Z")
jq --arg ts "$OLD_TS" '.Timestamp=$ts' tests/fixtures/sns/legitimate-bounce.json \
   > /tmp/expired.json

curl -sS -i -X POST "$BASE/webhooks/sns" \
  -H "Content-Type: text/plain; charset=UTF-8" \
  --data-binary @/tmp/expired.json
# Expected: HTTP/1.1 403 Forbidden
# Metric:   sns_signature_rejections_total{reason="timestamp_skew"} +1
# Note:     The task brief called this "400" — the verifier in fact returns 403
#           for every signature-adjacent rejection to starve scanners of signal.
#           Skew is the replay-window gate; changing the code to 400 would leak
#           which field failed.
```

### (e) Replay same MessageId

Send a legitimate payload twice within `ReplayDedupTtl` (default 1h).

```bash
curl -sS -i -X POST "$BASE/webhooks/sns" \
  -H "Content-Type: text/plain; charset=UTF-8" \
  --data-binary @tests/fixtures/sns/legitimate-bounce.json
# First:  HTTP/1.1 200 OK (processed)

curl -sS -i -X POST "$BASE/webhooks/sns" \
  -H "Content-Type: text/plain; charset=UTF-8" \
  --data-binary @tests/fixtures/sns/legitimate-bounce.json
# Second: HTTP/1.1 200 OK (dedup hit, not reprocessed)
# Metric: sns_dedup_hits_total +1
#         downstream bounce/complaint/delivery handlers NOT invoked
```

Confirm idempotence by checking the bounce/complaint counter in Grafana or the
DB row count for the affected SES `messageId` does not increase on the second POST.

### Body-cap test (bonus)

Payload larger than `MaxBodyBytes` (150 KB default) should be rejected **413**
before any signature work:

```bash
head -c 200000 /dev/urandom | base64 | \
  curl -sS -i -X POST "$BASE/webhooks/sns" \
       -H "Content-Type: text/plain; charset=UTF-8" \
       --data-binary @-
# Expected: HTTP/1.1 413 Payload Too Large
```

---

## Metrics and alerts

Counters (meter name `EaaS.WebhookProcessor.Sns`, source
`src/EaaS.WebhookProcessor/Handlers/SnsMetrics.cs`):

| Counter                                   | Tags                                                                       | Meaning                              |
|-------------------------------------------|----------------------------------------------------------------------------|--------------------------------------|
| `sns_signature_verifications_total`       | `result=success\|rejected\|disabled`                                       | Every verify attempt outcome         |
| `sns_signature_rejections_total`          | `reason=bad_cert_url\|bad_cert\|bad_host\|cert_fetch_failed\|signature_mismatch\|timestamp_skew\|missing_field` | Per-reason reject                    |
| `sns_signature_verification_disabled_total` | —                                                                        | Kill-switch-bypass counter           |
| `sns_cert_fetch_failures_total`           | `reason=http_error\|parse_error\|other`                                    | Cert fetch outcome                   |
| `sns_cert_cache_miss_total`               | —                                                                          | Positive-cache miss → outbound fetch |
| `sns_dedup_hits_total`                    | —                                                                          | Replay suppressed at Redis           |
| `sns_dedup_unavailable_total`             | —                                                                          | Redis error; fail-open dedup         |

Alerts defined in `infrastructure/prometheus/alerts/sns-webhook.yml`:

| Alert                              | Severity | Expression                                                                              | Links to section     |
|------------------------------------|----------|-----------------------------------------------------------------------------------------|----------------------|
| `SnsSignatureVerificationDisabled` | critical | `sum(rate(sns_signature_verifications_total{result="disabled"}[5m])) > 0` for 1m        | Kill switch          |
| `SnsCertFetchFailures`             | high     | `sum(rate(sns_cert_fetch_failures_total[5m])) > 0.1` for 5m                             | Cert fetch failures  |
| `SnsSignatureMismatchSpike`        | high     | `sum(rate(sns_signature_rejections_total{reason="signature_mismatch"}[5m])) > 1` for 2m | Signature mismatch   |
| `SnsVerificationDroppedToZero`     | critical | `absent_over_time(sns_signature_verifications_total[15m]) == 1` for 5m                  | Silent failure       |

Scrape target: Prometheus job `eaas-webhook-processor` scrapes
`webhook-processor:8081/metrics` (see `infrastructure/prometheus/prometheus.yml`).

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
