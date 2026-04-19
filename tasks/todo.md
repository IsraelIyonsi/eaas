# SendNex — Active Plan (2026-04-19)

Two parallel tracks. **Track A runs first** (small, unblocks prod). **Track B starts after A is merged** (big, needs architect gate).

---

## Track A — UAT sweep2 fallout

Screenshots in `uat/sweep2/` triaged:
- `06-delete-502.png` — **bug**: template delete → 502
- `06-templates-create-dialog.png` — **bug**: dialog header cut off, submit button appears disabled
- `07-emails-empty.png` — ✅ happy path, no action
- `08-ssrf-block-400.png` — ✅ SSRF protection working, no action
- `08-webhooks-deleted.png` — ✅ happy path, no action

### A1. Template delete 502 (Task #1)
- [ ] Repro locally, capture backend stack trace
- [ ] Add failing test (TDD red)
- [ ] Fix root cause — no try/catch swallowing
- [ ] Staff review → Principal sign-off → merge to `dev`

### A2. Template create dialog (Task #2)
- [ ] Inspect `dashboard/components/templates/create-dialog.*` scroll/sticky header
- [ ] Verify submit-button enable rule matches filled state
- [ ] Playwright E2E covering full create lifecycle (feedback_thorough_e2e_testing)
- [ ] Staff review → Principal sign-off → merge

---

## Track B — Mailgun Phase 2 (tenant feature flag + domain verify wizard)

Reference: `tasks/mailgun-migration-plan.md` §Phase 2 (week 3-4, from the 6-phase plan).

