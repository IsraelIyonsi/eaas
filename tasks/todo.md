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

### B1. Backend (Task #4 — blocked by B0 re-approval)

**Revised after dual-architect block. See `mailgun-migration-plan.md §Phase 2` and `phase2-architect-findings.md`.**

Pre-flight:
- [x] Schema-drift audit: PR #49 hotfix SQL matches EF migrations byte-for-byte (verified 2026-04-19).
- [ ] Deploy-pipeline decision: wire `dotnet ef database update` into `deploy.sh` **or** reproduce Phase 2 migration as raw SQL + `__EFMigrationsHistory` insert (per #49 pattern). Decide before writing migration.

Data model:
- [ ] New EF migration `AddSendingDomainsProviderColumns`: `provider_key` (NOT NULL default 'ses'), `mailgun_domain_state`, `verification_last_error`, `kill_switch_suspended_at`.
- [ ] `DnsRecordPurpose.TrackingCname` enum value.

Abstractions:
- [ ] `IDomainIdentityProviderFactory` in `EaaS.Domain/Providers/`.
- [ ] `MailgunDomainIdentityService : IDomainIdentityService` — SPF + DKIM CNAME + tracking CNAME + MX row shape.
- [ ] `AddDomainHandler` refactor: route by `tenant.PreferredEmailProviderKey` via factory.
- [ ] `MailgunClient.CreateDomainAsync` (POST /v4/domains, `use_automatic_sender_security=yes`).
- [ ] `MailgunClient.VerifyDomainAsync` (PUT /v4/domains/{name}/verify).

Verification pipeline:
- [ ] `DomainVerificationJob` in `EaaS.Worker`: 60s → 5m → 15m → 1h backoff, cap 48h. Terminal → `VerificationFailed`.
- [ ] Redis single-flight lock on `(tenantId, domainName)` for `POST /v4/domains` + verify attempts.
- [ ] 4xx-conflict reconcile path: fall through to `GET /v4/domains/{name}`.
- [ ] Frontend-facing `GET /api/v1/domains/{id}` reflects `mailgun_domain_state` + `verification_last_error`.

Guards:
- [ ] `PATCH /api/v1/admin/tenants/{id}/provider` (bidirectional ses↔mailgun). `AuditAction.TenantProviderChanged`.
- [ ] Pre-flip guard: reject unless ≥1 verified domain for target provider.
- [ ] `Email.ProviderKey` pinning in `SendEmailHandler` at enqueue; retries read the row, not the tenant.
- [ ] From-domain ownership check in `MailgunEmailProvider.SendAsync` (exact match to tenant's verified sending_domain).
- [ ] Kill-switch check at send-time (`kill_switch_suspended_at IS NOT NULL` → reject).

Secrets:
- [ ] Move `MailgunOptions.ApiKey` to secret-store path `Platform/Email/Mailgun/MasterApiKey`. Pick Azure Key Vault vs AWS Secrets Manager — document.
- [ ] Startup validator: reject if key looks like it came from static appsettings.
- [ ] Rotation runbook: dual-key window (issue new → deploy → observe 24h → revoke old).

Input validation:
- [ ] Domain-name validator (`Uri.CheckHostName`, punycode roundtrip, ≤253 chars, no CRLF/`..`/leading-trailing dot).
- [ ] Cross-tenant uniqueness on `sending_domains.domain_name` (ci).
- [ ] Admin-endpoint authz — reuse existing admin proxy-token verifier.

Tests (all must be green before B2 starts):
- [ ] Handler tests: `UpdateTenantProviderHandler` (happy, guard-reject, idempotent, audit). `AddDomainHandler` (routes by provider).
- [ ] Adapter tests: `MailgunClient.CreateDomainAsync` / `VerifyDomainAsync` (form fields, 200/400-conflict/404/5xx). `MailgunDomainIdentityService` DNS row shape.
- [ ] Worker tests: `DomainVerificationJob` backoff + terminal.
- [ ] Concurrency: single-flight lock with two threads.
- [ ] Adversarial: Mailgun 5xx dropped-response reconcile (WireMock); provider-flip mid-flight; Tenant A submitting Tenant B's domain; domain-name fuzz (IDN, CRLF, dots, >253); From-address mismatch rejection.
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
