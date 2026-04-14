# Tranche 2 P0/P1 Scope — Verified 2026-04-14

Source: `REVIEW_GATE_REPORT.md` (dated 2026-04-04).
Verification basis: current code on `dev` (HEAD b77eded1).
Verifier: Staff Engineer, line-by-line read of each referenced file.

## Methodology

For each CRITICAL (C1–C34) and HIGH (H1–H37) item in the review-gate report we:

1. Resolved the referenced file on `dev` (fallback: grep the symbol if the file had moved).
2. Read the relevant lines and determined one of:
   - **FIXED** — current code no longer exhibits the reported issue.
   - **PARTIAL** — the acute problem is mitigated but meaningful residual risk remains.
   - **OPEN** — the issue is still present on `dev` essentially as reported.
3. For OPEN/PARTIAL, captured the current file/line and a one-sentence gap description.

Items skipped / reclassified:
- H22, H23 were marked FIXED: `AppShell` now passes real session data (`userData?.displayName`,
  `userData?.email`) into `Sidebar`/`AppHeader`. The `?? "User"` / `?? "user@example.com"`
  strings are only null-safe display fallbacks for unauthenticated layouts, which the
  middleware redirects away from — not hardcoded production values.
- H1–H3 were marked OPEN (defensive): there is still no explicit `[JsonIgnore]` or
  `[NotMapped]`-in-response guard on `Webhook.Secret`, `Tenant.PasswordHash`,
  `AdminUser.PasswordHash`. Current handlers project into DTOs, so no leak today,
  but the "belt" promised by the audit (attribute-level guarantee) is still missing.

## Still OPEN — CRITICAL (P0)

| ID | File:Line | Issue | What's needed |
|----|-----------|-------|---------------|
| C25 | `k8s/redis/configmap.yaml:16,17` | `requirepass ${REDIS_PASSWORD}` still uses shell-var syntax; comment says "Requires init container to substitute secrets before use" but no init-container template is wired into `k8s/redis/statefulset.yaml`. | Add init container (or envsubst `command:`) to render the ConfigMap into an `emptyDir` before Redis starts; otherwise literal `${REDIS_PASSWORD}` is read by Redis. |

No other CRITICAL items remain open. See "FIXED" list below.

## Still OPEN — HIGH (P1)

