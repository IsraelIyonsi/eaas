# EaaS Sprint Plan — Remaining Work

## Sprint Process (9-Step Organogram)
1. **Architect** — designs WHAT (scope, spec, acceptance criteria)
2. **Staff Engineer** — reviews WHAT → APPROVE/BLOCK
3. **Developer** — plans HOW (implementation plan, code patterns)
4. **Architect** — reviews HOW → APPROVE/BLOCK
5. **Developer** — writes failing tests (RED)
6. **Developer** — writes code to pass tests (GREEN)
7. **Staff Engineer** — reviews code → APPROVE/BLOCK
8. **Principal Engineer** — final sign-off → APPROVE/BLOCK
9. **DevOps** — deploys

---

## Sprint 5: Admin Dashboard + RBAC (2 weeks, 45pts)

### Stories
| # | Story | Points | Priority |
|---|-------|--------|----------|
| 1 | Admin user role + permission system (RBAC) | 5 | P0 |
| 2 | Admin API endpoints (tenant CRUD, system health, audit logs) | 8 | P0 |
| 3 | Admin Dashboard — Overview (system stats, tenant list, health) | 5 | P0 |
| 4 | Admin Dashboard — Tenant Management (list, detail, suspend, delete) | 8 | P0 |
| 5 | Admin Dashboard — System Health (service status, queue depth, latency) | 5 | P1 |
| 6 | Admin Dashboard — Cross-Tenant Analytics | 5 | P1 |
| 7 | Admin Dashboard — Audit Logs (filterable, searchable) | 5 | P1 |
| 8 | Admin Dashboard — Billing & Plans (plan tiers, tenant billing) | 3 | P2 |
| 9 | Admin Dashboard — Settings (feature flags, rate limits, email provider) | 3 | P2 |
| 10 | Admin E2E tests (Playwright) | 3 | P0 |

### Deliverables
- Backend: Admin API (10+ endpoints), RBAC middleware, admin user seed
- Frontend: New admin Next.js app OR admin section in existing dashboard
- Tests: Unit tests for admin handlers, E2E for admin pages
- Design reference: `design/d6-hifi-screens/admin-dashboard.html` + `hifi-admin-panel.html`

---

## Sprint 6: Observability + Production Hardening (1 week, 25pts)

### Stories
| # | Story | Points | Priority |
|---|-------|--------|----------|
| 1 | Grafana + Prometheus + Loki stack (IN PROGRESS) | 5 | P0 |
| 2 | Structured logging audit (IN PROGRESS) | 3 | P0 |
| 3 | Custom business metrics (email throughput, webhook success rate) | 3 | P0 |
| 4 | Grafana LLM plugin for log interpretation (Option A) | 5 | P1 |
| 5 | API rate limiting at edge (nginx + Redis) | 3 | P1 |
| 6 | Load testing scripts (k6 or artillery) | 3 | P1 |
| 7 | Pixel-perfect UI polish (match hi-fi screens exactly) | 5 | P1 |
| 8 | Frontend component unit tests (Vitest) | 3 | P2 |

### Deliverables
- Grafana dashboards with email throughput, latency, error rate panels
- Loki log aggregation with structured queries
- Load test proving 140 emails/sec sustained throughput
- UI matching hi-fi wireframes

---

## Sprint 7: Advanced Features (2 weeks, 35pts)

### Stories
| # | Story | Points | Priority |
|---|-------|--------|----------|
| 1 | Email scheduling (send at future time) | 5 | P1 |
| 2 | Email resending (retry failed emails from dashboard) | 3 | P1 |
| 3 | Template versioning (view history, rollback) | 5 | P1 |
| 4 | Advanced analytics (funnel analysis, cohort, heatmaps) | 8 | P1 |
| 5 | Inbound email forward action (re-send via SES) | 5 | P1 |
| 6 | Webhook replay (resend failed webhooks from dashboard) | 3 | P1 |
| 7 | Multi-language email template support | 3 | P2 |
| 8 | Custom LLM RAG pipeline for log analysis (Option B) | 8 | P2 |

---

## Sprint 8: Production Launch Prep (1 week, 15pts)

### Stories
| # | Story | Points | Priority |
|---|-------|--------|----------|
| 1 | AWS SES production access + warm-up plan | 3 | P0 |
| 2 | S3 lifecycle policies for data retention | 2 | P0 |
| 3 | Backup strategy validation (restore test) | 2 | P0 |
| 4 | Security penetration testing | 3 | P0 |
| 5 | API documentation (Scalar) polish | 2 | P1 |
| 6 | Customer onboarding flow (first email in 5 min) | 3 | P1 |

---

## Execution Priority

```
Sprint 5 (Admin Dashboard) ──→ Sprint 6 (Observability) ──→ Sprint 7 (Features) ──→ Sprint 8 (Launch)
    NOW                           Parallel with S5              After S5+S6                 Final
```

Sprint 6 stories #1-2 are already in progress (Grafana + structured logging agents running now).
Sprint 5 is the critical path — admin dashboard is the biggest gap.

---

## Gate Review Schedule

Each sprint follows all 4 gates:
- **Gate 1** (Staff reviews Architect spec) — before any coding starts
- **Gate 2** (Architect reviews Developer plan) — before coding starts
- **Gate 3** (Staff reviews code) — after coding, before merge
- **Gate 4** (Principal sign-off) — before deploy
