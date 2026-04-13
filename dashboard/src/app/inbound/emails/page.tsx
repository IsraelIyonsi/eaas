"use client";

import { extractItems } from "@/lib/utils/api-response";

import { useState } from "react";
import { Inbox } from "lucide-react";
import { PageHeader } from "@/components/shared/page-header";
import { FilterBar } from "@/components/shared/filter-bar";
import { DataTable } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { getInboundEmailColumns } from "@/components/inbound/inbound-email-table";
import { useInboundEmails } from "@/lib/hooks/use-inbound";
import { Routes } from "@/lib/constants/routes";
import { PAGE_SIZE_DEFAULT } from "@/lib/constants/ui";
import type { InboundEmail, InboundEmailStatus } from "@/types/inbound";

const statusOptions = [
  { value: "", label: "All Statuses" },
  { value: "received", label: "Received" },
  { value: "processing", label: "Processing" },
  { value: "processed", label: "Processed" },
  { value: "forwarded", label: "Forwarded" },
  { value: "failed", label: "Failed" },
];

export default function InboundEmailsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [hasAttachments, setHasAttachments] = useState(false);

  const { data, isLoading } = useInboundEmails({
    page,
    page_size: PAGE_SIZE_DEFAULT,
    status: (status || undefined) as InboundEmailStatus | undefined,
    has_attachments: hasAttachments || undefined,
    from: search || undefined,
  });

  const columns = getInboundEmailColumns();
  const emails = extractItems(data);
  const total = data?.totalCount ?? 0;

  function handleClearFilters() {
    setStatus("");
    setSearch("");
    setHasAttachments(false);
    setPage(1);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Received Emails"
        description="View and manage all inbound emails received by your domains."
        badge={total > 0 ? `${total}` : undefined}
      />

      <FilterBar
        search={{
          value: search,
          onChange: (v) => {
            setSearch(v);
            setPage(1);
          },
          placeholder: "Search by sender, subject...",
        }}
        filters={[
          {
            key: "status",
            label: "Status",
            type: "select",
            options: statusOptions,
            value: status,
            onChange: (v) => {
              setStatus(v as string);
              setPage(1);
            },
          },
          {
            key: "has_attachments",
            label: "Has Attachments",
            type: "toggle",
            value: hasAttachments,
            onChange: (v) => {
              setHasAttachments(v as boolean);
              setPage(1);
            },
          },
        ]}
        onClear={handleClearFilters}
      />

      <DataTable<InboundEmail>
        columns={columns}
        data={emails}
        total={total}
        page={data?.page ?? 1}
        pageSize={data?.pageSize ?? PAGE_SIZE_DEFAULT}
        totalPages={Math.ceil((data?.totalCount ?? 0) / (data?.pageSize ?? PAGE_SIZE_DEFAULT))}
        onPageChange={setPage}
        loading={isLoading}
        getRowId={(e) => e.id}
        emptyState={
          <EmptyState
            icon={Inbox}
            title="No inbound emails yet"
            description="Set up your domain to start receiving emails. Configure MX records and inbound rules to get started."
            action={{
              label: "Set Up Domain",
              href: Routes.DOMAINS,
            }}
          />
        }
      />
    </div>
  );
}
