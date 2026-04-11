"use client";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { CopyButton } from "@/components/shared/copy-button";
import { cn } from "@/lib/utils";
import type { DnsRecord } from "@/types/domain";

function getVerificationStatus(isVerified: boolean) {
  return isVerified
    ? { label: "Verified", icon: "\u2713", className: "bg-emerald-500/15 text-emerald-400 border-emerald-500/30" }
    : { label: "Not Verified", icon: "\u2717", className: "bg-red-500/15 text-red-400 border-red-500/30" };
}

interface DnsRecordsTableProps {
  records: DnsRecord[];
}

export function DnsRecordsTable({ records }: DnsRecordsTableProps) {
  if (records.length === 0) {
    return (
      <p className="py-4 text-center text-sm text-muted-foreground/60">
        No DNS records to display
      </p>
    );
  }

  return (
    <div className="overflow-auto rounded-md border border-border">
      <Table>
        <TableHeader>
          <TableRow className="border-border hover:bg-transparent">
            <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
              Type
            </TableHead>
            <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
              Name
            </TableHead>
            <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
              Value
            </TableHead>
            <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
              Purpose
            </TableHead>
            <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
              Status
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {records.map((record, idx) => {
            const status = getVerificationStatus(record.isVerified);
            return (
              <TableRow
                key={`${record.type}-${record.name}-${idx}`}
                className="border-border hover:bg-muted"
              >
                <TableCell>
                  <Badge
                    variant="outline"
                    className="border-border bg-muted font-mono text-xs text-foreground/80"
                  >
                    {record.type}
                  </Badge>
                </TableCell>
                <TableCell className="max-w-[200px]">
                  <div className="flex items-center gap-1">
                    <code className="truncate font-mono text-xs text-foreground/80">
                      {record.name}
                    </code>
                    <CopyButton value={record.name} label="Record name" />
                  </div>
                </TableCell>
                <TableCell className="max-w-[300px]">
                  <div className="flex items-center gap-1">
                    <code className="truncate font-mono text-xs text-muted-foreground">
                      {record.value}
                    </code>
                    <CopyButton value={record.value} label="Record value" />
                  </div>
                </TableCell>
                <TableCell className="text-xs text-muted-foreground">
                  {record.purpose ?? "Configuration"}
                </TableCell>
                <TableCell>
                  <Badge
                    variant="outline"
                    className={cn("text-xs font-medium", status.className)}
                  >
                    {status.icon} {status.label}
                  </Badge>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
