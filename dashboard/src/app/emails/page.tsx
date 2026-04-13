"use client";

import { extractItems } from "@/lib/utils/api-response";

import { useState } from "react";
import { useEmails, useEmailEvents } from "@/lib/hooks/use-emails";
import { PageHeader } from "@/components/shared/page-header";
import { EmailTable } from "@/components/emails/email-table";
import { EmailDetailSheet } from "@/components/emails/email-detail";
import { EmailStatusConfig } from "@/lib/constants/status";
import { PAGE_SIZE_COMPACT } from "@/lib/constants/ui";
import { Skeleton } from "@/components/ui/skeleton";
import { FilterBar } from "@/components/shared/filter-bar";
import type { Email } from "@/types";

const statusOptions = [
  { value: "all", label: "All Statuses" },
  ...Object.entries(EmailStatusConfig).map(([value, config]) => ({
    value,
    label: config.label,
  })),
];

export default function EmailsPage() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("all");
  const [search, setSearch] = useState("");
  const [selectedEmail, setSelectedEmail] = useState<Email | null>(null);

  const { data, isLoading } = useEmails({
    page,
    page_size: PAGE_SIZE_COMPACT,
    status: status === "all" ? undefined : (status as Email["status"]),
    search: search || undefined,
  });

  const { data: events } = useEmailEvents(selectedEmail?.id);

  const hasFilters = status !== "all" || search !== "";

  function clearFilters() {
    setStatus("all");
    setSearch("");
    setPage(1);
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <PageHeader
        title="Email Logs"
        description="Search and filter all sent emails. Click any row to view full delivery details."
      />

      {/* Filter Bar */}
      <FilterBar
        search={{
          value: search,
          onChange: (v) => {
            setSearch(v);
            setPage(1);
          },
          placeholder: "Search by recipient, subject...",
        }}
        filters={[
          {
            key: "status",
            label: "Status",
            type: "select",
            options: statusOptions,
            value: status,
            onChange: (v) => {
              setStatus((v as string) ?? "all");
              setPage(1);
            },
          },
        ]}
        onClear={clearFilters}
      />

      {/* Table */}
      {isLoading ? (
        <Skeleton className="h-[400px] rounded-lg" />
      ) : (
        <EmailTable
          emails={extractItems(data)}
          total={data?.totalCount ?? 0}
          page={data?.page ?? 1}
          pageSize={data?.pageSize ?? PAGE_SIZE_COMPACT}
          totalPages={Math.ceil((data?.totalCount ?? 0) / (data?.pageSize ?? PAGE_SIZE_COMPACT))}
          onPageChange={setPage}
          onRowClick={setSelectedEmail}
          hasFilters={hasFilters}
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
