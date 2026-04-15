"use client";

import { ChevronLeft, ChevronRight } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import { PAGE_SIZE_DEFAULT, DATA_TABLE_SKELETON_ROWS } from "@/lib/constants/ui";

const EMPTY_SET = new Set<string>();

interface Column<T> {
  key: string;
  header: string;
  render: (item: T) => React.ReactNode;
  className?: string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  total?: number;
  page?: number;
  pageSize?: number;
  totalPages?: number;
  onPageChange?: (page: number) => void;
  onRowClick?: (item: T) => void;
  loading?: boolean;
  emptyState?: React.ReactNode;
  selectable?: boolean;
  selectedIds?: Set<string>;
  onSelectionChange?: (ids: Set<string>) => void;
  getRowId?: (item: T) => string;
}

export function DataTable<T>({
  columns,
  data,
  total,
  page = 1,
  pageSize = PAGE_SIZE_DEFAULT,
  totalPages,
  onPageChange,
  onRowClick,
  loading = false,
  emptyState,
  selectable = false,
  selectedIds,
  onSelectionChange,
  getRowId,
}: DataTableProps<T>) {
  const safeData = data ?? [];
  const computedTotalPages = totalPages ?? (total ? Math.ceil(total / pageSize) : 1);
  const start = (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, total ?? safeData.length);
  const displayTotal = total ?? safeData.length;

  const resolvedSelectedIds = selectedIds ?? EMPTY_SET;
  const allSelected = safeData.length > 0 && getRowId && safeData.every((item) => resolvedSelectedIds.has(getRowId(item)));

  function handleSelectAll() {
    if (!getRowId || !onSelectionChange) return;
    if (allSelected) {
      const next = new Set(resolvedSelectedIds);
      safeData.forEach((item) => next.delete(getRowId(item)));
      onSelectionChange(next);
    } else {
      const next = new Set(resolvedSelectedIds);
      safeData.forEach((item) => next.add(getRowId(item)));
      onSelectionChange(next);
    }
  }

  function handleSelectRow(item: T) {
    if (!getRowId || !onSelectionChange) return;
    const id = getRowId(item);
    const next = new Set(resolvedSelectedIds);
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    onSelectionChange(next);
  }

  if (loading) {
    return (
      <div className="data-table-wrap bg-background">
        <table className="w-full min-w-[640px] border-collapse">
          <thead>
            <tr className="bg-muted/50">
              {selectable && <th className="w-10 px-[14px] py-[10px]" />}
              {columns.map((col) => (
                <th
                  key={col.key}
                  className={cn(
                    "px-[14px] py-[10px] text-left text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground border-b border-border",
                    col.className,
                  )}
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: DATA_TABLE_SKELETON_ROWS }).map((_, i) => (
              <tr key={i} className="border-b border-border">
                {selectable && (
                  <td className="px-[14px] py-[13px]">
                    <Skeleton className="h-4 w-4" />
                  </td>
                )}
                {columns.map((col) => (
                  <td key={col.key} className="px-[14px] py-[13px]">
                    <Skeleton className="h-4 w-24" />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  if (safeData.length === 0 && emptyState) {
    return (
      <div className="data-table-wrap bg-background">
        {emptyState}
      </div>
    );
  }

  return (
    <div className="data-table-wrap bg-background">
      <table className="w-full min-w-[640px] border-collapse">
        {/* thead: bg-surface (bg-muted/50) */}
        <thead>
          <tr className="bg-muted/50">
            {selectable && (
              <th className="w-10 px-[14px] py-[10px] border-b border-border">
                <input
                  type="checkbox"
                  checked={allSelected}
                  onChange={handleSelectAll}
                  className="h-4 w-4 rounded border-border bg-transparent accent-primary"
                />
              </th>
            )}
            {/* th: padding 10px 14px, text-left, font-weight 500, font-size 12px, text-secondary, uppercase, tracking 0.04em, border-bottom */}
            {columns.map((col) => (
              <th
                key={col.key}
                className={cn(
                  "px-[14px] py-[10px] text-left text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground border-b border-border",
                  col.className,
                )}
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        {/* tbody tr:hover: bg-hover */}
        <tbody>
          {safeData.map((item, index) => {
            const rowId = getRowId?.(item);
            const isSelected = rowId ? resolvedSelectedIds.has(rowId) : false;

            return (
              <tr
                key={rowId ?? index}
                className={cn(
                  "border-b border-border text-foreground/80 transition-colors",
                  onRowClick && "cursor-pointer hover:bg-muted/50",
                  isSelected && "bg-primary/10",
                )}
                onClick={() => onRowClick?.(item)}
              >
                {selectable && (
                  <td
                    className="px-[14px] py-[13px] align-middle"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <input
                      type="checkbox"
                      checked={isSelected}
                      onChange={() => handleSelectRow(item)}
                      className="h-4 w-4 rounded border-border bg-transparent accent-primary"
                    />
                  </td>
                )}
                {/* td: padding 10px 14px, border-bottom, vertical-align middle */}
                {columns.map((col) => (
                  <td
                    key={col.key}
                    className={cn("px-[14px] py-[13px] align-middle text-[13px]", col.className)}
                  >
                    {col.render(item)}
                  </td>
                ))}
              </tr>
            );
          })}
        </tbody>
      </table>

      {/* Pagination: flex, justify-between, margin-top 16px, font-size 13px */}
      {onPageChange && (
        <div className="flex items-center justify-between border-t border-border px-[14px] py-3">
          <span className="text-[13px] text-muted-foreground">
            Showing {start}-{end} of {displayTotal}
          </span>
          <div className="flex items-center gap-2">
            <button
              onClick={() => onPageChange(page - 1)}
              disabled={page <= 1}
              aria-label="Previous page"
              className="inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1 text-[13px] font-medium text-foreground transition-colors hover:bg-muted disabled:opacity-30 disabled:pointer-events-none"
            >
              <ChevronLeft className="h-4 w-4" />
              <span className="hidden sm:inline">Previous</span>
            </button>
            <span className="text-[13px] text-muted-foreground">
              Page {page} of {computedTotalPages}
            </span>
            <button
              onClick={() => onPageChange(page + 1)}
              disabled={page >= computedTotalPages}
              aria-label="Next page"
              className="inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1 text-[13px] font-medium text-foreground transition-colors hover:bg-muted disabled:opacity-30 disabled:pointer-events-none"
            >
              <span className="hidden sm:inline">Next</span>
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
