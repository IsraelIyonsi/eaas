# Frontend Architecture

## Structure

```
dashboard/src/
  app/                  # Next.js App Router pages
  components/shared/    # Reusable: PageHeader, DataTable, FilterBar, EmptyState, etc.
  components/ui/        # shadcn/ui primitives (do not modify)
  components/{feature}/ # Feature-specific components
  lib/api/client.ts     # HttpClient base + CrudRepository
  lib/api/repositories/ # Feature repositories
  lib/hooks/            # React Query hooks per feature
  lib/constants/        # Routes, API paths, query keys, status configs, UI values
  lib/utils/api-response.ts  # extractItems, extractTotalCount, safeConfigLookup
  types/                # TypeScript interfaces per feature domain
```

## Repository Pattern

- `CrudRepository<T>` for standard CRUD — just set `basePath`
- `HttpClient` for custom operations
- All requests go through `/api/proxy${path}`

## React Query Hooks

- `staleTime: STALE_TIME_MS` on queries
- `enabled: !!id` on detail queries
- Mutations invalidate the `all` key
- Query keys from `QueryKeys` constant

## Constants (in `lib/constants/`)

- `api-paths.ts` — API endpoint paths
- `query-keys.ts` — React Query cache keys
- `routes.ts` — page navigation routes
- `status.ts` — status display metadata (label, color)
- `ui.ts` — page sizes, stale times

## Types (in `types/`)

- camelCase fields matching .NET JSON serialization
- Status enums as lowercase string unions
- Dates as `string` (ISO 8601)
- `PaginatedResponse<T>`: `{ items, totalCount, page, pageSize }`

## Critical Patterns

- Use `extractItems()` for all list data — NEVER inline `Array.isArray`
- Use `safeConfigLookup()` for status display
- Enum values from backend are lowercase strings
- JSON columns must be parsed in API endpoint's `Select()`
