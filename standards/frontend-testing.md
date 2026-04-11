# Playwright E2E Testing

## Structure

Tests in `dashboard/e2e/`. Mock API in `e2e/helpers/mock-api.ts`. Auth helper in `e2e/helpers/auth.ts`.

## Rules

- Use `getByRole`, `getByText`, `getByLabel` — NOT CSS selectors
- Mock data field names MUST match API response (camelCase)
- Every new page needs a test file
- Override routes per-test with `route.fallback()` for non-matching URLs
- Mock API intercepts `**/api/proxy/**` routes
- Response envelope: `{ success: true, data: ... }`
- Paginated: `{ items, totalCount, page, pageSize }`
