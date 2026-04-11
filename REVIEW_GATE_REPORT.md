# EaaS Full Review Gate Report

**Date:** 2026-04-04
**Reviewed by:** 6 parallel reviewer agents (fresh context, line-by-line)
**Scope:** 490 backend .cs files, 165 frontend .ts/.tsx files, 61 test files, 83 config/infra files

## Summary

| Severity | Z1 Domain | Z2 Infra | Z3 API | Z4 Frontend | Z5 Tests | Z6 Config | Total |
|----------|-----------|----------|--------|-------------|----------|-----------|-------|
| CRITICAL | 4 | 6 | 5 | 4 | 5 | 10 | **34** |
| HIGH | 6 | 6 | 6 | 6 | 10 | 9 | **37** |
| MEDIUM | 10 | 9 | 7 | 11 | 13 | 12 | 62 |
| LOW | 6 | 5 | 7 | 9 | 6 | 10 | 43 |
| **Total** | 26 | 26 | 25 | 30 | 34 | 41 | **182** |

---

## CRITICAL Issues (34 — must fix before launch)

### Security Vulnerabilities

| # | Zone | File | Issue | Fix |
|---|------|------|-------|-----|
| C1 | Z3-API | `AdminSessionAuthHandler.cs:28` | Admin auth trusts raw `X-Admin-User-Id` header — no token/JWT validation. Any attacker can impersonate admin by guessing GUID | Implement signed session token validation |
| C2 | Z3-API | `CreateApiKeyEndpoint.cs:8` | IDOR: TenantId accepted from request body, not auth claims. Any user can create API keys for ANY tenant | Extract TenantId from claims, remove from request body |
| C3 | Z4-FE | `emails/[id]/page.tsx:163` | XSS via `dangerouslySetInnerHTML` rendering user-supplied email HTML | Use sandboxed iframe like other pages |
| C4 | Z4-FE | `inbound/emails/[id]/page.tsx:242` | `sandbox="allow-same-origin"` on untrusted email iframe — defeats sandboxing | Change to `sandbox=""` |
| C5 | Z4-FE | `middleware.ts:34-37` | Middleware accepts ANY well-formatted token when SESSION_SECRET unset | Reject all tokens when secret unavailable |
| C6 | Z4-FE | `lib/auth/session.ts:5` | Random session secret per process — sessions invalid across restarts/instances | Require SESSION_SECRET env var, fail at startup if missing |
| C7 | Z2-Infra | `FlutterwavePaymentProvider.cs:179` | Webhook verification uses timing-unsafe `string.Equals` instead of `CryptographicOperations.FixedTimeEquals` | Use constant-time comparison |
| C8 | Z2-Infra | `SnsInboundHandler.cs:59` | SNS subscription confirmation sent without validating SigningCertUrl — SSRF risk | Add `IsValidSigningCertUrl()` check |
| C9 | Z6-Config | `scripts/integration_test.sh:3` | Hardcoded API key committed to git | Read from env var |
| C10 | Z6-Config | `k8s/monitoring/grafana-deployment.yaml:40` | Hardcoded Grafana admin password `"changeme"` | Use secretKeyRef |
| C11 | Z6-Config | `docker-compose.yml:255` | Hardcoded Grafana admin credentials `admin` | Use env var |

### Data Integrity / Logic Bugs

| # | Zone | File | Issue | Fix |
|---|------|------|-------|-----|
| C12 | Z1-Domain | `Invoice.cs:13` | `Status` is magic string `"pending"` instead of enum | Create `InvoiceStatus` enum with dual registration |
| C13 | Z1-Domain | `DnsRecord.cs:9` | `RecordType` is `string` but `DnsRecordType` enum exists unused | Use the enum or delete it |
| C14 | Z1-Domain | `DnsRecord.cs` | Missing `CreatedAt` property (required by standards) | Add `CreatedAt` |
| C15 | Z1-Domain | `SuppressionEntry.cs` | Missing `CreatedAt` property | Add `CreatedAt` |
| C16 | Z2-Infra | `WebhookConfiguration.cs:40` | `WebhookStatus` uses string conversion, not native PG enum — missing dual registration | Register in AppDbContext + DependencyInjection |
| C17 | Z2-Infra | `DnsRecordType` enum | Missing from both AppDbContext and DependencyInjection dual registration | Add to both |
| C18 | Z3-API | `ProcessPaymentWebhookHandler.cs:28` | `Enum.Parse` crashes on invalid provider (anonymous endpoint) | Use `Enum.TryParse` |
| C19 | Z3-API | `ScheduleEmailHandler.cs:48` | Scheduled email has empty FromEmail and Guid.Empty ApiKeyId — will fail at send | Require From field, validate domain |
| C20 | Z3-API | `ListDomainsHandler.cs:18` | Soft-deleted domains returned in list (no `DeletedAt == null` filter) | Add filter |

