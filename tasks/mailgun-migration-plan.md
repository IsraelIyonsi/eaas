# SendNex: AWS SES -> Mailgun Migration Plan

Research basis: Mailgun's documentation portal, NuGet, and Mailgun pricing page (all URLs verified live on 2026-04-14). Note the Mailgun docs site restructured: OpenAPI routes live under `/docs/mailgun/api-reference/send/mailgun/*`, not the old `openapi-final/tag/*` paths.

Authoritative index fetched from: https://documentation.mailgun.com/llms.txt

---

## 1. Messages API (Outbound Send)

- **Endpoint spec:** https://documentation.mailgun.com/docs/mailgun/api-reference/send/mailgun/messages/post-v3--domain-name--messages
- **HTTP how-to:** https://documentation.mailgun.com/docs/mailgun/user-manual/sending-messages/send-http
- **Attachments how-to:** https://documentation.mailgun.com/docs/mailgun/user-manual/sending-messages/send-attachments

### Endpoint
```
POST /v3/{domain_name}/messages        # multipart/form-data
POST /v3/{domain_name}/messages.mime   # pre-built RFC-compliant MIME
```
Regions: `https://api.mailgun.net` (US) or `https://api.eu.mailgun.net` (EU). Auth: HTTP Basic with username `api` and password `YOUR_API_KEY`. Max message size: **25 MB**. Send options combined (`o:`, `h:`, `v:`, `t:`) limited to **16 KB total**.

### Required params
`from`, `to` (array), `subject`, and **one of** `text`, `html`, `amp-html`, or `template`.

### Key optional params
- Recipients: `cc[]`, `bcc[]` (friendly name format supported).
- Content: `text`, `html`, `amp-html`, `attachment[]` (file), `inline[]` (inline images referenced via `cid:`).
- Template: `template`, `t:version`, `t:text=yes`, `t:variables="{...json...}"`.
- Tagging: `o:tag` (multiple allowed).
- Tracking: `o:tracking=yes|no`, `o:tracking-opens`, `o:tracking-clicks`.
- DKIM: `o:dkim=yes|no`, `o:secondary-dkim`, `o:secondary-dkim-public`.
- Scheduling: `o:deliverytime` (RFC-2822, up to 3 or 7 days by plan), `o:deliver-within=1h30m`.
- Custom variables: `v:my-var=value` (preferred) OR batch-inline via `-F recipient-variables='{...}'` using `%recipient.x%` substitutions. Exposed in webhooks as `user-variables`.

### Example send
```bash
curl -s --user 'api:YOUR_API_KEY' \
  https://api.mailgun.net/v3/YOUR_DOMAIN_NAME/messages \
  -F from='Excited User <postmaster@YOUR_DOMAIN_NAME>' \
  -F to=recipient@example.com \
  -F subject='Hello there!' \
  -F text='Testing some Mailgun awesomeness!' \
  -F o:tag='newsletters' \
  -F o:tracking-opens='yes' \
  -F v:tenant-id='t_abc123'
```

### Response shape
```json
{ "id": "<20260205213049.xxxxx@yourdomain.com>", "message": "Queued. Thank you." }
```

---

## 2. Routes (Inbound Email)

- **Overview:** https://documentation.mailgun.com/docs/mailgun/user-manual/receive-forward-store/routes
- **Filters:** https://documentation.mailgun.com/docs/mailgun/user-manual/receive-forward-store/route-filters
- **Actions:** https://documentation.mailgun.com/docs/mailgun/user-manual/receive-forward-store/route-actions
- **Parsed POST payload spec:** https://documentation.mailgun.com/docs/mailgun/user-manual/receive-forward-store/receive-http
- **Routes API (create):** https://documentation.mailgun.com/docs/mailgun/api-reference/send/mailgun/routes/post-v3-routes

### Filter DSL (Python-style regex)
```
match_recipient("^support@example\\.com$")
match_recipient("(?P<user>.*?)@(?P<domain>.*)")
match_header("subject", ".*urgent.*")
catch_all()
```
Named captures interpolate into actions via `\g<name>`.

### Actions
```
forward("https://api.sendnex.io/inbound/mailgun")      # HTTP POST
forward("agent@example.com")                           # SMTP forward
store(notify="https://api.sendnex.io/stored/notify")
stop()                                                 # halt priority waterfall
```
Multiple destinations: comma-separated inside one `forward()` call. Default behavior evaluates ALL routes; use `stop()` to short-circuit.

### Create a route programmatically
```
POST /v3/routes
Content-Type: application/x-www-form-urlencoded

priority=10
description=Tenant t_abc123 inbound
expression=match_recipient("^(.+)@inbound.t-abc123.sendnex.io$")
action=forward("https://api.sendnex.io/inbound/mailgun?tenant=t_abc123&mailbox=\\1")
action=stop()
```
Response: `{ "message": "Route has been created", "route": { "id": "...", "priority": 10, "expression": "...", "actions": [...], "created_at": "..." } }`

**Important caveat:** Routes are defined **globally per account**, not per domain (docs: "Get the list of routes. Note that routes are defined globally, per account, not per domain."). In a multi-tenant model this is a big deal — we must either (a) give every tenant a subaccount (subaccount-scoped routes), or (b) prefix every route expression with a tenant discriminator.

### Inbound POST payload (from forward(url))
`application/x-www-form-urlencoded` (or `multipart/form-data` when attachments present). Fields:
- `signature`, `timestamp`, `token` (HMAC, same verification as webhooks)
- `subject`, `sender`, `from`, `recipient`, `message-headers` (JSON array)
- `body-plain`, `body-html`, `stripped-text`, `stripped-signature`, `stripped-html`
- `attachment-count`, `attachment-1`, `attachment-2`, ..., `content-id-map`
- If URL ends in `/mime` or `/raw-mime`: `body-mime` field instead of plain/html.

### Retry behavior
- `200` = success, no retry. `406` = permanent reject, no retry. Anything else = retries at 10m, 15m, 30m, 1h, 2h, 4h — capped at 8 hours total.

---

## 3. Webhooks (Outbound Event Notifications)

- **Overview:** https://documentation.mailgun.com/docs/mailgun/user-manual/webhooks/webhooks
- **Configuring:** https://documentation.mailgun.com/docs/mailgun/user-manual/webhooks/configuring-webhooks
- **Signature/HMAC verification:** https://documentation.mailgun.com/docs/mailgun/user-manual/webhooks/securing-webhooks
- **Payload examples:** https://documentation.mailgun.com/docs/mailgun/user-manual/webhooks/webhook-payloads
- **Event types:** https://documentation.mailgun.com/docs/mailgun/user-manual/events/event-types

