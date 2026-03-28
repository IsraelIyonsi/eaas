"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { EmailTable } from "@/components/emails/email-table";
import { EmailDetailSheet } from "@/components/emails/email-detail";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Search, X } from "lucide-react";
import type { Email, EmailStatus } from "@/types";

const statusOptions: { value: string; label: string }[] = [
  { value: "all", label: "All Statuses" },
  { value: "queued", label: "Queued" },
  { value: "sending", label: "Sending" },
  { value: "delivered", label: "Delivered" },
  { value: "bounced", label: "Bounced" },
  { value: "complained", label: "Complained" },
  { value: "failed", label: "Failed" },
  { value: "opened", label: "Opened" },
  { value: "clicked", label: "Clicked" },
];

export default function EmailsPage() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("all");
  const [search, setSearch] = useState("");
  const [selectedEmail, setSelectedEmail] = useState<Email | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["emails", page, status, search],
    queryFn: () =>
      api.getEmails({
        page,
        page_size: 10,
        status: status === "all" ? undefined : status,
        search: search || undefined,
      }),
    placeholderData: (prev) => prev,
  });

  const { data: events } = useQuery({
    queryKey: ["email-events", selectedEmail?.id],
    queryFn: () =>
      selectedEmail ? api.getEmailEvents(selectedEmail) : Promise.resolve([]),
    enabled: !!selectedEmail,
  });

  function clearFilters() {
    setStatus("all");
    setSearch("");
    setPage(1);
  }

  const hasFilters = status !== "all" || search !== "";

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-xl font-bold text-white">Email Logs</h1>
        <p className="mt-1 text-sm text-white/50">
          Search and filter all sent emails. Click any row to view full delivery
          details.
        </p>
      </div>

      {/* Filter Bar */}
      <div className="flex flex-wrap items-center gap-3 pb-2">
        <Select
          value={status}
          onValueChange={(v) => {
            setStatus(v ?? "all");
            setPage(1);
          }}
        >
          <SelectTrigger className="w-[160px] border-white/10 bg-[#27293D] text-white">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent className="border-white/10 bg-[#27293D]">
            {statusOptions.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <div className="relative flex-1 min-w-[200px] max-w-xs">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-white/30" />
          <Input
            placeholder="Search by recipient, subject..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            className="border-white/10 bg-[#27293D] pl-9 text-white placeholder:text-white/30"
          />
        </div>

        {hasFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={clearFilters}
            className="text-white/40 hover:text-white"
          >
            <X className="mr-1 h-3 w-3" />
            Clear Filters
          </Button>
        )}
      </div>

      {/* Table */}
      {isLoading ? (
        <Skeleton className="h-[400px] rounded-lg bg-white/5" />
      ) : (
        <EmailTable
          emails={data?.items ?? []}
          total={data?.total ?? 0}
          page={data?.page ?? 1}
          pageSize={data?.page_size ?? 10}
          totalPages={data?.total_pages ?? 1}
          onPageChange={setPage}
          onRowClick={setSelectedEmail}
        />
      )}

      {/* Detail Sheet */}
      <EmailDetailSheet
        email={selectedEmail}
        events={events ?? []}
        open={!!selectedEmail}
        onClose={() => setSelectedEmail(null)}
      />
    </div>
  );
}