### Resource Leaks / Stability

| # | Zone | File | Issue | Fix |
|---|------|------|-------|-----|
| C21 | Z2-Infra | `SendEmailConsumer.cs:268` | `File.OpenRead` FileStreams never disposed — file handle exhaustion under load | Copy to MemoryStream or track for disposal |
| C22 | Z2-Infra | `StripePaymentProvider.cs:304` | Sync `.GetAwaiter().GetResult()` blocks thread pool — thread starvation | Make `EnsureSuccess` async |
| C23 | Z2-Infra | `PayPalPaymentProvider.cs:344` | Same sync blocking issue | Same fix |

### Infrastructure / Config

| # | Zone | File | Issue | Fix |
|---|------|------|-------|-----|
| C24 | Z6-Config | `infrastructure/redis/sentinel.conf:16` | Unresolved `${REDIS_PASSWORD}` — Redis conf doesn't support shell vars | Use entrypoint envsubst |
| C25 | Z6-Config | `k8s/redis/configmap.yaml:13` | Same unresolved `${REDIS_PASSWORD}` in K8s ConfigMap | Use init container |
| C26 | Z6-Config | `infrastructure/pgbouncer/pgbouncer.ini:8` | PgBouncer using `md5` auth (weak) — K8s uses `scram-sha-256` | Change to scram-sha-256 |
| C27 | Z6-Config | `infrastructure/pgbouncer/userlist.txt:1` | Wrong username `eaas` (should be `eaas_app`) | Fix username |
| C28 | Z6-Config | `docker/nginx/nginx.conf:85` | Dashboard proxied to port 8082 (should be 3000) | Fix port |
| C29 | Z6-Config | `docker/nginx/nginx.conf:86` | References Blazor despite Next.js dashboard | Fix comment |

### Test Coverage Gaps (blocking launch)

| # | Zone | Issue | Fix |
|---|------|-------|-----|
| C30 | Z5-Tests | `ApiKeyAuthHandler` and `AdminSessionAuthHandler` — ZERO test coverage | Write auth handler tests |
| C31 | Z5-Tests | `SendBatchValidator` — no tests (spam risk) | Write validator tests |
| C32 | Z5-Tests | `CreateSubscriptionValidator` — no tests (billing risk) | Write validator tests |
| C33 | Z5-Tests | `ScheduleEmailHandler/Validator` — no tests | Write handler + validator tests |
| C34 | Z5-Tests | `CreateTenantValidator` — no tests (admin privilege) | Write validator tests |

---

## HIGH Issues (37 — fix within 24 hours)

### Security

| # | Zone | File | Issue |
|---|------|------|-------|
| H1 | Z1 | `Webhook.cs:11` | `Secret` stored as nullable string — ensure never serialized |
| H2 | Z1 | `Tenant.cs:16` | `PasswordHash` on Tenant — ensure never in API responses |
| H3 | Z1 | `AdminUser.cs:16` | `PasswordHash` on AdminUser — same concern |
| H4 | Z3 | `ListEmailsHandler.cs:55` | ILIKE wildcard injection — user input not escaped for `%`/`_` |
| H5 | Z3 | `CreateAdminUserValidator.cs:18` | Admin password: no complexity requirements beyond 8 chars |
| H6 | Z4 | `api/auth/register/route.ts:43` | Fallback userId hardcoded to `"user-001"` |
| H7 | Z4 | `api/auth/login/route.ts:66` | Auth errors swallowed — backend-down returns 401 not 502 |

### Data Integrity

| # | Zone | File | Issue |
|---|------|------|-------|
| H8 | Z2 | `PayStackPaymentProvider.cs:101` | Subscription period dates hardcoded to UtcNow+30d instead of parsing API response |
| H9 | Z2 | `SendEmailConsumer.cs:57` | Idempotency guard logic incorrect — may swallow retries for Failed emails |
| H10 | Z2 | `BatchEmailConsumer.cs:46` | Batch swallows ALL exceptions — if entire batch fails, message ACK'd and emails lost |
| H11 | Z2 | `WebhookDispatchConsumer.cs:113` | No idempotency per webhook+email — retries re-send to already-succeeded webhooks |
| H12 | Z3 | `SendBatchHandler.cs:122` | Messages published to MassTransit BEFORE SaveChangesAsync — phantom emails on DB failure |
| H13 | Z3 | `SendBatchHandler.cs:40` | Batch rate limit consumes 1 slot regardless of batch size — bypass vector |
| H14 | Z3 | `CancelSubscriptionHandler.cs:21` | Query doesn't filter by active status — may cancel wrong subscription |