### Event types
`accepted`, `rejected`, `delivered`, `failed` (with `severity: permanent` or `temporary`), `opened`, `clicked`, `unsubscribed`, `complained`, `stored`.

> Note: Mailgun collapses SES's bounce/delivery-delay/complaint into `failed` (w/ severity) + `complained`. Our event mapper needs a dedicated layer.

### Configuration rules
- HTTPS endpoint must use a **CA-signed cert**, not self-signed.
- Webhooks configurable at domain-level OR account-level (inherit to all subaccounts). Max 3 URLs per event type.
- Regions US/EU are isolated — configure separately.
- Deduplication: same URL at domain + account = delivered once (account-level wins).

### Signature verification algorithm
Payload includes a `signature` block:
```json
{
  "signature": {
    "token": "e0b5477167110d68991efc6b9f89f0a11066af27834600e123",
    "timestamp": "1770920772",
    "signature": "12d99f5a15355c180971bed7494d578b093c958f57766f3fe750761baed12345"
  },
  "event-data": { ... }
}
```
**Algorithm:** `HMAC_SHA256(key = WebhookSigningKey, message = timestamp || token)` -> hex digest; compare constant-time to `signature`.

.NET equivalent (conceptual only, code in later phase):
```csharp
using var h = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
var hex = Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(timestamp + token))).ToLowerInvariant();
return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hex), Encoding.UTF8.GetBytes(signature));
```
Plus: cache token for replay protection, loosely check timestamp freshness (not too aggressive — Mailgun can delay).

Subaccount events also include a `parent-signature` so receivers verify against the primary account's signing key only.

### Sample `delivered` payload (abridged)
```json
{
  "event": "delivered",
  "id": "MXcc2gEpS-eN8HfkOnmK2w",
  "timestamp": 1770146431.6585283,
  "account": { "id": "1234567890303a4bd1f33898" },
  "domain": { "name": "sample.mailgun.com" },
  "recipient": "recipient@sample.com",
  "message": { "headers": { "message-id": "...", "from": "...", "to": "...", "subject": "..." } },
  "delivery-status": { "code": 250, "message": "OK", "mx-host": "...", "tls": true, "attempt-no": 1 },
  "envelope": { "sender": "...", "sending-ip": "161.38.194.10", "transport": "smtp" },
  "tags": ["webhook_payload"],
  "user-variables": {}
}
```
`failed` payloads add `severity: "permanent"|"temporary"`, `reason`, and `delivery-status.bounce-type: "hard"|"soft"`.

---

## 4. Subaccounts (Multi-Tenant Model)

> **DEFERRED — not wired in Phase 1.** SendNex launches on Mailgun Flex (see §7 "Launch tier"), which has no subaccounts. The tenancy boundary at launch is **domain-per-tenant on a single shared account**, tagged with `v:tenant_id`. The subaccount client helper described below stays dark until **Phase 6** (per-tenant Foundation upgrade). Keep the code/spec here as reference — do not register it in DI in Phases 1–5.

- **Overview:** https://documentation.mailgun.com/docs/mailgun/user-manual/subaccounts/subaccounts
- **Features:** https://documentation.mailgun.com/docs/mailgun/user-manual/subaccounts/subaccounts-features
- **API requests on behalf of:** https://documentation.mailgun.com/docs/mailgun/user-manual/subaccounts/subaccounts-api-requests
- **Create endpoint:** https://documentation.mailgun.com/docs/mailgun/api-reference/send/mailgun/subaccounts/post-v5-accounts-subaccounts

### Model
- **Wholly separate assets per subaccount:** sending domains, IPs, API keys, users, templates, webhooks, reputation.
- **Rolled-up billing:** usage aggregates to primary; no separate billing per subaccount.
- **Feature-gated:** requires Foundation plan or above (confirmed on pricing page — Subaccounts row is a Foundation/Scale feature).

### Create subaccount
```
POST /v5/accounts/subaccounts?name=tenant_abc123
Auth: Basic api:PRIMARY_API_KEY

Response 200:
{ "subaccount": { "id": "646d00a1b32c35364a2ad34f", "name": "tenant_abc123",
  "status": "open", "created_at": "...", "updated_at": "...", "features": {...} } }
```

### Two ways to call API as subaccount
1. **Mint a subaccount-scoped API key** (recommended for per-tenant credential isolation).
2. **Use primary key + header** `X-Mailgun-On-Behalf-Of: <SUBACCOUNT_ID>`.
   ```bash
   curl --user 'api:PRIMARY_KEY' \
     https://api.mailgun.net/v3/SUBACCOUNT_DOMAIN/messages \
     -H "X-Mailgun-On-Behalf-Of: 646d00a1b32c35364a2ad34f" \
     -F from=...
   ```
   If header is missing the action silently targets the primary account — critical footgun.

**Future state (Phase 6 onward):** one Mailgun subaccount per upgraded SendNex tenant gives that tenant an isolated IP reputation, domain list, API key, and webhook config. This is cleaner than SES which required us to manage configuration sets and dedicated IP pools manually. Not wired at launch — see §4 DEFERRED banner and §8 Phase 6.

---

## 5. Domains + DKIM/SPF/MX

- **Domain verification (full DNS table):** https://documentation.mailgun.com/docs/mailgun/user-manual/domains/domains-verify
- **DKIM / Automatic Sender Security:** https://documentation.mailgun.com/docs/mailgun/user-manual/domains/dkim_security
- **Create domain API:** https://documentation.mailgun.com/docs/mailgun/api-reference/send/mailgun/domains/post-v4-domains
- **Verify API:** https://documentation.mailgun.com/docs/mailgun/api-reference/send/mailgun/domains/put-v4-domains--name--verify

### DNS records a tenant must add
| Type  | Req | Purpose                         | Value                                       |
|-------|-----|---------------------------------|---------------------------------------------|
| TXT   | Yes | SPF                             | `v=spf1 include:mailgun.org ~all`           |
| TXT   | Yes | DKIM (manual mode)              | Generated per domain; shown in UI/API       |
| CNAME | Yes | Open/click/unsubscribe tracking | `email.<domain>` -> `mailgun.org`           |
| MX    | Yes | Inbound (for routes)            | `10 mxa.mailgun.org`, `10 mxb.mailgun.org`  |
| CNAME | Alt | Automatic Sender Security       | `pdk1._domainkey...` and `pdk2._domainkey...` -> Mailgun hosts. Key auto-rotates every 120d (min 5d). Preferred over TXT. |
| TXT   | Rec | DMARC (tenant manages)          | `v=DMARC1; p=quarantine; rua=mailto:...`    |

