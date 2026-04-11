"use client";

import { useState } from "react";
import {
  useSuppressions,
  useCreateSuppression,
  useDeleteSuppression,
} from "@/lib/hooks/use-suppressions";
import { PageHeader } from "@/components/shared/page-header";
import { EmptyState } from "@/components/shared/empty-state";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { FilterBar } from "@/components/shared/filter-bar";
import { SuppressionReasonConfig } from "@/lib/constants/status";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { SuppressionReasonBadge } from "@/components/shared/status-badge";
import { toast } from "sonner";
import { Plus, Trash2, ShieldBan } from "lucide-react";
import { format, parseISO } from "date-fns";
import type { SuppressionReason } from "@/types";

const reasonFilterOptions = [
  { value: "all", label: "All Reasons" },
  ...Object.entries(SuppressionReasonConfig).map(([value, config]) => ({
    value,
    label: config.label,
  })),
];

export default function SuppressionsPage() {
  const [search, setSearch] = useState("");
  const [reason, setReason] = useState("all");
  const [page, setPage] = useState(1);
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [removeId, setRemoveId] = useState<string | null>(null);
  const [newEmail, setNewEmail] = useState("");
  const [newReason, setNewReason] = useState<SuppressionReason>("manual");

  const { data, isLoading } = useSuppressions({
    search: search || undefined,
    reason: reason === "all" ? undefined : reason,
    page,
    page_size: 20,
  });

  const createMutation = useCreateSuppression();
  const deleteMutation = useDeleteSuppression();

  function handleAdd() {
    createMutation.mutate(
      { email: newEmail, reason: newReason },
      {
        onSuccess: () => {
          toast.success(
            `"${newEmail}" added to suppression list. Future sends to this address will be blocked.`,
          );
          setAddDialogOpen(false);
          setNewEmail("");
          setNewReason("manual");
        },
      },
    );
  }

  function handleRemove() {
    if (!removeId) return;
    deleteMutation.mutate(removeId, {
      onSuccess: () => {
        toast.success(
          "Suppression removed. Emails can now be sent to this address.",
        );
        setRemoveId(null);
      },
    });
  }

  function clearFilters() {
    setSearch("");
    setReason("all");
    setPage(1);
  }

  const hasFilters = search !== "" || reason !== "all";

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <PageHeader
        title="Suppression List"
        description="Addresses blocked from receiving emails due to bounces, complaints, or manual suppression."
        action={
          <Button
            onClick={() => setAddDialogOpen(true)}
            className="bg-primary text-primary-foreground hover:bg-primary/90"
          >
            <Plus className="mr-1.5 h-4 w-4" />
            Add Suppression
          </Button>
        }
      />

      {/* Filters */}
      <FilterBar
        search={{
          value: search,
          onChange: (v) => {
            setSearch(v);
            setPage(1);
          },
          placeholder: "Search by email address...",
        }}
        filters={[
          {
            key: "reason",
            label: "Reason",
            type: "select",
            options: reasonFilterOptions,
            value: reason,
            onChange: (v) => {
              setReason((v as string) ?? "all");
              setPage(1);
            },
          },
        ]}
        onClear={clearFilters}
      />

      {/* Table */}
      {isLoading ? (
        <Skeleton className="h-[400px] rounded-lg bg-muted" />
      ) : !data || data.items.length === 0 ? (
        <div className="rounded-lg border border-border bg-card">
          <EmptyState
            icon={ShieldBan}
            title={hasFilters ? "No suppressions match your filters" : "No suppressed addresses"}
            description={
              hasFilters
                ? "Try adjusting the reason filter or search terms."
                : "Addresses that hard bounce or receive complaints are automatically added here. You can also manually suppress addresses."
            }
            action={
              !hasFilters
                ? { label: "Add Suppression", onClick: () => setAddDialogOpen(true) }
                : undefined
            }
          />
        </div>
      ) : (
        <div className="rounded-lg border border-border bg-card">
          <Table>
            <TableHeader>
              <TableRow className="border-border hover:bg-transparent">
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                  Email Address
                </TableHead>
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                  Reason
                </TableHead>
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                  Date Added
                </TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((sup) => (
                <TableRow
                  key={sup.id}
                  className="border-border transition-colors hover:bg-muted even:bg-muted/30"
                >
                  <TableCell className="font-mono text-sm text-foreground">
                    {sup.emailAddress}
                  </TableCell>
                  <TableCell>
                    <SuppressionReasonBadge reason={sup.reason} />
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground/60 whitespace-nowrap">
                    {sup.suppressedAt ? format(parseISO(sup.suppressedAt), "MMM d, yyyy") : "—"}
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8 text-muted-foreground/40 hover:text-red-400"
                      onClick={() => setRemoveId(sup.id)}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Add Suppression Dialog */}
      <Dialog open={addDialogOpen} onOpenChange={setAddDialogOpen}>
        <DialogContent className="max-w-sm border-border bg-card">
          <DialogHeader>
            <DialogTitle className="text-foreground">Add Suppression</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label className="text-foreground/80">Email Address</Label>
              <Input
                type="email"
                value={newEmail}
                onChange={(e) => setNewEmail(e.target.value)}
                placeholder="user@example.com"
                className="border-border bg-muted text-foreground"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-foreground/80">Reason</Label>
              <Select value={newReason} onValueChange={(v) => setNewReason((v ?? "manual") as SuppressionReason)}>
                <SelectTrigger className="border-border bg-muted text-foreground">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent className="border-border bg-muted">
                  <SelectItem value="manual">Manual</SelectItem>
                  <SelectItem value="hard_bounce">Hard Bounce</SelectItem>
                  <SelectItem value="complaint">Complaint</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="ghost"
              onClick={() => setAddDialogOpen(false)}
              className="text-muted-foreground"
            >
              Cancel
            </Button>
            <Button
              onClick={handleAdd}
              disabled={!newEmail || createMutation.isPending}
              className="bg-primary text-primary-foreground hover:bg-primary/90"
            >
              Add Suppression
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Remove Confirmation Dialog */}
      <ConfirmDialog
        open={!!removeId}
        onOpenChange={() => setRemoveId(null)}
        title="Remove Suppression"
        description="Are you sure you want to remove this suppression? Emails will be able to be sent to this address again."
        confirmLabel="Remove Suppression"
        variant="destructive"
        loading={deleteMutation.isPending}
        onConfirm={handleRemove}
      />
    </div>
  );
}