### Consistency / Standards

| # | Zone | File | Issue |
|---|------|------|-------|
| H15 | Z1 | `EmailServiceModels.cs` | Misplaced in Interfaces/ folder — these are DTOs |
| H16 | Z1 | `IPaymentProvider.cs:30` | 9 DTOs co-located with interface — should be separate file |
| H17 | Z1 | `Plan.cs` | Missing comment explaining why no TenantId |
| H18 | Z2 | `PlanConfiguration.cs:83` | Missing `HasDatabaseName()` on indexes (3 config files) |
| H19 | Z2 | `DependencyInjection.cs:126` | SES credentials bypass AWS default credential chain |

### Frontend

| # | Zone | File | Issue |
|---|------|------|-------|
| H20 | Z4 | `app-shell.tsx:16` | Missing legal page bypasses (/dpa, /sub-processors, /acceptable-use) |
| H21 | Z4 | `app-shell.tsx:28` | Direct `window` access without SSR guard |
| H22 | Z4 | `sidebar.tsx:192` | Hardcoded "User" / "user@example.com" instead of session data |
| H23 | Z4 | `app-header.tsx:107` | Same hardcoded user display |

### Infrastructure / Config

| # | Zone | File | Issue |
|---|------|------|-------|
| H24 | Z6 | `k8s/postgres/statefulset.yaml:27` | Missing `runAsNonRoot: true` |
| H25 | Z6 | `k8s/worker/deployment.yaml:31` | Prometheus annotation on port 9090 but Worker has no HTTP listener |
| H26 | Z6 | `docker-compose.yml:202` | Dashboard API key hardcoded in base compose |
| H27 | Z6 | `prometheus.yml` | Only scrapes single-instance targets — prod has multiple |
| H28 | Z6 | `k8s/overlays/production/kustomization.yaml:53` | Image tags are literal `RELEASE_TAG` placeholder |
| H29 | Z6 | `migrate.sql:237` | ALTER TABLE outside transaction block |
| H30 | Z6 | `k8s/redis/statefulset.yaml:93` | Redis probes skip authentication — will fail with NOAUTH |

### Test Gaps (HIGH)

| # | Zone | Issue |
|---|------|-------|
| H31 | Z5 | 6 validators have ZERO tests (UpdateInboundRule, UpdateTemplate, PreviewTemplate, AddSuppression, CreateWebhook, UpdateWebhook, RollbackTemplate) |
| H32 | Z5 | SendEmailHandler: rate limiter rejection path untested |
| H33 | Z5 | SendEmailHandler: SubscriptionLimitService rejection untested |
| H34 | Z5 | CreateApiKeyHandler: CanCreateApiKeyAsync=false untested |
| H35 | Z5 | SendBatchHandler: rate limiter rejection untested |
| H36 | Z5 | MockDbSetFactory: Remove() not tracked — hiding bugs |
| H37 | Z5 | Multiple handlers: SaveChangesAsync never verified in tests |

---

## Fix Priority Groups

### Group A: Security (C1-C11, H1-H7) — 18 issues
### Group B: Data Integrity (C12-C20, H8-H14) — 16 issues
### Group C: Resource/Stability (C21-C23) — 3 issues
### Group D: Infrastructure (C24-C29, H24-H30) — 13 issues
### Group E: Test Coverage (C30-C34, H31-H37) — 12 issues
### Group F: Code Quality (H15-H23) — 9 issues

---

## Positive Findings (across all zones)

- Clean POCO entity pattern, consistent tenant isolation
- Strong MassTransit configuration with circuit breakers, rate limiters, retry policies
- Payment provider factory pattern with HMAC webhook verification
- Redis fail-closed safety (suppression returns true on error, rate limiter denies on error)
- Structured logging via `[LoggerMessage]` source generators throughout
- Repository -> React Query hooks -> Components pattern consistently followed
- No console.log/debugger statements in frontend
- All auth via httpOnly cookies, no localStorage
- Comprehensive K8s security (seccomp, drop ALL, readOnlyRootFilesystem)
- PostgreSQL tuning is production-grade
- Well-designed exception hierarchy mapping cleanly to HTTP status codes
- Central Package Management preventing version drift
