# Email Provider Infrastructure Runbook

**Owner:** Staff DevOps / Infra
**Status:** Authoritative spec — other agents implement from this document
**Last updated:** 2026-04-14
**Scope:** Infrastructure layer that hosts the multi-provider `IEmailProvider` abstraction (Mailgun + SES + future). Covers nginx routing, secret management, DNS, phased cutover, observability, and strongly-typed config.

> Principal Engineer owns the `IEmailProvider`, `IWebhookSignatureVerifier`, `IEmailEventNormalizer` interfaces. This runbook describes the *host* for those interfaces — the infra layer other agents plug into.

---

## 1. Webhook URL Scheme

### 1.1 Decision

| Concern | Old | New |
|---|---|---|
| Event ingress | `/hooks/sns` | `/hooks/email/{provider}/events` |
| Inbound email | `/hooks/sns/inbound` | `/hooks/email/{provider}/inbound` |
| Payment callbacks | `/hooks/paystack`, `/hooks/flutterwave` | unchanged |

`{provider}` is a closed-set slug: `ses`, `mailgun`, `sendgrid`, `postmark`. nginx MUST NOT forward unknown slugs — the webhook-processor rejects them with 404 for defence-in-depth, but we block at the edge too.

### 1.2 Backward compatibility

Legacy SES webhooks hit `/hooks/sns` and `/hooks/sns/inbound`. We CANNOT 308/redirect those endpoints because:

1. AWS SNS refuses to follow 3xx redirects on subscription confirmations — the `SubscribeURL` handshake breaks.
2. Inbound S3→SNS deliveries also reject redirects silently.

**Strategy:** serve both paths from the same backend for the duration of Phase 2–4. Use an nginx `location` **alias** (same `proxy_pass` target, two URIs). Only remove `/hooks/sns*` after Phase 5 (SES teardown).

### 1.3 nginx config diff

**CRITICAL — do not drift:** Apply the same block to BOTH `docker/nginx/nginx.conf` (HTTP + HTTPS pre-TLS bootstrap) AND `docker/nginx/nginx-ssl.conf` (post-TLS production). `scripts/deploy.sh` line 105 copies `nginx-ssl.conf → nginx.conf` after cert issue — anything missing from `nginx-ssl.conf` silently disappears in production. This has bitten us before (see commit history around SSL webhook path). **Both files must be edited in the same PR.**

Replace the existing single `location /hooks/ { ... }` block in each file with:

