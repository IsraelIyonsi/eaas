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

### B1. Backend (Task #4 — blocked by B0)
- [ ] Extend `SendNex.Mailgun` client:
  - `POST /v4/domains` with `use_automatic_sender_security=true`
  - `PUT /v4/domains/{name}/verify`
- [ ] Admin endpoint: `POST /admin/tenants/{id}/provider` → flip `PreferredProvider`
- [ ] DB migration if any column missing (most shipped via #49 hotfix — verify)
- [ ] TDD: failing handler + validator + client tests first (feedback_tdd_strict)
- [ ] IProvider + factory pattern, no magic strings (feedback_architecture_swappable)

### B2. Frontend wizard (Task #5 — blocked by B1)
- [ ] Admin tenant detail page: "Switch to Mailgun" toggle
- [ ] Wizard step 1: show DNS records to copy (SPF, DKIM CNAME, tracking CNAME, MX)
- [ ] Wizard step 2: poll `PUT /verify` every 60s, show green tick on 200
- [ ] Match hi-fi tokens exactly (feedback_review_design_compliance)
- [ ] Playwright E2E: full wizard happy path + verify failure + retry
- [ ] No Blazor — Next.js + shadcn/ui + Tailwind (feedback_no_blazor)

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
