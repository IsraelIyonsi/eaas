"use client";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { EmailStatusBadge } from "@/components/shared/status-badge";
import type { Email } from "@/types";
import { format, parseISO } from "date-fns";
import { ChevronLeft, ChevronRight } from "lucide-react";

interface EmailTableProps {
  emails: Email[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  onRowClick: (email: Email) => void;
  compact?: boolean;
}

export function EmailTable({
  emails,
  total,
  page,
  pageSize,
  totalPages,
  onPageChange,
  onRowClick,
  compact = false,
}: EmailTableProps) {
  return (
    <div>
      <div className="rounded-lg border border-border bg-card">
        <Table>
          <TableHeader>
            <TableRow className="border-border hover:bg-transparent">
              <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                Status
              </TableHead>
              <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                To
              </TableHead>
              <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                Subject
              </TableHead>
              {!compact && (
                <TableHead className="hidden text-xs font-semibold uppercase tracking-wider text-muted-foreground/60 md:table-cell">
                  From
                </TableHead>
              )}
              <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                Date
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {emails.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={compact ? 4 : 5}
                  className="h-24 text-center text-sm text-muted-foreground/60"
                >
                  No emails match your filters
                </TableCell>
              </TableRow>
            ) : (
              emails.map((email) => (
                <TableRow
                  key={email.id}
                  className="cursor-pointer border-border transition-colors hover:bg-muted even:bg-muted/30"
                  onClick={() => onRowClick(email)}
                >
                  <TableCell>
                    <EmailStatusBadge status={email.status} />
                  </TableCell>
                  <TableCell className="text-sm text-foreground/80">
                    {email.to}
                  </TableCell>
                  <TableCell className="max-w-[200px] truncate text-sm text-foreground">
                    {email.subject}
                  </TableCell>
                  {!compact && (
                    <TableCell className="hidden text-sm text-muted-foreground md:table-cell">
                      {email.from}
                    </TableCell>
                  )}
                  <TableCell className="text-xs text-muted-foreground/60 whitespace-nowrap">
                    {format(parseISO(email.createdAt), "MMM d, HH:mm")}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
      {!compact && totalPages > 1 && (
        <div className="mt-3 flex items-center justify-between">
          <p className="text-xs text-muted-foreground/60">
            Showing {(page - 1) * pageSize + 1}
            {" - "}
            {Math.min(page * pageSize, total)} of {total}
          </p>
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              disabled={page === 1}
              onClick={() => onPageChange(page - 1)}
              className="text-muted-foreground/60 hover:text-foreground"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <span className="px-2 text-xs text-muted-foreground">
              {page} / {totalPages}
            </span>
            <Button
              variant="ghost"
              size="sm"
              disabled={page === totalPages}
              onClick={() => onPageChange(page + 1)}
              className="text-muted-foreground/60 hover:text-foreground"
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