| ID | File:Line | Issue | What's needed |
|----|-----------|-------|---------------|
| H1 | `src/EaaS.Domain/Entities/Webhook.cs:11` | `Secret` has no `[JsonIgnore]`; accidental inclusion in a future DTO or `return webhook` would leak HMAC secret. | Add `[JsonIgnore]` + serializer-level guard test. |
| H2 | `src/EaaS.Domain/Entities/Tenant.cs:19` | `PasswordHash` has no `[JsonIgnore]` / guard. | Same as H1. |
| H3 | `src/EaaS.Domain/Entities/AdminUser.cs:15` | `PasswordHash` has no `[JsonIgnore]` / guard. | Same as H1. |
| H4 | `src/EaaS.Api/Features/Emails/ListEmailsHandler.cs:57,64` | User input embedded into ILIKE patterns via `$"%{request.FromEmail}%"` and `$"%{request.ToEmail}%"` — `%` / `_` / `\` inside the input are not escaped, letting callers construct unanchored wildcard searches across the tenant. | Escape `%`, `_`, `\` before interpolating (or use parameterised `EF.Functions.Like` with `ESCAPE`). |
| H5 | `src/EaaS.Api/Features/Admin/Users/CreateAdminUserValidator.cs:20` | Admin password rule is only `MinimumLength(8)` — no uppercase/lowercase/digit/symbol requirement. | Add complexity rule (same pattern as customer register validator). |
| H6 | `dashboard/src/app/api/auth/register/route.ts:67` | `data.tenantId ?? data.userId ?? "user-001"` — still contains the hardcoded fallback ID. | Fail the request if backend returned no userId/tenantId instead of fabricating one. |
| H7 | `dashboard/src/app/api/auth/login/route.ts:128-132` | `catch` block returns 401 "Invalid email or password" for ANY thrown error (network timeout, DNS, 5xx, abort). | Differentiate thrown errors (AbortError/ECONNREFUSED → 502, only 4xx passthrough → 401). |
| H8 | `src/EaaS.Infrastructure/Payments/PayStackPaymentProvider.cs:101-102` | Returned `CurrentPeriodStart = DateTime.UtcNow`, `CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)` — still ignores PayStack's API response. | Parse PayStack subscription response and use the real period dates. |
| H11 | `src/EaaS.Infrastructure/Messaging/WebhookDispatchConsumer.cs:62` | Dispatches to every matching webhook with no `(webhook_id, email_id, event_type)` idempotency check. A MassTransit retry will re-deliver to webhooks that already succeeded. | Persist a `webhook_deliveries` row keyed on (webhook_id, email_id, event_type) + short-circuit when a successful row exists. |
| H15 | `src/EaaS.Domain/Interfaces/EmailServiceModels.cs` | File still lives under `Interfaces/` but contains only DTOs (`DomainIdentityResult`, `DkimToken`, etc.). | Move to `EaaS.Domain/Models/Email/` (or similar) to match standards. |
| H16 | `src/EaaS.Domain/Interfaces/IPaymentProvider.cs:30+` | `CreateCustomerRequest/Result`, `CreateSubscriptionRequest/Result`, etc. (9 DTOs) are still co-located with the interface. | Split DTOs into `EaaS.Domain/Models/Payments/*.cs`. |
| H17 | `src/EaaS.Domain/Entities/Plan.cs` | No comment explaining why `Plan` has no `TenantId` (platform-level); AdminUser has this comment, Plan doesn't. | Add XML summary (copy the AdminUser pattern). |
| H19 | `src/EaaS.Infrastructure/DependencyInjection.cs:142-143` | SES registration still passes `sesSettings.AccessKeyId`, `sesSettings.SecretAccessKey` explicitly when present — bypasses the AWS default credential chain (IAM role on EKS/ECS). | Only override when both are non-empty; otherwise construct the client with no explicit creds so default chain is used. |
| H28 | `k8s/overlays/production/kustomization.yaml:53,61,69,77` | Literal `RELEASE_TAG` placeholders remain in all four image tags (api, worker, webhook-processor, migration). | Parameterise via kustomize image transformer or CI `sed` step before `kubectl apply`. |
| H31 | `tests/EaaS.Api.Tests/Features/Templates/…` | 3 of the 7 originally-flagged validators still lack tests: `UpdateTemplateValidator`, `PreviewTemplateValidator`, `RollbackTemplateValidator`. | Write validator tests for the three. |

## PARTIAL — noted for follow-up

| ID | File:Line | Gap |
|----|-----------|-----|
| C11 | `docker-compose.yml:286` | `GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD:-admin}` — env-driven but the fallback is still literal `admin`, so an unset env var silently reinstates the weak credential. Replace the default with a build-time fail (or a random generated placeholder). |

## FIXED — confirmed closed

CRITICAL: C1, C2, C3, C4, C5, C6, C7, C8, C9, C10, C12, C13, C14, C15, C16, C17, C18, C19, C20, C21, C22, C23, C24, C26, C27, C28, C29, C30, C31, C32, C33, C34.

HIGH: H9, H10, H12, H13, H14, H18, H20, H21, H22, H23, H24, H25, H26, H29, H30, H32, H33, H34, H35, H36, H37.

## Recommended dispatch groups

| Group | IDs | Fix type | Effort | Suggested owner |
|-------|-----|----------|--------|-----------------|
| **G1 — Secret hygiene** | H1, H2, H3, C11 (Partial) | Security hardening: add `[JsonIgnore]` + test + tighten Grafana env default. | S (half day) | Backend engineer |
| **G2 — Customer-input injection** | H4, H5 | Security: ILIKE escaping + admin password complexity. | S | Backend engineer |
| **G3 — Dashboard BFF hardening** | H6, H7 | Frontend API route: remove "user-001" fallback, differentiate network vs auth errors. | S | Frontend engineer |
| **G4 — Payment provider correctness** | H8 | Data integrity: parse real period dates from PayStack. | M (needs PayStack sandbox) | Backend engineer (billing) |
| **G5 — Webhook delivery idempotency** | H11 | Data integrity + DB migration for `webhook_deliveries` dedup key. | M | Backend engineer (messaging) |
| **G6 — Infra / deploy hygiene** | C25, H19, H28 | Infrastructure: Redis ConfigMap init container, SES default-chain creds, kustomize image tags. | M | DevOps |
| **G7 — Code-layout standards** | H15, H16, H17 | Quality: move DTOs out of `Interfaces/`, add Plan xmldoc. | S | Any backend engineer |
| **G8 — Template validator tests** | H31 | Tests: 3 template validator test files. | S | QA engineer |

Total remaining P0/P1: **1 OPEN CRITICAL**, **14 OPEN HIGH**, **1 PARTIAL**.

Top-5 highest-priority OPEN items (severity × blast radius):

1. **C25** — `k8s/redis/configmap.yaml:16` — production Redis cannot start with an authenticated master without the init-container substitution. Deploy blocker.
2. **H4** — `ListEmailsHandler.cs:57` — ILIKE wildcard injection allows a tenant to exfiltrate rows they wouldn't normally paginate to.
3. **H11** — `WebhookDispatchConsumer.cs:62` — missing per-delivery idempotency causes duplicate webhook hits to customer endpoints on retry (merchant trust issue).
4. **H7** — `dashboard/src/app/api/auth/login/route.ts:128` — backend outage masquerades as "invalid credentials", blinding operators to a real incident.
5. **H6** — `dashboard/src/app/api/auth/register/route.ts:67` — hardcoded `"user-001"` userId silently corrupts sessions if the backend response shape changes.
