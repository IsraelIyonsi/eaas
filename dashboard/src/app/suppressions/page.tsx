"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
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
import { Plus, Search, Trash2, X } from "lucide-react";
import { format, parseISO } from "date-fns";

const reasonOptions = [
  { value: "all", label: "All Reasons" },
  { value: "hard_bounce", label: "Hard Bounce" },
  { value: "soft_bounce_limit", label: "Soft Bounce Limit" },
  { value: "complaint", label: "Complaint" },
  { value: "manual", label: "Manual" },
];

export default function SuppressionsPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [reason, setReason] = useState("all");
  const [page, setPage] = useState(1);
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [removeId, setRemoveId] = useState<string | null>(null);
  const [newEmail, setNewEmail] = useState("");
  const [newReason, setNewReason] = useState("manual");

  const { data, isLoading } = useQuery({
    queryKey: ["suppressions", search, reason, page],
    queryFn: () =>
      api.getSuppressions({
        search: search || undefined,
        reason: reason === "all" ? undefined : reason,
        page,
        page_size: 20,
      }),
    placeholderData: (prev) => prev,
  });

  const addMutation = useMutation({
    mutationFn: () => api.addSuppression(newEmail, newReason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["suppressions"] });
      toast.success(
        `"${newEmail}" added to suppression list. Future sends to this address will be blocked.`,
      );
      setAddDialogOpen(false);
      setNewEmail("");
      setNewReason("manual");
    },
  });

  const removeMutation = useMutation({
    mutationFn: (id: string) => api.removeSuppression(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["suppressions"] });
      toast.success(
        "Suppression removed. Emails can now be sent to this address.",
      );
      setRemoveId(null);
    },
  });

  const hasFilters = search !== "" || reason !== "all";

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Suppression List</h1>
          <p className="text-sm text-white/50">
            Addresses blocked from receiving emails due to bounces, complaints,
            or manual suppression.
          </p>
        </div>
        <Button
          onClick={() => setAddDialogOpen(true)}
          className="bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
        >
          <Plus className="mr-1.5 h-4 w-4" />
          Add Suppression
        </Button>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={reason}
          onValueChange={(v) => {
            setReason(v ?? "all");
            setPage(1);
          }}
        >
          <SelectTrigger className="w-[180px] border-white/10 bg-[#27293D] text-white">
            <SelectValue placeholder="Reason" />
          </SelectTrigger>
          <SelectContent className="border-white/10 bg-[#27293D]">
            {reasonOptions.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <div className="relative flex-1 min-w-[200px] max-w-xs">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-white/30" />
          <Input
            placeholder="Search by email address..."
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
            onClick={() => {
              setSearch("");
              setReason("all");
              setPage(1);
            }}
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
      ) : !data || data.items.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-white/10 bg-[#1E1E2E] py-16 text-center">
          <p className="text-lg font-semibold text-white">
            {hasFilters ? "No suppressions match your filters" : "No suppressed addresses"}
          </p>
          <p className="mt-2 max-w-sm text-sm text-white/50">
            {hasFilters
              ? "Try adjusting the reason filter or search terms."
              : "Addresses that hard bounce or receive complaints are automatically added here. You can also manually suppress addresses."}
          </p>
          {!hasFilters && (
            <Button
              onClick={() => setAddDialogOpen(true)}
              className="mt-4 bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
            >
              Add Suppression
            </Button>
          )}
        </div>
      ) : (
        <div className="rounded-lg border border-white/10 bg-[#1E1E2E]">
          <Table>
            <TableHeader>
              <TableRow className="border-white/10 hover:bg-transparent">
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-white/40">
                  Email Address
                </TableHead>
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-white/40">
                  Reason
                </TableHead>
                <TableHead className="text-xs font-semibold uppercase tracking-wider text-white/40">
                  Date Added
                </TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((sup) => (
                <TableRow
                  key={sup.id}
                  className="border-white/5 transition-colors hover:bg-white/[0.06] even:bg-white/[0.02]"
                >
                  <TableCell className="font-mono text-sm text-white/80">
                    {sup.email}
                  </TableCell>
                  <TableCell>
                    <SuppressionReasonBadge reason={sup.reason} />
                  </TableCell>
                  <TableCell className="text-xs text-white/40 whitespace-nowrap">
                    {format(parseISO(sup.created_at), "MMM d, yyyy")}
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8 text-white/30 hover:text-red-400"
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
        <DialogContent className="max-w-sm border-white/10 bg-[#1E1E2E]">
          <DialogHeader>
            <DialogTitle className="text-white">Add Suppression</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label className="text-white/70">Email Address</Label>
              <Input
                type="email"
                value={newEmail}
                onChange={(e) => setNewEmail(e.target.value)}
                placeholder="user@example.com"
                className="border-white/10 bg-[#27293D] text-white"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-white/70">Reason</Label>
              <Select value={newReason} onValueChange={(v) => setNewReason(v ?? "manual")}>
                <SelectTrigger className="border-white/10 bg-[#27293D] text-white">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent className="border-white/10 bg-[#27293D]">
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
              className="text-white/60"
            >
              Cancel
            </Button>
            <Button
              onClick={() => addMutation.mutate()}
              disabled={!newEmail || addMutation.isPending}
              className="bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
            >
              Add Suppression
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Remove Confirmation Dialog */}
      <Dialog open={!!removeId} onOpenChange={() => setRemoveId(null)}>
        <DialogContent className="max-w-sm border-white/10 bg-[#1E1E2E]">
          <DialogHeader>
            <DialogTitle className="text-white">Remove Suppression</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-white/60">
            Are you sure you want to remove this suppression? Emails will be
            able to be sent to this address again.
          </p>
          <DialogFooter>
            <Button
              variant="ghost"
              onClick={() => setRemoveId(null)}
              className="text-white/60"
            >
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => removeId && removeMutation.mutate(removeId)}
            >
              Remove Suppression
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
