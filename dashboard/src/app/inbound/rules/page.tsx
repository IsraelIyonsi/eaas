"use client";

import { extractItems } from "@/lib/utils/api-response";

import { useState } from "react";
import { toast } from "sonner";
import { Plus, Shield } from "lucide-react";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/shared/page-header";
import { EmptyState } from "@/components/shared/empty-state";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { CardGridSkeleton } from "@/components/shared/loading-skeleton";
import { RuleCard } from "@/components/inbound/rule-card";
import { RuleFormSheet } from "@/components/inbound/rule-form-sheet";
import {
  useInboundRules,
  useUpdateInboundRule,
  useDeleteInboundRule,
} from "@/lib/hooks/use-inbound";
import type { InboundRule } from "@/types/inbound";

export default function InboundRulesPage() {
  const { data: rulesData, isLoading } = useInboundRules();
  const updateRule = useUpdateInboundRule();
  const deleteRule = useDeleteInboundRule();

  const [sheetOpen, setSheetOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<InboundRule | undefined>();
  const [deleteTarget, setDeleteTarget] = useState<InboundRule | null>(null);

  const rules: InboundRule[] = Array.isArray(rulesData)
    ? rulesData
    : extractItems(rulesData);

  const activeCount = rules.filter((r) => r.isActive).length;
  const inactiveCount = rules.length - activeCount;

  function handleEdit(rule: InboundRule) {
    setEditingRule(rule);
    setSheetOpen(true);
  }

  function handleCreate() {
    setEditingRule(undefined);
    setSheetOpen(true);
  }

  async function handleToggle(rule: InboundRule) {
    try {
      await updateRule.mutateAsync({
        id: rule.id,
        data: { isActive: !rule.isActive },
      });
      toast.success(
        rule.isActive ? "Rule deactivated" : "Rule activated",
      );
    } catch {
      toast.error("Failed to toggle rule");
    }
  }

  async function handleConfirmDelete() {
    if (!deleteTarget) return;
    try {
      await deleteRule.mutateAsync(deleteTarget.id);
      toast.success("Rule deleted");
      setDeleteTarget(null);
    } catch {
      toast.error("Failed to delete rule");
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Inbound Rules"
        description={
          rules.length > 0
            ? `${activeCount} active rule${activeCount !== 1 ? "s" : ""}${inactiveCount > 0 ? ` · ${inactiveCount} inactive` : ""}`
            : "Configure how inbound emails are routed and processed."
        }
        action={
          <Button onClick={handleCreate} size="sm">
            <Plus className="mr-1.5 h-4 w-4" />
            Create Rule
          </Button>
        }
      />

      {isLoading ? (
        <CardGridSkeleton cards={3} />
      ) : rules.length === 0 ? (
        <div className="rounded-lg border border-border bg-card">
          <EmptyState
            icon={Shield}
            title="No rules configured"
            description="Create your first inbound routing rule to control how emails are processed."
            action={{ label: "Create Rule", onClick: handleCreate }}
          />
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {rules
            .sort((a, b) => a.priority - b.priority)
            .map((rule) => (
              <RuleCard
                key={rule.id}
                rule={rule}
                onEdit={handleEdit}
                onDelete={setDeleteTarget}
                onToggle={handleToggle}
              />
            ))}
        </div>
      )}

      {/* Create/Edit Sheet */}
      <RuleFormSheet
        open={sheetOpen}
        onOpenChange={setSheetOpen}
        rule={editingRule}
      />

      {/* Delete Confirmation */}
      <ConfirmDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete Rule"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? Emails matching this rule will no longer be processed.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteRule.isPending}
        onConfirm={handleConfirmDelete}
      />
    </div>
  );
}
