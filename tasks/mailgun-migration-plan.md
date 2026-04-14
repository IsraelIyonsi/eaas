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

### Phase 2 — Tenant Feature Flag (week 3-4)
- [ ] Admin UI: per-tenant toggle "Switch to Mailgun" with domain verification wizard.
- [ ] Onboarding wizard (Flex path — no subaccount create): `POST /v4/domains` (with `use_automatic_sender_security=true`) under the master key -> render DNS records to copy -> poll `PUT /v4/domains/{name}/verify` every 60s -> green check on success. The subaccount create step is skipped in Phase 1–5 and only invoked in Phase 6 for upgraded tenants.
- [ ] Migrate 1 pilot tenant (internal/friendly). Measure deliverability for 7 days against SES baseline.
- [ ] Roll out to 10% then 50% then 100% of tenants over 3 weeks.

**Risk:** Tenant DNS misconfiguration -> outage for that tenant. **Mitigation:** Only flip tenant to Mailgun after `verify` returns 200; keep SES fallback active until 7-day stability period elapses. **Rollback:** Toggle tenant back to SES; DNS records (CNAME tracking, MX) can stay — SES won't use them.

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