### Self-serve domain flow (fully API-driven)
```
POST /v4/domains            body: name=send.tenant.com&use_automatic_sender_security=true
 -> returns sending_dns_records[] and receiving_dns_records[]
(Tenant adds DNS)
PUT  /v4/domains/{name}/verify    -> 200 when all records propagated and pass checks
```
Verified domains: not subject to 300 emails/day sandbox cap; no "sent via Mailgun" tag. Can take 24-48h for DNS propagation.

DKIM options: 1024-bit or 2048-bit; up to 3 active keys per domain (round-robin signed); manual rotation OR ASS (Automatic Sender Security) via CNAME delegation to Mailgun-hosted keys.

---

## 6. .NET SDK

- **Official Mailgun SDKs page:** https://documentation.mailgun.com/docs/mailgun/sdk/introduction
- **Officially supported languages:** Go, Node.js, PHP, Java, Ruby, Python. **No official .NET SDK.**

### NuGet survey (2026-04-14)
| Package                     | Downloads | Last updated | Verdict |
|-----------------------------|-----------|--------------|---------|
| **FluentEmail.Mailgun**     | 1.6M      | 2022-03-17   | Most-used, stale; abstraction-heavy wrapper over FluentEmail. OK as a quick drop-in, but stale. |
| mailgun_csharp              | 120k      | 2024-11-14   | Active-ish; basic send + batch only (no routes/webhooks/subaccounts). |
| Mailgun.Models.SignedEvent  | 126k      | 2020-06-29   | Just DTO/HMAC models for inbound webhook verification. Useful as a reference. |
| PureApi.Mailgun             | 123k      | 2018-08-09   | Abandoned. |
| mnailgun                    | smaller   | —            | Old. |
| cloudscribe.Email.Senders   | 709k      | 7 days ago   | Multi-ESP abstraction (SendGrid/Mailgun/Elastic). Not purpose-built. |

### Recommendation
**Build a thin first-party `SendNex.Mailgun` client** over `HttpClient` + `IHttpClientFactory`:
- Mailgun API surface we need (Messages, Routes, Domains, Subaccounts, Webhooks, Events) is small and well-documented. An owned client is ~400 LOC and aligns with our clean architecture.
- Official support on `FluentEmail.Mailgun` ended in 2022 — unacceptable for a product built on it.
- Reuse `Mailgun.Models.SignedEvent` types as inspiration for the webhook HMAC verifier — signature scheme is already a one-liner with `HMACSHA256`.
- Resilience: wrap with Polly for transient 5xx + respect 429 with `Retry-After`.

---

## 7. Pricing & ESP Policy

### Launch tier: Flex PAYG, not Foundation

**Decision (2026-04-14):** SendNex launches on **Mailgun Flex (pay-as-you-go)** — $0.80 per 1k sent, **no monthly minimum**, first 5,000/mo free for the first 3 months. The Foundation $35/mo floor is not justifiable while SendNex is pre-revenue.

**Tenancy-model shift (Flex has no subaccounts):**
- Single Mailgun account, single master API key held by the SendNex platform (stored at `Platform/Email/Mailgun/MasterApiKey`).
- The **isolation boundary is the tenant's sending domain**, which every tenant already brings for DKIM. Domain-per-tenant, not subaccount-per-tenant.
- Every outbound message is tagged with `v:tenant_id=<id>` (Mailgun custom variable) for routing, attribution, and analytics.
- SendNex issues its own opaque per-tenant API keys (stored in our DB, still at the `Tenants/{tenantId}/Email/Mailgun/ApiKey` secret path). The platform translates each to the single upstream Mailgun master key at send time.
- Mailgun routes are account-global — we prefix route expressions with a tenant discriminator (`match_recipient("^(.+)@inbound.<tenantslug>.sendnex.io$")`) or match on the tenant's verified sending domain.
- **`X-Mailgun-On-Behalf-Of` header is NOT set on Flex** (no subaccount exists to address).

**Trade-offs (documented honestly):**
- One abusive tenant can dent the shared account's reputation. Mitigation: strict onboarding send-rate limits, auto-suspend on bounce/complaint thresholds, fast manual kill switch.
- No per-tenant dedicated IPs on Flex. Acceptable — nobody needs dedicated IPs below ~100k/mo.
- ✅ $0 fixed cost; spend scales with revenue.
- ✅ Migration to subaccounts later is config-only: the `IEmailProvider` abstraction already takes `tenantId`; swap the adapter from "shared-key" to "subaccount key + `X-Mailgun-On-Behalf-Of`" for the tenants that upgrade.

**Upgrade trigger to Foundation ($35+):** the first tenant that needs a dedicated IP, isolated reputation, or compliance-driven isolation. Pay the $35 when that tenant's revenue justifies it, not before. See Phase 6 below.

**Architectural impact — almost nothing changes.** The `IEmailProvider` / `IEmailProviderFactory` / `IEmailEventNormalizer` / `IWebhookSignatureVerifier` abstractions are identical. Only two things differ on Flex:
1. `MailgunEmailProvider.SendAsync` omits `X-Mailgun-On-Behalf-Of` and sends under the shared account.
2. `IEmailProviderFactory.GetForTenant` returns the same concrete `MailgunEmailProvider` for every tenant, parameterised by the tenant's verified sending domain and `v:tenant_id` tag — no subaccount lookup.

- **Live pricing:** https://www.mailgun.com/pricing/

| Tier       | Monthly | Included emails/mo | Overage (per 1K)      | Subaccounts? | Retention           |
|------------|---------|---------------------|------------------------|--------------|---------------------|
| Free trial | $0      | Trial only          | —                      | No           | 1 day logs          |
| **Flex (PAYG) ← SendNex launch** | **$0 (min)** | **Pay $0.80/1K; first 5K free for 3 mo** | **$0.80/1K** | No | 1 day logs |
| Basic      | ~$15    | 10K (legacy)        | $1.80/1K               | No           | 1 day logs          |
| Foundation | $35     | 50,000              | $1.30/1K               | Yes (Phase 6 upgrade trigger) | 5d logs, 1d msgs    |
| Scale      | $90     | 100,000             | $1.10/1K               | Yes + SAML SSO, STO, dedicated IP pools | 30d logs, up to 7d msgs |
| Enterprise | Quote   | Custom              | Custom + Rapid Fire SLA| Yes          | Custom              |

**For SendNex (multi-tenant ESP-on-ESP):** **launch on Flex PAYG** (see "Launch tier" decision above). Upgrade individual tenants to Foundation/Scale only when they earn it (dedicated IP, SAML SSO, isolated reputation, compliance). The per-tenant upgrade path is Phase 6.

