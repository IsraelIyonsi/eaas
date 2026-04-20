# Phase 2 Mailgun — Dual Architect Review Findings (2026-04-19)

Both architects independently returned **BLOCK**. Per `feedback_dual_architect_gate`, Phase 2 cannot proceed until these are addressed. Revise `tasks/mailgun-migration-plan.md §Phase 2` and `tasks/todo.md B1/B2`, then resubmit for re-review.

## Blockers (union of both reviews — all must be addressed)

### Architecture
1. **Verify polling moves to background worker** (`EaaS.Worker`), exponential schedule 60s→5m→15m→1h cap 48h. Frontend polls our own `GET /domains/{id}` which reads cached DB state. Mailgun is never called from the browser.
2. **Server-side single-flight lock** on (tenantId, domainName) for `POST /v4/domains` and verify polls.
3. **Domain state machine**: `Pending → Verifying → Verified | Failed`. Persisted on `sending_domains`.
4. **`IDomainIdentityProviderFactory`** — mirror of `IEmailProviderFactory`. Split `AddDomainHandler` to route by `tenant.PreferredEmailProviderKey`.
5. **Bidirectional flip** (SES↔Mailgun). Kill-switch column for suspend.

### Guards
6. **Pre-flip guard**: `UpdateTenantProviderHandler` must reject flip unless `≥1 SendingDomain with Provider=target, Status=Verified` exists. Writes `AuditLog` (`AuditAction.TenantProviderChanged`).
7. **From-domain ownership check** in `MailgunEmailProvider.SendAsync` — reject when request `From` domain ≠ tenant's verified `SendingDomain.DomainName` (exact match).
8. **Email-row provider pinning**: `Email.ProviderKey` set at enqueue in `SendEmailHandler`; retries honor pinned value. Prevents duplicate delivery on mid-flight flip.
9. **Reconciliation on 4xx-conflict**: on `POST /v4/domains` returning "domain exists," fall through to `GET /v4/domains/{name}` and reconcile (handles orphan state when response drops).

### Data model — new migration needed
Extend `sending_domains` (NOT a new `TenantDomain` table):
- `provider_key` varchar(32) NOT NULL default 'ses'
- `mailgun_domain_state` varchar (`unverified|active|disabled`)
- `verification_last_error` text
- `kill_switch_suspended_at` timestamptz
- Add `DnsRecordPurpose.TrackingCname`

### Security
10. **Master API key to secret store**. Today `MailgunOptions.ApiKey` binds from appsettings/env. Must sit at `Platform/Email/Mailgun/MasterApiKey` in Key Vault / AWS Secrets Manager. Document zero-downtime rotation (dual-key window).
11. **Domain-name input validation**: `System.Uri.CheckHostName` + punycode roundtrip + IDN + length cap 253. Cross-tenant uniqueness on `sending_domains`. Authz check that acting admin owns target tenant.

### Pre-flight schema audit (before writing the new migration)
12. Run `dotnet ef migrations script --idempotent` against prod. Diff vs. what `scripts/migrate_mailgun_webhooks_dedup.sql` actually wrote. Confirm Designer snapshots match. PR #49 bypassed EF — silent drift risk.

### Test gaps — all must be covered before merge
- Handler: `UpdateTenantProviderHandler` happy + guard-rejection + idempotency + audit log written.
- Adapter: `MailgunClient.CreateDomainAsync` / `VerifyDomainAsync` — wire-level HttpMessageHandler fakes, assert `use_automatic_sender_security=true`, 400/404/409/200 branches.
- Worker: `DomainVerificationJob` backoff schedule, terminal after 48h.
- Concurrency: two threads polling verify simultaneously (single-flight).
- Mailgun 5xx on `POST /v4/domains` with dropped response (WireMock).
- Provider-flip while `SendEmailHandler` in-flight (integration test with hold).
- Tenant A submitting Tenant B's domain.
- Domain-name fuzzing (IDN, `\r\n`, `..`, trailing dot, >253 chars).
- E2E Playwright: wizard happy path + DNS-not-propagated retry + provider-flip blocked when unverified.

## Next actions
1. Update `tasks/mailgun-migration-plan.md` §Phase 2 to reflect the above.
2. Rewrite `tasks/todo.md` B1/B2 with the new work-item list.
3. Run the schema-drift audit (#12) — results shape whether migration is cleanup-then-new or just new.
4. Resubmit to both architects for re-review.