```nginx
# Legacy SES/SNS webhooks — kept alive until Phase 5 teardown.
# DO NOT 301/308 — AWS SNS rejects redirects on SubscribeURL.
location = /hooks/sns {
    proxy_pass http://webhook-processor:8081/webhooks/email/ses/events;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Legacy-Path "true";
}
location = /hooks/sns/inbound {
    proxy_pass http://webhook-processor:8081/webhooks/email/ses/inbound;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Legacy-Path "true";
}

# New provider-agnostic scheme.
# Regex-constrained to the allow-listed providers.
location ~ ^/hooks/email/(ses|mailgun|sendgrid|postmark)/(events|inbound)$ {
    proxy_pass http://webhook-processor:8081$request_uri;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;

    # Mailgun posts up to ~5MB for inbound MIME; SES via SNS stays small
    client_max_body_size 15m;
    proxy_read_timeout 30s;
}

# All other /hooks/ routes (paystack, flutterwave, etc.) — unchanged.
location /hooks/ {
    proxy_pass http://webhook-processor:8081/webhooks/;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

**Route matching order:** nginx evaluates `= exact` before `~ regex` before `prefix`, so the two legacy exact locations win over the catch-all `/hooks/`, and the regex provider route is chosen for the new paths without shadowing other `/hooks/*` services.

### 1.4 Validation

After edit, before deploy:

```bash
docker run --rm -v $PWD/docker/nginx/nginx.conf:/etc/nginx/nginx.conf:ro \
  -v $PWD/docker/nginx/nginx-ssl.conf:/etc/nginx/nginx-ssl.conf:ro \
  nginx:1.25-alpine nginx -t -c /etc/nginx/nginx-ssl.conf
```

Post-deploy smoke (from VPS):

```bash
curl -s -o /dev/null -w "%{http_code}\n" https://sendnex.xyz/hooks/email/ses/events     # expect 405 GET
curl -s -o /dev/null -w "%{http_code}\n" https://sendnex.xyz/hooks/email/mailgun/events # expect 405 GET
curl -s -o /dev/null -w "%{http_code}\n" https://sendnex.xyz/hooks/email/bogus/events   # expect 404
curl -s -o /dev/null -w "%{http_code}\n" https://sendnex.xyz/hooks/sns                  # expect 405 GET (alias works)
```

---

## 2. Secret Management Abstraction

### 2.1 Today

All secrets live in `/opt/eaas/.env` on the VPS (mode 0600, backed up to `/root/.env.backup`). They are injected as env vars into containers via `docker-compose.yml` and bound into .NET through `IOptions<T>`.

Current email-related secrets:
- `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION` → `Ses__*`
- `TRACKING_HMAC_SECRET`

### 2.2 Target: `ISecretStore`

```csharp
public interface ISecretStore
{
    /// <summary>Read a single secret by its canonical path.</summary>
    Task<string> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Stream descriptors (name + last-rotated-at + version) under a prefix.</summary>
    IAsyncEnumerable<SecretDescriptor> ListAsync(string prefix, CancellationToken ct = default);
}

public record SecretDescriptor(string Key, DateTimeOffset RotatedAt, string Version);
```

Implementations live in `src/EaaS.Infrastructure/Secrets/`:

| Phase | Class | Backing store |
|---|---|---|
| 1 (now) | `EnvVarSecretStore` | `IConfiguration` — reads `/opt/eaas/.env` via .NET env binding |
| 2 (Q3) | `AwsSecretsManagerSecretStore` | AWS Secrets Manager |
| 2 (Q3) | `AzureKeyVaultSecretStore` | Azure Key Vault |

DI registration (single line swap in `Program.cs`):

```csharp
services.AddSingleton<ISecretStore, EnvVarSecretStore>();
```

### 2.3 Key-path convention

Dotted path, flattened to `__` for env vars. Tenant-scoped secrets:

```
Tenants/{tenantId}/Email/Mailgun/ApiKey           -> TENANTS__{TENANTID}__EMAIL__MAILGUN__APIKEY
Tenants/{tenantId}/Email/Mailgun/WebhookSigningKey
Tenants/{tenantId}/Email/Ses/AccessKeyId
Tenants/{tenantId}/Email/Ses/SecretAccessKey
```

Platform-scoped (fallback when a tenant has no per-tenant creds):

```
Platform/Email/Mailgun/ApiKey
Platform/Email/Mailgun/MasterApiKey   # Flex: the real upstream Mailgun credential (see note below)
Platform/Email/Ses/AccessKeyId
Platform/Email/Ses/SecretAccessKey
```

Resolution order in `IEmailProviderFactory`: tenant-specific → platform default → fail fast with `MissingEmailCredentialsException`.

**Flex-mode note (launch config):** On Mailgun Flex there is no subaccount-per-tenant and therefore no per-tenant Mailgun API key. The path shape `Tenants/{tenantId}/Email/Mailgun/ApiKey` is preserved, but on Flex its value is a **SendNex-issued opaque key** that our platform translates to the single shared upstream Mailgun master key at send time. The actual upstream Mailgun credential lives exclusively at `Platform/Email/Mailgun/MasterApiKey`. This is not a Mailgun subaccount key. On Phase 6 upgrade (per tenant), the tenant's value is rotated in-place to a real Mailgun subaccount-scoped API key (or stays opaque if the adapter uses master key + `X-Mailgun-On-Behalf-Of`).

### 2.4 Migration

1. Add `EnvVarSecretStore` reading current flat keys (`Ses__AccessKeyId`, etc.).
2. Add alias table inside `EnvVarSecretStore` mapping canonical paths → legacy env-var names so no `.env` edits are needed day one.
3. New Mailgun secrets go straight to canonical paths.
4. Phase 5+: introduce `AwsSecretsManagerSecretStore`, backfill, flip DI binding, decommission aliases.

---

## 3. DNS Strategy for Tenant Domain Verification

### 3.1 Record diff per tenant

| Purpose | SES (today) | Mailgun (target) |
|---|---|---|
| DKIM | 3× CNAME `*._domainkey.<tenant>` → `*.dkim.amazonses.com` | 2× TXT `k1._domainkey`, `mx._domainkey` with 2048-bit public key |
| SPF | (optional) TXT `v=spf1 include:amazonses.com ~all` | TXT `v=spf1 include:mailgun.org ~all` |
| MX (inbound) | MX → `inbound-smtp.<region>.amazonaws.com` | MX 10 `mxa.mailgun.org`, MX 10 `mxb.mailgun.org` |
| DMARC | optional TXT `_dmarc` | required TXT `_dmarc v=DMARC1; p=none; rua=mailto:...` |
| Return-Path | (n/a) | CNAME `email.<tenant>` → `mailgun.org` |

**Cost to tenant:** 2 CNAME removals + 4–5 record additions. Not trivial — this is mostly a documentation and UX problem, not a technical one.

### 3.2 Self-serve flow

`POST /api/v1/domains/{id}/verify`

Request (empty body, auth via tenant API key). Response shape:

```json
{
  "domainId": "dom_...",
  "provider": "mailgun",
  "status": "pending_dns" | "verified" | "failed",
  "requiredRecords": [
    { "type": "TXT",   "host": "k1._domainkey.tenant.com", "value": "k=rsa; p=MIGf...",   "verified": false },
    { "type": "TXT",   "host": "mx._domainkey.tenant.com", "value": "k=rsa; p=MIGf...",   "verified": false },
    { "type": "MX",    "host": "tenant.com",               "value": "10 mxa.mailgun.org", "verified": false },
    { "type": "MX",    "host": "tenant.com",               "value": "10 mxb.mailgun.org", "verified": false },
    { "type": "TXT",   "host": "tenant.com",               "value": "v=spf1 include:mailgun.org ~all", "verified": false },
    { "type": "CNAME", "host": "email.tenant.com",         "value": "mailgun.org",        "verified": false }
  ],
  "lastCheckedAt": "2026-04-14T10:00:00Z",
  "nextCheckAt":   "2026-04-14T10:05:00Z"
}
```

Worker job `DomainVerificationChecker` polls Mailgun `/domains/{name}/verify` every 5 minutes (backoff to hourly after 24h). Emits domain event `DomainVerifiedEvent` on transition so the dashboard can invalidate cache.

---

## 4. Deployment Strategy (Zero-Downtime)

> **Launch tier decision (2026-04-14): Mailgun Flex PAYG + domain-per-tenant, NOT Foundation + subaccount-per-tenant.**
> SendNex launches on Mailgun Flex (pay-as-you-go, $0.80 per 1k, no monthly minimum, first 5k/mo free for 3 months). Foundation's $35/mo floor is not justified pre-revenue. Flex has no subaccounts, so the multi-tenant isolation boundary is the **tenant's verified sending domain** on a single shared Mailgun account, with every outbound message tagged `v:tenant_id=<id>`. A single master API key (stored at `Platform/Email/Mailgun/MasterApiKey`) is the real upstream credential; per-tenant keys in our DB are SendNex-issued opaque keys that the platform translates to the master key at send time. The `X-Mailgun-On-Behalf-Of` header is NOT set on Flex. Per-tenant upgrade to Foundation/subaccount is Phase 6, triggered per-tenant on dedicated-IP or compliance need.
>
> Architectural impact is minimal: `IEmailProvider`, `IEmailProviderFactory`, `IEmailEventNormalizer`, `IWebhookSignatureVerifier` are identical to the original design. Only `MailgunEmailProvider.SendAsync` (omits `X-Mailgun-On-Behalf-Of`) and `IEmailProviderFactory.GetForTenant` (returns the shared-account adapter for every tenant, parameterised by tenant sending domain and `v:tenant_id`) differ.

Each phase is a separate PR to `prod`. No phase merges until the previous phase has been green for its stated soak window.

### Phase 1 — `IEmailProvider` abstraction, SES-only wiring
- **What ships:** interfaces + `SesEmailProvider` implementation. Existing call sites refactored to `IEmailProvider`. Feature flag `Features__EmailProviderAbstraction=true`.
- **Health checks:** `/health` (API + webhook-processor) return 200; `email_sent_total{provider="ses"}` keeps climbing at pre-deploy rate.
- **Rollback:** revert commit; no schema changes, no config changes.
- **Metrics to watch (30 min):** send rate, bounce rate, p95 send duration. No change expected.

### Phase 2 — Mailgun adapter, dark
- **What ships:** `MailgunEmailProvider` (shared-account mode — no `X-Mailgun-On-Behalf-Of`, every send tagged `v:tenant_id`, posts under the single master API key), `MailgunWebhookSignatureVerifier`, `MailgunEmailEventNormalizer`. Registered in DI but NOT selected by any tenant. New nginx routes live.
- **Health checks:** `curl /hooks/email/mailgun/events` returns 405 (no body). `webhook_signature_rejections_total{provider="mailgun"}` exists but 0.
- **Rollback:** revert commit. nginx routes harmless if no one's sending to them.
- **Soak:** 48h with zero traffic; confirm no startup regressions.

### Phase 3 — Internal burner tenant flipped
- **What ships:** feature flag per tenant `Tenants/{id}/EmailProvider=mailgun`. Flip ONE internal tenant (`tenant_sendnex_internal`). Mailgun sending domain verified out of band.
- **Health checks:** `email_sent_total{provider="mailgun",tenant="sendnex_internal"}` > 0 within 10 minutes of first send. Delivery rate ≥ 99% over 24h. Webhook signature rejections = 0.
- **Rollback:** flip flag back to `ses`; in-flight messages continue through Mailgun until the queue drains, then all new sends go SES. Target RTO: 5 minutes.
- **Soak:** 24h.

### Phase 4 — Cascade to customer tenants
- **Gate:** each customer tenant must re-verify their domain against Mailgun DNS before the flag flips. No silent cutover.
- **Rollout:** batches of 5 tenants/day. Monitor per-tenant delivery, bounce, complaint rates.
- **Rollback:** per-tenant flag flip back to `ses`. No global rollback — blast radius is 1 tenant.

### Phase 5 — SES teardown
- **Precondition:** 14 days with zero SES sends across all tenants.
- **Tear down:** SNS topics, S3 inbound bucket, SES configuration set `sendnex-production`, IAM user. Remove `/hooks/sns*` nginx aliases. Remove `Ses__*` env vars from `/opt/eaas/.env`. Remove `SesEmailProvider` from DI (leave class behind — reinstatement insurance).
- **Rollback:** re-register `SesEmailProvider` in DI, re-add SNS topic (Terraform/Pulumi IaC to be added in Phase 2 — tracked as separate ticket).

### Phase 6 — Optional Foundation upgrade per tenant (on-demand)

Triggered per-tenant, not on a schedule. Fires when a specific tenant earns (a) dedicated IP / isolated reputation, (b) compliance-driven isolation (HIPAA/GDPR ring-fencing), or (c) volume/SLA requirements that Flex can't meet.

- **What ships:** the subaccount-aware variant of `MailgunEmailProvider` (sets `X-Mailgun-On-Behalf-Of`, or uses a subaccount-scoped API key). Code is parked from Phase 2 — this phase just wires it in DI for upgraded tenants.
- **Account change:** upgrade the Mailgun account to Foundation ($35/mo floor) or Scale. One-time — subsequent tenant upgrades reuse the same account.
- **Per-tenant cutover:** `POST /v5/accounts/subaccounts?name=<tenantslug>` → store `MailgunSubaccountId` on the tenant row → migrate the tenant's verified sending domain under the subaccount → reissue inbound routes on-behalf-of the subaccount. `IEmailProviderFactory.GetForTenant` returns the subaccount-aware adapter for upgraded tenants; all other tenants continue on the shared-account adapter.
- **Secrets:** the tenant's `Tenants/{tenantId}/Email/Mailgun/ApiKey` value is rotated from a SendNex-issued opaque key to a real Mailgun subaccount-scoped API key (or kept opaque if we stay on `X-Mailgun-On-Behalf-Of` with the master key).
- **Health checks:** per-tenant send rate flat through the cutover; `email_webhook_events_total{tenant=<upgraded>}` keeps flowing; no signature rejections.
- **Rollback:** factory flip back to shared-account adapter for that tenant; the subaccount stays dormant (can be reused on a subsequent attempt). Target RTO: 10 minutes.
- **Soak:** 48h dual-route overlap (shared + subaccount) before removing the shared-account sending path for the upgraded tenant.

---

## 5. Observability Plan

### 5.1 Prometheus metrics (emitted by `Application` layer, labelled by provider)

| Metric | Type | Labels | Notes |
|---|---|---|---|
| `email_sent_total` | counter | `provider`, `tenant`, `result` (`success`/`failure`) | bump on every `IEmailProvider.SendAsync` return |
| `email_send_duration_seconds` | histogram | `provider` | buckets: `0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10` |
| `email_provider_errors_total` | counter | `provider`, `error_type` (`auth`, `rate_limit`, `timeout`, `5xx`, `other`) | |
| `webhook_signature_rejections_total` | counter | `provider`, `reason` | from `IWebhookSignatureVerifier` |
| `email_webhook_events_total` | counter | `provider`, `event_type` (`delivered`, `bounced`, `complained`, `opened`, `clicked`) | after normalization |
| `email_domain_verification_status` | gauge | `tenant`, `domain`, `provider` | 1=verified, 0=pending |

### 5.2 Grafana dashboard

File: `infrastructure/grafana/dashboards/email-providers.json`. Layout:

- **Row per provider** (`ses`, `mailgun`): send rate, error rate, p50/p95/p99 latency, webhook lag.
- **Global row:** comparative send rate, side-by-side delivery/bounce/complaint percentages.
- **Tenant drill-down** templated by `$tenant`.

### 5.3 Alerts (Alertmanager)

```yaml
- alert: EmailProviderHighErrorRate
  expr: |
    sum by (provider) (rate(email_provider_errors_total[5m]))
      / sum by (provider) (rate(email_sent_total[5m])) > 0.01
  for: 5m
  annotations:
    summary: "{{ $labels.provider }} error rate > 1%"
- alert: EmailWebhookSignatureRejections
  expr: sum by (provider) (rate(webhook_signature_rejections_total[5m])) > 0.1
  for: 5m
- alert: EmailProviderDown
  expr: absent(rate(email_sent_total[10m]))
  for: 15m
```

### 5.4 Log correlation

Every log statement from Application through Infrastructure MUST flow through an `ILogger` scope containing:

```
using var scope = _logger.BeginScope(new Dictionary<string, object>
{
    ["ProviderName"] = provider.Name,
    ["TenantId"]     = tenantId,
    ["MessageId"]    = message.Id,
    ["CorrelationId"] = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
});
```

Loki query pattern: `{service="api"} | json | ProviderName="mailgun" | TenantId="..."`.

---

## 6. Config & Cost Table

### 6.1 `EmailProviderConfigKeys` — single source of truth, zero magic strings

New file: `src/EaaS.Infrastructure/Email/Configuration/EmailProviderConfigKeys.cs`

```csharp
public static class EmailProviderConfigKeys
{
    public const string Section = "EmailProviders";

    public static class Mailgun
    {
        public const string Section            = "EmailProviders:Mailgun";
        public const string ApiKey             = "EmailProviders:Mailgun:ApiKey";
        public const string Region             = "EmailProviders:Mailgun:Region";              // "us" | "eu"
        public const string WebhookSigningKey  = "EmailProviders:Mailgun:WebhookSigningKey";
        public const string DefaultDomain      = "EmailProviders:Mailgun:DefaultDomain";
    }

    public static class Ses
    {
        public const string Section            = "EmailProviders:Ses";
        public const string AccessKeyId        = "EmailProviders:Ses:AccessKeyId";
        public const string SecretAccessKey    = "EmailProviders:Ses:SecretAccessKey";
        public const string Region             = "EmailProviders:Ses:Region";
        public const string ConfigurationSet   = "EmailProviders:Ses:ConfigurationSetName";
    }

    public static class Routing
    {
        public const string Section            = "EmailProviders:Routing";
        public const string DefaultProvider    = "EmailProviders:Routing:DefaultProvider";
    }
}
```

Bound via strongly-typed options classes (`MailgunOptions`, `SesOptions`, `EmailRoutingOptions`) registered with `services.AddOptions<T>().BindConfiguration(EmailProviderConfigKeys.Mailgun.Section).ValidateDataAnnotations().ValidateOnStart()`.

### 6.2 Env vars post-migration (`/opt/eaas/.env`)

| Env var | Bound config key | Purpose |
|---|---|---|
| `EmailProviders__Mailgun__ApiKey` | `EmailProviders:Mailgun:ApiKey` | Platform Mailgun master API key — the single upstream credential on Flex. Per-tenant subaccount-scoped keys introduced in Phase 6 live only in `ISecretStore` at `Tenants/{tenantId}/Email/Mailgun/ApiKey` — never as env vars. |
| `EmailProviders__Mailgun__Region` | `EmailProviders:Mailgun:Region` | `us` or `eu` |
| `EmailProviders__Mailgun__WebhookSigningKey` | `EmailProviders:Mailgun:WebhookSigningKey` | HMAC signature verification |
| `EmailProviders__Mailgun__DefaultDomain` | `EmailProviders:Mailgun:DefaultDomain` | Platform fallback send domain |
| `EmailProviders__Ses__AccessKeyId` | `EmailProviders:Ses:AccessKeyId` | (Phases 1–4 only) |
| `EmailProviders__Ses__SecretAccessKey` | `EmailProviders:Ses:SecretAccessKey` | (Phases 1–4 only) |
| `EmailProviders__Ses__Region` | `EmailProviders:Ses:Region` | |
| `EmailProviders__Ses__ConfigurationSetName` | `EmailProviders:Ses:ConfigurationSetName` | |
| `EmailProviders__Routing__DefaultProvider` | `EmailProviders:Routing:DefaultProvider` | `ses` in Phase 1; `mailgun` after Phase 4 |

Legacy `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`, `Ses__*` env vars remain readable by `EnvVarSecretStore` via an alias table during the transition; delete at Phase 5.

`docker-compose.yml` must pass these through to `api`, `worker`, and `webhook-processor` (same three services that currently receive `Ses__*`).

---

## 7. File-Edit Checklist (for implementing agents)

- [ ] `docker/nginx/nginx.conf` — both legacy + new `/hooks/email/*` locations
- [ ] `docker/nginx/nginx-ssl.conf` — **identical** edit, same PR (drift-bug guard)
- [ ] `docker-compose.yml` — add `EmailProviders__*` env pass-through to `api`, `worker`, `webhook-processor` services
- [ ] `src/EaaS.Infrastructure/Email/Configuration/EmailProviderConfigKeys.cs` — new
- [ ] `src/EaaS.Infrastructure/Secrets/ISecretStore.cs` + `EnvVarSecretStore.cs` — new
- [ ] `infrastructure/prometheus/prometheus.yml` — scrape targets already cover api + webhook-processor; no change
- [ ] `infrastructure/grafana/dashboards/email-providers.json` — new
- [ ] `docs/runbooks/email-provider-infrastructure.md` — this document

---

## 8. Open Questions for Principal Engineer

1. **Tenant-to-provider binding storage:** table column on `tenants` (e.g. `email_provider_slug`) vs a first-class `tenant_email_provider_bindings` table with history? Audit requirements likely force the latter.
2. **Per-message provider override:** does `SendEmailCommand` accept an explicit provider, or is it always resolved from the tenant? Needed for transactional replay / smoke tests.
3. **Inbound routing from Mailgun:** Mailgun posts MIME as `multipart/form-data` up to ~25MB. Current webhook-processor has `client_max_body_size 10m` globally. I've set `15m` on the new route — confirm whether we also need to bump the processor's Kestrel `MaxRequestBodySize` or if we should cap Mailgun inbound size at 15MB platform-wide.
4. **Signature verification timing:** is verification done in nginx (fast-fail, leaks 4xx semantics) or in `IWebhookSignatureVerifier` inside the .NET app (slower, but centralized)? My spec assumes the latter.
5. **Secret rotation SLA:** what's the acceptable staleness for `EnvVarSecretStore.GetAsync`? Today env vars are frozen at container start — rotating requires a restart. Is that OK until we ship `AwsSecretsManagerSecretStore`, or do we need a hot-reload shim?
6. **Bounce/complaint feedback loop ownership:** does the Mailgun adapter write directly to our suppression list, or does the normalizer emit a `SuppressionEvent` that a separate handler consumes? (Cleaner, but one more moving part.)

---

## 9. Lessons referenced

- **nginx config drift:** `scripts/deploy.sh:105` performs `cp nginx-ssl.conf nginx.conf` after cert issue. Any change to `/hooks/*` routing MUST be applied to both files in the same PR. We have been bitten by webhook endpoints working in staging (HTTP) and silently 404-ing in production (HTTPS) because only one file was edited.