**AUP / ESP-of-ESP check:** Mailgun permits sending "on behalf of customers" — that's literally the subaccount feature. Standard prohibitions (no spam, no purchased lists, no adult/illegal content) apply to our tenants; we pass them through in our SendNex ToS. No clause prohibits running a reseller/ESP on Mailgun as long as sending complies with CAN-SPAM/GDPR. (Full verification required against https://www.mailgun.com/legal/acceptable-use/ before GA — quick check, not a blocker.)

---

## 8. Five-Phase Migration Checklist

### Phase 0 — Prep (week 0, 2-3 days)
- [ ] Sign up for **Mailgun Flex (PAYG)**; create single shared account (US region, EU later if needed). No Foundation contract at launch.
- [ ] Generate master API key + webhook signing key; store at `Platform/Email/Mailgun/MasterApiKey` (Azure Key Vault / secret store).
- [ ] Build `SendNex.Mailgun` client library (send / routes / domains / webhook HMAC verify). The subaccount helper is built but left unregistered — reactivated in Phase 6.
- [ ] Add `ProviderType` + `MailgunProviderConfig` to the PostgreSQL tenant schema (EF Core migration). Track `MailgunApiKey` (SendNex-issued opaque key, encrypted), sending-domain mapping table. `MailgunSubaccountId` column is nullable and stays NULL until Phase 6.

**Risk:** Key leakage. **Rollback:** Revoke key, issue new one; no production impact yet.

### Phase 1 — Parallel-Send Adapter (week 1-2) — shared-account adapter, no subaccounts
- [ ] Introduce `IEmailProvider` abstraction; make existing SES path implement it.
- [ ] Implement `MailgunEmailProvider` against the new client — **shared-account mode**: no `X-Mailgun-On-Behalf-Of`, every send carries `v:tenant_id=<id>` and posts to `/v3/{tenant-sending-domain}/messages` under the single master API key.
- [ ] Add `ProviderRouter` that picks per send based on `tenant.PreferredProvider` (defaults to SES).
- [ ] Shadow-send mode: primary=SES, shadow=Mailgun, diff results, log divergence (no double-delivery to recipients).
- [ ] Internal QA tenant set to Mailgun-primary; full smoke tests (text/html/attachments/templates/batch/custom vars).

**Risk:** Double-send bug double-bills recipients. **Mitigation:** Shadow mode uses test-mode sends (`o:testmode=yes`) or a dedicated `shadow@` recipient. **Rollback:** Feature flag `providerRouterEnabled=false`.

### Phase 2 — Tenant Feature Flag (week 3-4) — **REVISED 2026-04-19** (pass 2)

Revision history:
- **Pass 1** (2026-04-19 a.m.): original one-paragraph scope. Both architects returned BLOCK. 12 gaps captured in `tasks/phase2-architect-findings.md`.
- **Pass 2** (2026-04-19 p.m.): Senior APPROVE, Independent BLOCK with 8 must-fix items + Senior flagged 3 substantive nice-to-haves. Items 1–11 folded into this revision. See subsections below for per-item resolution.

#### 2.a Pre-flight audit (hard gate — all must be green before any Phase 2 code)

