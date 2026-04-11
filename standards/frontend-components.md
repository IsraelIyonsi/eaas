# Frontend Components & Styling

## Shared Components (`components/shared/`)

PageHeader, DataTable, FilterBar, EmptyState, ConfirmDialog, CopyButton, StatusBadge, LoadingSkeleton

## Page Pattern

- `"use client"` on every page using hooks
- Pages are thin: hooks + shared components + layout
- Feature-specific rendering in `components/{feature}/`
- Every page handles loading, empty, and error states

## Styling

- Tailwind CSS with CSS variable theming
- Semantic tokens: `bg-background`, `text-foreground`, `bg-muted`, `border-border`
- NO hardcoded hex colors (`bg-[#...]`)
- Primary: `#2563eb` (blue-600)
- Fonts: Inter (UI), JetBrains Mono (code)
- Sidebar: always dark (`#0f172a`)
- Don't modify `components/ui/` (shadcn/ui primitives)
