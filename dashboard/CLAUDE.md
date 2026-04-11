@AGENTS.md

# Dashboard Development Rules

Standards are split into focused files in `../standards/`. Read ONLY the relevant file for your current task:
- Backend: `backend-structure.md`, `backend-cqrs.md`, `backend-services.md`, `backend-testing.md`, `backend-migrations.md`
- Frontend: `frontend-architecture.md`, `frontend-components.md`, `frontend-testing.md`
- Infra: `infrastructure.md`
- Review: `review-checklist.md`

## Quick Reference (do NOT duplicate — read the standards file for details)

- Repository pattern → React Query hooks → Constants for everything
- `extractItems()` for list data, `safeConfigLookup()` for status display
- `"use client"` on pages with hooks
- Pages are thin: hooks + shared components + layout
- Tailwind with CSS variables, NO hardcoded hex colors
- Playwright E2E: `getByRole`/`getByText`/`getByLabel`, NOT CSS selectors
- Every new page needs a test in `e2e/`