- [x] **Schema-drift check on PR #49 hotfix**: `scripts/migrate_mailgun_webhooks_dedup.sql` byte-for-byte matches the `Up()` method of EF migrations `20260414154548_AddEmailProviderColumns` and `20260414185323_AddWebhookDeliveriesDedup`. Both are recorded in `__EFMigrationsHistory`. **Verified 2026-04-19.**
- [x] **TargetFramework unified — VERIFIED 2026-04-20.** `Directory.Build.props` pins `net10.0` for all projects; Independent's `net8.0`/`net10.0` concern was stale `obj/` artifacts, not actual csproj drift. Pre-flight action: run `git clean -fdx src/*/bin src/*/obj tests/*/bin tests/*/obj` on the feature branch before CI to eliminate the stale-artifact false positive.
- [ ] **`AuditAction` enum-mapping audit**: inspect `AuditLog` EF configuration (`src/EaaS.Infrastructure/Persistence/Configurations/*Audit*.cs`). If the column is stored as `int`, inserting new enum values between existing members shifts underlying ordinals and silently reinterprets every historical row. Decision rule: if `int`-stored and history is non-trivial → either (a) convert the column to `varchar` with `[EnumMemberAttribute]` string mapping via a data-migration, or (b) keep `int` and commit to append-only enum growth with explicit integer values on every member. §2.e commits to (b) unless this audit says otherwise.
- [ ] **Deploy-pipeline decision — DECIDED: wire `dotnet ef database update --idempotent` into `deploy.sh`** (Option A). Rationale: Option B (reproduce migrations as raw SQL + `__EFMigrationsHistory` insert per #49 pattern) is tech debt that scales linearly with every future migration and keeps reintroducing drift risk. Option A requires the `dotnet-ef` tool be present in the build/deploy image — single-line `dotnet tool install --global dotnet-ef` in the Dockerfile or CI job. This lands as a **prerequisite commit before the Phase 2 migration**, not alongside it, so the pipeline change can be validated on an existing no-op deploy first.
- [ ] **Secret store choice — DECIDED: AWS Secrets Manager**. Rationale: `AWSSDK.SimpleEmail` and related AWS SDK packages are already transitive dependencies from the SES era; adding `AWSSDK.SecretsManager` reuses the existing IAM credential chain and region config. No new cloud provider onboarding. Azure Key Vault stays as a theoretical alternative for Phase 6+ if a specific tenant demands it.

#### 2.b Data model — new EF migration `AddSendingDomainsProviderColumns`

Extend existing `sending_domains` (do NOT introduce a parallel `TenantDomain` table):

| Column | Type | Nullable | Default | Notes |
|---|---|---|---|---|
| `provider_key` | varchar(32) | NOT NULL | `'ses'` | Matches `tenants.preferred_email_provider_key` value space. |
| `mailgun_domain_state` | varchar(16) | NULL | — | `unverified` \| `active` \| `disabled`. NULL for SES rows. |
| `verification_last_error` | text | NULL | — | Populated by `DomainVerificationJob` on failed verify. |
| `kill_switch_suspended_at` | timestamptz | NULL | — | Set by admin suspend; blocks sends for that domain. |

Rename of `ses_identity_arn` is deferred — keep the column, ignore it for Mailgun rows.

Enum additions:
- `DnsRecordPurpose.TrackingCname` for Mailgun's `email.<domain> → mailgun.org` CNAME.
- `DomainStatus.Verifying` — explicit intermediate state so the canonical FSM is `PendingVerification → Verifying → Verified | Failed | Suspended`. Before this pass the FSM implicitly collapsed `Verifying` into `PendingVerification`, leaking the distinction into the Mailgun-specific `mailgun_domain_state` column. Now `mailgun_domain_state` stays provider-scoped (unverified|active|disabled — Mailgun's terms) and `DomainStatus` carries the canonical SendNex-level FSM.

#### 2.c Abstractions

- **`IDomainIdentityProviderFactory`** in `EaaS.Domain/Providers/` — mirror of `IEmailProviderFactory.GetForTenant`. `GetForProvider(string providerKey)` returns the right `IDomainIdentityService`.
- **`MailgunDomainIdentityService : IDomainIdentityService`** in `EaaS.Infrastructure/EmailProviders/Providers/Mailgun/` — implements `CreateDomainAsync`, `VerifyDomainAsync`, `GetDomainAsync`, emits the correct SPF + DKIM CNAME + tracking CNAME + MX `DnsRecord` rows.
- **`AddDomainHandler` refactor**: route by `tenant.PreferredEmailProviderKey` (or explicit `request.ProviderKey` when admin specifies), invoke the factory, persist provider-specific identity ref.
- `MailgunClient` gains `CreateDomainAsync` (POST /v4/domains with `use_automatic_sender_security=true`) and `VerifyDomainAsync` (PUT /v4/domains/{name}/verify). Existing Phase-1 wire-level test pattern (HttpMessageHandler fake) is the model for new tests.

#### 2.d Verification pipeline (browser → server)

- **Verify polling moves to `EaaS.Worker`** as `DomainVerificationJob`, sibling to existing `ScheduledEmailJob` (same `BackgroundService` + `IServiceScopeFactory` pattern — no worker refactor). Exponential backoff per-domain: 60s → 5m → 15m → 1h, capped at 48h total. On each attempt the job flips `DomainStatus` to `Verifying`, calls Mailgun, then lands `Verified` | `Failed` | stays `Verifying` + writes `mailgun_domain_state`, `verification_last_error`, `LastCheckedAt`.
- **Global concurrency cap**: `DomainVerificationJob` uses a `SemaphoreSlim` (or bounded Channel) with configurable max-in-flight (default 10) so a surge of onboarding tenants cannot stampede Mailgun's shared-account rate limits. Setting lives in `MailgunOptions.MaxVerifyConcurrency`.
- **429 + `Retry-After` honor**: on HTTP 429 the job reads `Retry-After` (seconds or HTTP-date) and schedules the next attempt at `max(Retry-After, next backoff step)`. On 503 with `Retry-After` ditto. No exponential backoff *reduction* on 5xx — we treat 5xx as transient and use the next scheduled step.
- **Server-side single-flight lock** keyed on `(tenantId, domainName)` for `POST /v4/domains` and verify attempts. Redis `SET NX PX` with watchdog renewal (30s TTL, 10s renew) so crashed workers don't deadlock. Multiple admins / tabs / job attempts cannot race.
  - **Watchdog renewal failure semantics**: the renewal loop runs as a separate `Task` linked to a `CancellationTokenSource`. On renewal failure (Redis timeout, ThreadPool starvation, network blip that misses the 10s window) the CTS cancels immediately, which propagates into the in-flight Mailgun call via its linked `CancellationToken`. The operation aborts cleanly rather than continuing under a lock it no longer holds. Losing the lock mid-operation is NOT fenced — on Flex this is acceptable because the worst case is a 4xx-conflict on the subsequent `POST /v4/domains`, which §2.d's reconcile path already handles. A fencing token would be a Phase 6 concern.
- **`DomainVerificationJob` shutdown semantics**: every `await` inside the job honors `stoppingToken` (MassTransit-worker pattern already used by `ScheduledEmailJob.cs:43`). The global `SemaphoreSlim` (§2.d below) is released in a `finally` block so a cancelled attempt does not leak a permit. Per-domain scheduled attempts use `Task.Delay(backoff, stoppingToken)` so shutdown collapses all pending delays cleanly without leaving zombie waiters.
- **Reconcile on 4xx-conflict**: `POST /v4/domains` returning "domain exists" falls through to `GET /v4/domains/{name}` and writes whatever Mailgun already has into `sending_domains`. Handles the dropped-response orphan state.
- **Frontend polls our API, not Mailgun**: `GET /api/v1/domains/{id}` every 60s until `Status` flips, then stops. Browser closing the tab does not lose state — the worker continues independently.

#### 2.e Provider flip — guards and pinning

- **Admin endpoint `POST /api/v1/admin/tenants/{id}/provider`** writes `tenants.preferred_email_provider_key`. **Verb chosen `POST` (not `PATCH`) to match existing admin-tenant action-style slices** (`SuspendTenantEndpoint`, `ActivateTenantEndpoint`, `DeleteTenantEndpoint`). Body: `{ "providerKey": "mailgun" | "ses" }`.
- **Audit-log row is written in the same DB transaction as the flip** (not deferred to outbox) — consistent with existing `SuspendTenantHandler` pattern. New enum values in `AuditAction` — **append-only with explicit integer values** to prevent silent reinterpretation of historical rows when the enum is stored as `int` (see §2.a audit). Existing enum ends at `SettingsUpdated = 10`; new values extend from 11:
  - `TenantProviderChanged = 11` — the flip itself (before + after values, actor admin id, timestamp).
  - `DomainKillSwitchSuspended = 12` — admin or automated kill-switch fires.
  - `DomainCrossTenantRejected = 13` — a tenant tries to claim a domain already verified on another tenant.
  - `MailgunApiKeyRotated = 14` — ops invokes the rotation runbook (see §2.f).
  - `DomainVerificationFailedTerminal = 15` — `DomainVerificationJob` hits the 48h cap and marks `Failed`.
  - Existing values get retrofitted with explicit `= 0` … `= 10` in the same commit so the contract is self-documenting. Any future insertion between members is prohibited by review checklist.
- **Pre-flip guard**: reject the flip unless `SELECT EXISTS (... WHERE tenant_id = X AND provider_key = <target> AND status = 'Verified' AND deleted_at IS NULL)` returns true. Error code `DomainNotVerifiedForProvider`, HTTP 409.
- **Bidirectional**: the same endpoint handles `ses → mailgun` and `mailgun → ses`. Same guard logic. Idempotent — flipping to the same value is a no-op (no audit row, 204).
- **Email-row provider pinning** — two-layer defense with deploy-safe contract evolution:
  1. `SendEmailHandler` sets `Email.ProviderKey` at enqueue, reading `tenant.PreferredEmailProviderKey` once.
  2. `SendEmailMessage` MassTransit contract gains `public string? ProviderKey { get; init; }` — **nullable** and with no default value. MassTransit/JSON treats missing fields as `null`, so messages enqueued under the old schema deserialize cleanly on a new consumer during rolling deploy.
  3. `SendEmailConsumer` resolves the provider from `email.ProviderKey` (the DB row is authoritative — it already re-reads the `Email` by id at `SendEmailConsumer.cs:54-55`). The message-level `ProviderKey` is used as a **soft cross-check only when non-null**. Null (pre-deploy in-flight) → trust DB silently, emit a debug log, do NOT alert. Non-null mismatch → log at `Warning`, honor DB (conservative), emit `sendnex_mailgun_provider_pin_mismatch_total` counter.
  - **Deploy ordering**: rolling deploy is safe on its own, but recommended to pair with a pre-deploy drain on the `eaas-emails-send*` queues (lower to zero in-flight count, then deploy). Not required — the nullable-trust-DB path makes it correctness-safe, just log-noisier for a few minutes during the crossover.
  - Deliberately NOT introducing `SendEmailMessageV2` — MassTransit's JSON serializer handles additive changes natively and versioning doubles the consumer registration surface. Revisit only if we ever need a non-additive change.
- **From-domain ownership check in `MailgunEmailProvider.SendAsync`**: reject (throw domain exception → 403) when the request's `From` address domain ≠ tenant's verified `sending_domains.DomainName` for `provider_key = 'mailgun'`. Exact match, case-insensitive, not suffix. This is the isolation boundary on the shared Flex account.
- **Kill switch + soft-delete** block sends at the same check-point:
  - `kill_switch_suspended_at IS NOT NULL` on the tenant's active mailgun domain → reject.
  - `sending_domains.deleted_at IS NOT NULL` for the domain referenced by the From address → reject with `DomainDeleted`.
- **Domain soft-delete mid-flight rule**: deleting a `SendingDomain` (setting `deleted_at`) is soft. Already-enqueued emails with `provider_key` pinned to that domain's provider will hit the send-time check above and DLQ with `DomainDeleted`. They do NOT silently drop — the consumer logs the reason and MassTransit's existing error queue surfaces the count. Operators can manually re-queue after restoring the domain or re-route to SES by bulk-updating `emails.provider_key` for that tenant (documented runbook deliverable).

#### 2.f Secrets

- **Master API key out of appsettings, into AWS Secrets Manager** at secret id `sendnex/platform/email/mailgun/master-api-key`. Package: `AWSSDK.SecretsManager` (added to `EaaS.Infrastructure.csproj` — reuses existing AWS SDK credential chain).
- **Startup must not hard-fail on Secrets Manager outage** (Independent pass-3 blocker). Registration is a custom `MailgunSecretProvider` that fetches with **three layers of resilience**:
  1. **Primary**: live Secrets Manager fetch on startup + 30-min refresh loop for rotation pickup (5-min in-memory cache for read hot path).
  2. **Last-known-good disk cache** at `/var/lib/sendnex/secrets/mailgun-master.cache` — AES-encrypted with a pod-local KMS-DEK (or an operator-provided `SENDNEX_LKG_KEY` env var, rotated quarterly). On every successful Secrets Manager fetch the value is written through to this cache. On cold-start if Secrets Manager is unreachable, the provider reads the LKG cache and continues with a startup `Warning` log + the `sendnex_mailgun_secret_source{source="lkg_disk"}` gauge set to 1. TTL on the LKG read is 7 days — older than that, refuse to start (prevents indefinitely-stale secrets in a long-dead region).
  3. **Lazy circuit breaker for cold-start when no LKG exists** (first-ever deploy of a region): the `MailgunEmailProvider.SendAsync` path calls a lazy resolver. If the secret is still unavailable at first send, open a 30s circuit breaker, log + metric, and return a `ProviderTemporarilyUnavailable` domain error that the send pipeline translates to DLQ-for-retry — NOT to a send failure observable by the tenant as "delivery failed."
  4. Net effect: a Secrets Manager regional incident degrades rotation (new keys don't propagate until SM comes back) but does NOT take down SendNex. Pods stay up, existing keys keep working, rotation queues.
- **Rotation runbook** (zero-downtime dual-key window):
  1. Mint a new Mailgun API key in the Mailgun UI (Mailgun supports multiple active keys).
  2. Update AWS Secrets Manager secret value; do NOT deploy yet.
  3. Wait **up to 35 min** (5-min cache TTL + 30-min refresh interval) for the hosted refresher to pick up the new value OR trigger ops-only `POST /api/v1/admin/secrets/refresh-mailgun` for immediate invalidation.
  4. Observe traffic for 24h. Old key still valid on Mailgun side.
  5. Revoke the old key in the Mailgun UI.
  6. Write `AuditAction.MailgunApiKeyRotated` on every step.
- **Startup validator — rewritten, provenance-sentinel-based** (Independent pass-3 blocker: the pass-2 rule rejected k8s-Secret-as-env, which is the industry-standard pod-secret pattern). Validator logic:
  1. Read `SENDNEX_SECRET_SOURCE` environment variable. Must be one of: `aws-sm` | `k8s-secret` | `dev-local` | `lkg-fallback`.
  2. If the variable is missing in any non-development environment → throw at startup. Dev environments default to `dev-local` with a warning.
  3. If `aws-sm` → the configuration value must have originated from the `MailgunSecretProvider` (introspect `IConfigurationRoot.Providers`); else throw.
  4. If `k8s-secret` → the configuration value must have originated from `EnvironmentVariablesConfigurationProvider` AND the env var name must start with the documented prefix `SENDNEX_SECRET__` (double-underscore matches .NET's standard env-var-to-section convention). This specifically whitelists the k8s-Secret-as-env-var deploy pattern without whitelisting every env var.
  5. If `lkg-fallback` → the `MailgunSecretProvider` set this at runtime when primary failed and LKG served the value. Validator logs the `lkg_age_seconds` and continues; alerts fire via §2.k.
  6. `dev-local` allows appsettings.Development.json + env vars with a warning log, `IsDevelopment()` only.
  - Unit tests cover all five matrix cells (source × provenance match/mismatch) + the k8s-Secret-as-env happy path.

#### 2.g Input validation

- `domainName` on create: `System.Uri.CheckHostName(value) == UriHostNameType.Dns` + punycode roundtrip (`System.Globalization.IdnMapping`) + length ≤ 253 + no `..`, no CR/LF, no leading/trailing dot.
- Cross-tenant uniqueness on `sending_domains.domain_name` (case-insensitive) — a second tenant claiming a domain already verified elsewhere is rejected outright.
- Admin-endpoint authz: acting admin must have a session whose `tenant_id` (or superadmin role) covers the target tenant. Reuse the existing admin proxy token verifier — do not introduce a new authz scheme.

#### 2.h UI (thin layer)

- Admin tenant detail page: "Switch to Mailgun" toggle + "Switch back to SES" variant.
- Wizard step 1: show Mailgun DNS records (SPF, DKIM CNAME, tracking CNAME, MX).
- Wizard step 2: poll our own `GET /domains/{id}` every 60s, render `mailgun_domain_state` + any `verification_last_error`.
- Toggle is disabled (with tooltip) until at least one verified domain exists for the target provider.

#### 2.i Test matrix (merge-blocking)

Handler layer:
- `UpdateTenantProviderHandler`: happy flip, rejection when no verified target domain, idempotent flip, `AuditLog` written.
- `AddDomainHandler`: routes to the correct `IDomainIdentityService` per provider, persists returned DNS rows.

Adapter layer (HttpMessageHandler fake, mirrors `MailgunClientHttpTests`):
- `MailgunClient.CreateDomainAsync`: asserts `use_automatic_sender_security=yes`, 200 / 400-conflict / 404 / 5xx branches.
- `MailgunClient.VerifyDomainAsync`: 200 / 4xx / 5xx branches.
- `MailgunDomainIdentityService`: returns correctly shaped `DomainIdentityResult` with SPF + DKIM CNAME + tracking CNAME + MX.

Worker:
- `DomainVerificationJob` backoff schedule, failure-then-next-attempt, terminal state after 48h → `VerificationFailed`.
- Single-flight lock: two concurrent attempts on same key → one runs, one waits/skips.

Adversarial:
- Mailgun 5xx on `POST /v4/domains` with dropped response → reconcile via `GET /v4/domains/{name}` (WireMock).
- Provider-flip while `SendEmailHandler` holds an inflight enqueue → delivered via pinned provider, not the new one.
- Tenant A submitting Tenant B's verified domain → 409 cross-tenant-uniqueness.
- Domain-name fuzz: IDN, CRLF injection, `..`, trailing dot, > 253 chars, leading dot, all-numeric → all rejected at validator.
- `MailgunEmailProvider.SendAsync` with `From: evil@other-tenant-domain` → rejected with domain-ownership error.

E2E (Playwright against local stack):
- Wizard happy path: toggle on → DNS rows shown → mock verify success → green tick → toggle enables.
- DNS not propagated: verify returns 400 → retry UI shown → eventual success.
- Provider-flip blocked when no verified domain → toggle disabled with tooltip.

Regression:
- Existing SES `AddDomainHandler` tests green after factory refactor.
- Existing send path tests green with new `Email.ProviderKey` column populated.

#### 2.j Rollout

- [ ] Migrate 1 pilot tenant (internal). Measure 7-day deliverability vs SES.
- [ ] Roll out to 10% → 50% → 100% over 3 weeks. Keep SES adapter registered and selectable until Phase 5 cutover.

**Risk:** Tenant DNS misconfiguration → outage for that tenant. **Mitigation:** Pre-flip guard (2.e) prevents the flip from landing until a verified domain exists. **Rollback:** Flip `preferred_email_provider_key` back to `ses`; Email rows already enqueued under `provider_key='mailgun'` continue on Mailgun until drained (acceptable — deliverability, not correctness, is the risk).

#### 2.k Observability (named metric + log contract)

Metrics (Prometheus, registered on existing meter `EaaS.Api` / new meter `EaaS.Worker.Mailgun`):
- `sendnex_mailgun_verify_duration_seconds` (histogram, labels: `result=success|failure|timeout`) — wall-clock latency of each `DomainVerificationJob` Mailgun call.
- `sendnex_mailgun_verify_failures_total` (counter, labels: `reason=dns_not_propagated|rate_limited|4xx|5xx|timeout`) — bucketed by HTTP-status-class + semantic reason.
- `sendnex_mailgun_verify_terminal_total` (counter, labels: `outcome=verified|failed`) — increments once per domain at terminal state.
- `sendnex_mailgun_singleflight_contention_total` (counter, labels: `resource=create|verify`) — incremented when `SET NX` returns false (a competing attempt is already running).
- `sendnex_mailgun_send_rejected_total` (counter, labels: `reason=killswitch|from_mismatch|domain_deleted|provider_unavailable|tenant_id`) — **consolidated** rejection counter replacing the pass-2 split metrics. Collapses killswitch + from-domain-mismatch + soft-delete + provider-circuit-breaker into one counter with `reason` label. Independent pass-3 noted `domain_deleted` had no metric at all; now covered here. `provider_unavailable` covers the §2.f circuit-breaker rejection.
- `sendnex_mailgun_api_key_refresh_total` (counter, labels: `source=hosted_service|manual_endpoint|lkg_fallback`) — tracks rotation cache refreshes; `lkg_fallback` fires when the disk cache served.
- `sendnex_mailgun_api_key_cache_age_seconds` (gauge) — time since last successful Secrets Manager fetch. A stalled refresh loop shows up here as a continuously-climbing value; alert if > 3600.
- `sendnex_mailgun_secret_source` (gauge, labels: `source=aws_sm|lkg_disk|k8s_secret|dev_local`) — which secret source served the current value. Dashboard panel lights up when LKG is serving so ops can see a silent Secrets Manager outage.
- `sendnex_mailgun_provider_pin_mismatch_total` (counter) — §2.e soft cross-check: message-level `ProviderKey` disagrees with DB row. Non-zero during rolling deploy is expected (old-schema messages in-flight); sustained non-zero outside a deploy window is an invariant violation.

Structured log events (Serilog, at `Information` unless noted):
- `mailgun.domain.create_requested` / `mailgun.domain.create_reconciled` (latter = 4xx-conflict fall-through, see §2.d)
- `mailgun.domain.verify_attempt` with `attemptNumber`, `backoffSeconds`, `state`
- `mailgun.domain.verify_terminal` (`Warning` on failed, `Information` on verified)
- `mailgun.provider.flip_requested` / `mailgun.provider.flip_rejected_guard` (`Warning`)
- `mailgun.provider.flip_completed`
- `mailgun.send.rejected_from_domain_mismatch` (`Warning`)
- `mailgun.send.rejected_killswitch` (`Warning`)
- `mailgun.send.rejected_domain_deleted` (`Warning`) — soft-deleted domain hit at send-time; pairs with DLQ entry
- `mailgun.send.rejected_provider_unavailable` (`Warning`) — Secrets Manager circuit-breaker open; DLQ-for-retry
- `mailgun.apikey.rotated` (`Warning` — intentional so it's conspicuous in log search)
- `mailgun.apikey.refresh_failed` (`Warning`) — primary fetch failed; includes `lkg_age_seconds` on fallback
- `mailgun.secret.lkg_serving` (`Warning`) — LKG disk cache is currently authoritative because Secrets Manager is unreachable

Alert hints for Grafana (not defined here, implementation ticket):
- `sendnex_mailgun_send_rejected_total{reason="from_mismatch"}` rate > 0.1/s per tenant for 5min → page oncall (compromised key / misconfigured integration).
- `sendnex_mailgun_send_rejected_total{reason="domain_deleted"}` any non-zero sustained for 5min → ops investigates; soft-deleted-but-still-enqueued sends are DLQ'd and need triage (see runbook).
- `sendnex_mailgun_verify_failures_total{reason="5xx"}` rate > 1/s for 5min → page oncall (Mailgun outage).
- `sendnex_mailgun_singleflight_contention_total` sustained > 10/s → investigate runaway poller.
- `sendnex_mailgun_api_key_cache_age_seconds` > 3600 → refresh loop dead; page oncall even if nothing else is failing (silent failure class).
- `sendnex_mailgun_secret_source{source="lkg_disk"}` == 1 for > 15 min → Secrets Manager outage, escalate.
- `sendnex_mailgun_provider_pin_mismatch_total` non-zero for > 15 min outside a deploy window → invariant violation; investigate.

### Phase 3 — Inbound Routes Cutover (week 5-6)
- [ ] For each tenant, `POST /v3/routes` (account-global on Flex — no `X-Mailgun-On-Behalf-Of`) with a **tenant-discriminated** expression `match_recipient("^(.+)@inbound.<tenantslug>.sendnex.io$")` and action `forward("https://api.sendnex.io/inbound/mailgun?tenantId=<id>&mailbox=\\1")` + `stop()`. The tenant's slug in the match pattern is what keeps route matches isolated on the shared account.
- [ ] Update tenant DNS: MX -> `mxa.mailgun.org`/`mxb.mailgun.org` (replace SES inbound MX).
- [ ] Implement `POST /inbound/mailgun` endpoint: HMAC-verify signature, parse `multipart/form-data`, extract attachments, dispatch to tenant's configured inbound handler.

**Risk:** Lost inbound emails during MX cutover (TTL gap). **Mitigation:** Pre-stage route creation; lower MX TTL to 300s 48h before cutover; run both MX records in parallel (priority 10 Mailgun, priority 20 SES) for 72h, then remove SES.

### Phase 4 — Webhooks Wiring (week 6)
- [ ] Create account-level webhooks on primary Mailgun account for each event type -> `https://api.sendnex.io/webhooks/mailgun/{eventType}`.
- [ ] Implement webhook receiver: HMAC verify (SHA256 of `timestamp||token` against `WebhookSigningKey`), replay-protection via token cache (Redis, 10 min TTL), timestamp freshness check (<= 5 min drift).
- [ ] Event mapper: Mailgun events -> SendNex canonical `EmailEvent` enum (preserve SES-era enum values so downstream consumers don't break).
- [ ] Backfill: run Events API poll (`GET /v3/{domain}/events`) for any gap between SES shutoff and first Mailgun webhook.

**Risk:** Event gap leaves bounced/complained addresses uncleaned. **Mitigation:** Events API backfill + 24h overlap with SES SNS subscriptions still live. **Rollback:** SNS subscriptions remain dormant but reactivatable.

### Phase 5 — Full Cutover + AWS Teardown (week 7-8)
- [ ] Confirm 100% tenant traffic on Mailgun for 14 consecutive days; all webhooks ingesting; no SES fallback invocations logged.
- [ ] Disable SES `IEmailProvider` registration; keep code behind feature flag for 30 days.
- [ ] Tear down AWS: Configuration Sets, SNS topics, Lambda inbound handlers, SES dedicated IPs, Route53 inbound MX. Archive IAM policies.
- [ ] Update status page, customer docs, pricing FAQ.
- [ ] Post-mortem + runbook update.

**Risk:** Discover latent SES dependency after teardown. **Mitigation:** 30-day code-retention window; IaC archive in git; keep SES-region Route53 zones frozen for 30d. **Rollback:** Re-enable SES provider via feature flag; redeploy IaC from the archived branch.

### Phase 6 — Optional Foundation upgrade per tenant (on-demand, not time-boxed)

**Trigger (per tenant):** first tenant that needs a dedicated IP, isolated sender reputation, or compliance-driven isolation (e.g. HIPAA/GDPR ring-fencing). Upgrade is tenant-by-tenant, not a global cutover.

- [ ] Upgrade the Mailgun account to Foundation ($35/mo floor) or Scale as the tenant's volume/requirements dictate.
- [ ] Register the subaccount-aware `MailgunEmailProvider` variant in DI (the code parked in §4 goes live here). Same `IEmailProvider` interface — factory swap only.
- [ ] Onboarding: `POST /v5/accounts/subaccounts?name=<tenantslug>` -> store `MailgunSubaccountId` on the tenant row -> mint subaccount-scoped API key OR keep master key + set `X-Mailgun-On-Behalf-Of` header in the adapter.
- [ ] Migrate that tenant's sending domain under the subaccount; re-verify DNS if needed. Reissue routes on-behalf-of the subaccount.
- [ ] `IEmailProviderFactory.GetForTenant` returns the subaccount-aware adapter for upgraded tenants; shared-account adapter continues serving everyone else.
- [ ] Dedicated IP request + warm-up plan (Scale tier), if applicable.

**Risk:** Reputation flap during the subaccount cutover for the upgrading tenant. **Mitigation:** Dual-route for 48h (shared + subaccount), bleed sends over, then remove the shared-account route. **Rollback:** Flip factory back to shared-account adapter; subaccount stays dormant.

---

## 9. Open Questions / Follow-ups

1. **EU region:** Do any tenants have GDPR data-residency SLAs? If yes, stand up a parallel Mailgun EU account and region-aware routing from day 1.
2. **Template storage:** SES SendTemplatedEmail templates must be ported to Mailgun Templates API. Write a one-shot migration script.
3. **Suppression list migration:** Export SES bounce/complaint suppressions, import to Mailgun via Suppressions API (`POST /v3/{domain}/bounces`, `/unsubscribes`, `/complaints`) before first send.
4. **AUP review:** Read https://www.mailgun.com/legal/acceptable-use/ end-to-end and get legal sign-off that SendNex's reseller model on Flex (shared account, domain-per-tenant) is explicitly permitted. Re-check clauses on shared-reputation resellers.

(Dedicated-IP / per-tenant IP pool question moved to Phase 6 — not a launch concern on Flex.)