Status of prior phases: Phase 0 (PR #24) ✅, Phase 1 (PR #27) ✅.

### B0. Dual architect gate (Task #3) — **blocks everything below**
- [ ] Senior Architect reviews scope + API surface + tenancy model
- [ ] Independent Architect reviews same in parallel
- [ ] Both must APPROVE (feedback_dual_architect_gate)
- [ ] If either BLOCKS → halt and revise

### B1. Backend (Task #4 — blocked by B0 pass-3 approval)

**Revised 2x after dual-architect review. See `mailgun-migration-plan.md §Phase 2` and `phase2-architect-findings.md`.**

Pre-flight (HARD GATE — must land before any §2.b+ work):
- [x] Schema-drift audit: PR #49 hotfix SQL matches EF migrations byte-for-byte (verified 2026-04-19).
- [x] TargetFramework unified on `net10.0` via `Directory.Build.props` (verified 2026-04-20). Run `git clean -fdx` on obj/bin before CI to eliminate stale `net8.0` artifacts.
- [ ] **`AuditAction` EF-mapping audit**: inspect `src/EaaS.Infrastructure/Persistence/Configurations/` for how `AuditLog.Action` is stored. If `int` → append-only + explicit ordinals rule (§2.e). If string-mapped → safe to reorder. Decide before adding new enum values.
- [ ] **Prerequisite commit** wiring `dotnet ef database update --idempotent` into `deploy.sh`. Ship and validate on a no-op deploy before the Phase 2 migration goes anywhere near prod.
- [ ] Add `dotnet-ef` to the build/deploy container image (Dockerfile or CI step).
- [ ] Add `AWSSDK.SecretsManager` to `EaaS.Infrastructure.csproj`.

Data model:
- [ ] New EF migration `AddSendingDomainsProviderColumns`: `provider_key` (NOT NULL default 'ses'), `mailgun_domain_state`, `verification_last_error`, `kill_switch_suspended_at`.
- [ ] `DnsRecordPurpose.TrackingCname` enum value.
- [ ] `DomainStatus.Verifying` enum value — canonical FSM (PendingVerification → Verifying → Verified|Failed|Suspended).

Abstractions:
- [ ] `IDomainIdentityProviderFactory` in `EaaS.Domain/Providers/`.
- [ ] `MailgunDomainIdentityService : IDomainIdentityService` — SPF + DKIM CNAME + tracking CNAME + MX row shape.
- [ ] `AddDomainHandler` refactor: route by `tenant.PreferredEmailProviderKey` via factory.
- [ ] `MailgunClient.CreateDomainAsync` (POST /v4/domains, `use_automatic_sender_security=yes`).
- [ ] `MailgunClient.VerifyDomainAsync` (PUT /v4/domains/{name}/verify).

Verification pipeline:
- [ ] `DomainVerificationJob` in `EaaS.Worker` (sibling of `ScheduledEmailJob`): 60s → 5m → 15m → 1h backoff, cap 48h. Terminal → `Failed` + `AuditAction.DomainVerificationFailedTerminal`.
- [ ] Global concurrency cap via `SemaphoreSlim` (default 10, config: `MailgunOptions.MaxVerifyConcurrency`). Release in `finally` on cancellation so permits don't leak.
- [ ] Honor HTTP `Retry-After` on 429 + 503: next attempt at `max(Retry-After, next backoff step)`.
- [ ] Every `await` honors `stoppingToken`; per-domain delays via `Task.Delay(backoff, stoppingToken)` so shutdown collapses pending waiters cleanly.
- [ ] Redis single-flight lock on `(tenantId, domainName)` — `SET NX PX` with 30s TTL + 10s watchdog renewal for `POST /v4/domains` + verify attempts.
- [ ] Watchdog renewal failure → CTS cancels → in-flight Mailgun call aborts cleanly (lock loss is not fenced; §2.d reconcile covers the 4xx-conflict case).
- [ ] 4xx-conflict reconcile path: fall through to `GET /v4/domains/{name}`.
- [ ] Frontend-facing `GET /api/v1/domains/{id}` reflects `Status`, `mailgun_domain_state`, `verification_last_error`.

Guards:
- [ ] `POST /api/v1/admin/tenants/{id}/provider` (bidirectional ses↔mailgun; verb is POST action-style to match existing admin slices). `AuditAction.TenantProviderChanged`.
- [ ] Audit-log write in same DB transaction as flip (pattern: `SuspendTenantHandler`).
- [ ] Pre-flip guard: reject (409 `DomainNotVerifiedForProvider`) unless ≥1 `Verified` + non-deleted domain for target provider.
- [ ] Extend `AuditAction` enum append-only with explicit ordinals: retrofit existing `= 0`..`= 10`, add `TenantProviderChanged = 11`, `DomainKillSwitchSuspended = 12`, `DomainCrossTenantRejected = 13`, `MailgunApiKeyRotated = 14`, `DomainVerificationFailedTerminal = 15`.
- [ ] Add `ProviderKey` field to `Email` entity (already there per `Email.cs:32`) AND to `SendEmailMessage` MassTransit contract as `public string? ProviderKey { get; init; }` — **nullable** for rolling-deploy safety.
- [ ] `SendEmailHandler` pins `ProviderKey` at enqueue on both the DB row and the message.
- [ ] `SendEmailConsumer` resolves provider from `email.ProviderKey` (DB row authoritative, already re-read at `SendEmailConsumer.cs:54-55`). Soft cross-check against message-level `ProviderKey` **only when non-null**: null → trust DB silently (debug log); non-null mismatch → Warning log + honor DB + `sendnex_mailgun_provider_pin_mismatch_total` counter.
- [ ] From-domain ownership check in `MailgunEmailProvider.SendAsync` (case-insensitive exact match to tenant's `Verified` + non-deleted `sending_domains.DomainName`). 403 on reject.
- [ ] Kill-switch check at send-time (`kill_switch_suspended_at IS NOT NULL` → reject).
- [ ] Soft-delete handling: `sending_domains.deleted_at IS NOT NULL` at send-time → reject with `DomainDeleted`; already-enqueued sends DLQ with reason (do NOT silently drop).
- [ ] **Soft-delete triage runbook**: documented procedure for listing DLQ'd sends by `reason=domain_deleted`, triaging (restore domain? re-route to SES via bulk `UPDATE emails SET provider_key='ses' WHERE ...`?), re-queueing via MassTransit's fault consumer. Ships as `docs/runbooks/mailgun-domain-deleted-triage.md` alongside B1.

Secrets (AWS Secrets Manager — decided, pass-3 resilience added):
- [ ] Add `AWSSDK.SecretsManager` package. Secret id: `sendnex/platform/email/mailgun/master-api-key`.
- [ ] `MailgunSecretProvider` with three-layer resilience (§2.f):
  - Primary: live SM fetch + 30-min refresh loop + 5-min in-memory cache.
  - LKG: AES-encrypted disk cache at `/var/lib/sendnex/secrets/mailgun-master.cache`, write-through on every successful fetch, 7-day max-age on cold-start fallback. KMS-DEK or operator `SENDNEX_LKG_KEY` env var.
  - Lazy circuit breaker: on cold-start with no LKG, `MailgunEmailProvider.SendAsync` opens a 30s circuit and DLQ's for retry rather than failing the send.
- [ ] Ops-only endpoint `POST /api/v1/admin/secrets/refresh-mailgun` for manual invalidation.
- [ ] Startup validator (REWRITTEN AGAIN — pass-3 rule: provenance-sentinel, supports k8s-Secret-as-env): `SENDNEX_SECRET_SOURCE` env var ∈ `{aws-sm, k8s-secret, dev-local, lkg-fallback}` required outside Development. `k8s-secret` requires env var name prefix `SENDNEX_SECRET__`. Unit-test all 5 provenance cases plus mismatch cases.
- [ ] Rotation runbook: issue new key → update Secrets Manager → wait **up to 35min** OR hit refresh endpoint → observe 24h → revoke old → `AuditAction.MailgunApiKeyRotated` at each step.

Input validation:
- [ ] Domain-name validator (`Uri.CheckHostName`, punycode roundtrip, ≤253 chars, no CRLF/`..`/leading-trailing dot).
- [ ] Cross-tenant uniqueness on `sending_domains.domain_name` (ci).
- [ ] Admin-endpoint authz — reuse existing admin proxy-token verifier.

Observability (§2.k contract — consolidated in pass 3):
- [ ] Register metrics: `sendnex_mailgun_verify_duration_seconds`, `sendnex_mailgun_verify_failures_total`, `sendnex_mailgun_verify_terminal_total`, `sendnex_mailgun_singleflight_contention_total`, `sendnex_mailgun_send_rejected_total` (labeled `reason` covering `killswitch|from_mismatch|domain_deleted|provider_unavailable`), `sendnex_mailgun_api_key_refresh_total`, `sendnex_mailgun_api_key_cache_age_seconds`, `sendnex_mailgun_secret_source`, `sendnex_mailgun_provider_pin_mismatch_total`.
- [ ] Emit named log events: the 11 events listed in §2.k (added `rejected_domain_deleted`, `rejected_provider_unavailable`, `apikey.refresh_failed`, `secret.lkg_serving`).
- [ ] Grafana alert hints documented in §2.k (7 alerts — implementation ticket not in B1).

Tests (all must be green before B2 starts):
- [ ] Handler tests: `UpdateTenantProviderHandler` (happy, guard-reject, idempotent, audit-in-same-tx). `AddDomainHandler` (routes by provider).
- [ ] Adapter tests: `MailgunClient.CreateDomainAsync` / `VerifyDomainAsync` (form fields, 200/400-conflict/404/429-with-Retry-After/5xx). `MailgunDomainIdentityService` DNS row shape.
- [ ] Worker tests: `DomainVerificationJob` backoff + terminal + 429 Retry-After honor + concurrency cap respected.
- [ ] Concurrency: single-flight lock with two threads; watchdog renewal.
- [ ] Config-source validator: four provider types (Json/Env/CommandLine/SecretsManager) with + without `IsDevelopment`.
- [ ] Adversarial: Mailgun 5xx dropped-response reconcile (WireMock); provider-flip mid-flight (Email.ProviderKey pinning holds); Tenant A submitting Tenant B's domain → `DomainCrossTenantRejected` audit; domain-name fuzz (IDN, CRLF, dots, >253); From-address mismatch → 403 + metric.
- [ ] Soft-delete: delete a verified domain mid-flight → enqueued sends DLQ with `DomainDeleted`, metric increments.
- [ ] Secrets rotation: refresh endpoint invalidates cache, next send uses new key; `MailgunApiKeyRotated` audit row.
- [ ] Regression: existing SES + send tests stay green.

### B2. Frontend wizard (Task #5 — blocked by B1)

- [ ] Admin tenant detail page: bidirectional "Switch to Mailgun" / "Switch back to SES" toggle. Disabled (with tooltip) when target provider has no verified domain.
- [ ] Wizard step 1: render DNS records (SPF, DKIM CNAME, tracking CNAME, MX) with copy-to-clipboard.
- [ ] Wizard step 2: poll **our** `GET /api/v1/domains/{id}` every 60s (NOT Mailgun). Render `mailgun_domain_state` + `verification_last_error`.
- [ ] Match hi-fi tokens exactly (feedback_review_design_compliance).
- [ ] Playwright E2E: wizard happy path; DNS-not-propagated → retry; flip-blocked-when-unverified → toggle disabled with tooltip.
- [ ] No Blazor — Next.js + shadcn/ui + Tailwind (feedback_no_blazor).

### B3. Review chain (Task #6 — blocked by B2)
- [ ] Staff reviews code
- [ ] Principal final sign-off (feedback_never_skip_reviews)
- [ ] Merge to `dev`, update PROJECT_STATUS.md + SPRINT_PLAN.md (feedback_update_board)

---

## Checkpoint

I'll pause here for approval before touching code. Confirm:
1. Track A first (quick wins), then Track B (architect gate, then implementation)?
2. Or run B0 architect review **in parallel** with Track A coding, so B1 can start the moment A merges?

## Review
(filled in after work completes)
