"use client";

import { useState } from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { useWebhookDeliveries } from "@/lib/hooks/use-webhooks";
import { format, parseISO } from "date-fns";
import { ChevronLeft, ChevronRight, RotateCw } from "lucide-react";
import { cn } from "@/lib/utils";
import { PAGE_SIZE_COMPACT } from "@/lib/constants/ui";
import type { WebhookDelivery } from "@/types/webhook";

function statusCodeClass(code: number): string {
  if (code >= 200 && code < 300)
    return "bg-emerald-500/15 text-emerald-400 border-emerald-500/30";
  if (code >= 400 && code < 500)
    return "bg-amber-500/15 text-amber-400 border-amber-500/30";
  return "bg-red-500/15 text-red-400 border-red-500/30";
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  return `${(bytes / 1024).toFixed(1)} KB`;
}

interface DeliveryLogsProps {
  webhookId: string;
}

export function DeliveryLogs({ webhookId }: DeliveryLogsProps) {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useWebhookDeliveries(webhookId, {
    page,
    page_size: PAGE_SIZE_COMPACT,
  });

  const deliveries: WebhookDelivery[] = Array.isArray(data)
    ? data
    : (data as { items?: WebhookDelivery[] })?.items ?? [];
  const totalPages =
    (data as { totalCount?: number; pageSize?: number })?.totalCount
      ? Math.ceil(((data as { totalCount: number }).totalCount) / ((data as { pageSize?: number }).pageSize ?? PAGE_SIZE_COMPACT))
      : 1;

  if (isLoading) {
    return (
      <div className="space-y-2 border-t border-border pt-3">
        <Skeleton className="h-[120px] rounded bg-muted" />
      </div>
    );
  }

  if (deliveries.length === 0) {
    return (
      <div className="border-t border-border pt-3 text-center">
        <p className="py-4 text-xs text-muted-foreground/60">No delivery logs yet</p>
      </div>
    );
  }

  return (
    <div className="space-y-2 border-t border-border pt-3">
      <p className="text-xs font-medium text-muted-foreground">Recent Deliveries</p>
      <div className="overflow-auto rounded-md border border-border">
        <Table>
          <TableHeader>
            <TableRow className="border-border hover:bg-transparent">
              <TableHead className="text-[10px] uppercase tracking-wider text-muted-foreground/60">
                Timestamp
              </TableHead>
              <TableHead className="text-[10px] uppercase tracking-wider text-muted-foreground/60">
                Event
              </TableHead>
              <TableHead className="text-[10px] uppercase tracking-wider text-muted-foreground/60">
                Status
              </TableHead>
              <TableHead className="text-[10px] uppercase tracking-wider text-muted-foreground/60">
                Response
              </TableHead>
              <TableHead className="text-[10px] uppercase tracking-wider text-muted-foreground/60">
                Size
              </TableHead>
              <TableHead className="w-8" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {deliveries.map((d) => (
              <TableRow
                key={d.id}
                className="border-border hover:bg-muted"
              >
                <TableCell className="text-[11px] text-muted-foreground whitespace-nowrap">
                  {format(parseISO(d.timestamp), "MMM d, HH:mm:ss")}
                </TableCell>
                <TableCell className="text-[11px] text-muted-foreground">
                  {d.eventType}
                </TableCell>
                <TableCell>
                  <Badge
                    variant="outline"
                    className={cn(
                      "text-[10px] font-medium",
                      statusCodeClass(d.statusCode),
                    )}
                  >
                    {d.statusCode}
                  </Badge>
                </TableCell>
                <TableCell className="text-[11px] text-muted-foreground">
                  {d.responseTimeMs}ms
                </TableCell>
                <TableCell className="text-[11px] text-muted-foreground">
                  {formatBytes(d.payload_sizeBytes)}
                </TableCell>
                <TableCell>
                  {d.statusCode >= 400 && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-6 w-6 p-0 text-muted-foreground/40 hover:text-foreground"
                      title="Retry"
                    >
                      <RotateCw className="h-3 w-3" />
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2 pt-1">
          <Button
            variant="ghost"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage(page - 1)}
            className="h-6 text-xs text-muted-foreground/60"
          >
            <ChevronLeft className="h-3 w-3" />
          </Button>
          <span className="text-[10px] text-muted-foreground/60">
            {page} / {totalPages}
          </span>
          <Button
            variant="ghost"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => setPage(page + 1)}
            className="h-6 text-xs text-muted-foreground/60"
          >
            <ChevronRight className="h-3 w-3" />
          </Button>
        </div>
      )}
    </div>
  );
}
