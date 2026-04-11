"use client";

import { extractItems } from "@/lib/utils/api-response";

import { useState } from "react";
import { PageHeader } from "@/components/shared/page-header";
import { EmptyState } from "@/components/shared/empty-state";
import { CardGridSkeleton } from "@/components/shared/loading-skeleton";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import {
  useWebhooks,
  useTestWebhook,
  useDeleteWebhook,
  useUpdateWebhook,
} from "@/lib/hooks/use-webhooks";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { WebhookFormSheet } from "@/components/webhooks/webhook-form-sheet";
import { DeliveryLogs } from "@/components/webhooks/delivery-logs";
import { toast } from "sonner";
import {
  Plus,
  Webhook,
  Pencil,
  Play,
  ScrollText,
  Trash2,
  CheckCircle2,
  XCircle,
  Eye,
  EyeOff,
  Loader2,
} from "lucide-react";
import { formatDistanceToNow, parseISO } from "date-fns";
import { cn } from "@/lib/utils";
import type {
  Webhook as WebhookType,
  WebhookStatus,
  TestWebhookResult,
} from "@/types/webhook";

const statusBadgeClass: Record<WebhookStatus, string> = {
  active: "bg-emerald-500/15 text-emerald-400 border-emerald-500/30",
  inactive: "bg-gray-500/15 text-gray-400 border-gray-500/30",
  disabled: "bg-red-500/15 text-red-400 border-red-500/30",
};

const eventColors: Record<string, string> = {
  "email.queued": "bg-gray-500/15 text-gray-400 border-gray-500/30",
  "email.sent": "bg-blue-500/15 text-blue-400 border-blue-500/30",
  "email.delivered": "bg-emerald-500/15 text-emerald-400 border-emerald-500/30",
  "email.bounced": "bg-red-500/15 text-red-400 border-red-500/30",
  "email.complained": "bg-amber-500/15 text-amber-400 border-amber-500/30",
  "email.opened": "bg-violet-500/15 text-violet-400 border-violet-500/30",
  "email.clicked": "bg-cyan-500/15 text-cyan-400 border-cyan-500/30",
  "email.failed": "bg-red-600/15 text-red-500 border-red-600/30",
  "inbound.received": "bg-purple-500/15 text-purple-400 border-purple-500/30",
};

export default function WebhooksPage() {
  const { data: webhooks, isLoading } = useWebhooks();
  const testMutation = useTestWebhook();
  const deleteMutation = useDeleteWebhook();
  const updateMutation = useUpdateWebhook();

  const [formOpen, setFormOpen] = useState(false);
  const [editWebhook, setEditWebhook] = useState<WebhookType | null>(null);
  const [deleteWebhook, setDeleteWebhook] = useState<WebhookType | null>(null);
  const [logsWebhookId, setLogsWebhookId] = useState<string | null>(null);
  const [testResults, setTestResults] = useState<Record<string, TestWebhookResult>>({});
  const [secretVisible, setSecretVisible] = useState<Record<string, boolean>>({});

  const items = extractItems(webhooks);

  function handleTest(id: string) {
    testMutation.mutate(id, {
      onSuccess: (result) => {
        const res = result as unknown as TestWebhookResult;
        setTestResults((prev) => ({ ...prev, [id]: res }));
        if (res.success) {
          toast.success("Webhook test successful");
        } else {
          toast.error(`Webhook test failed: ${res.error ?? `Status ${res.statusCode}`}`);
        }
      },
      onError: () => {
        toast.error("Failed to send test webhook");
      },
    });
  }

  function handleToggleStatus(webhook: WebhookType) {
    const newStatus = webhook.status === "active" ? "inactive" : "active";
    updateMutation.mutate(
      { id: webhook.id, data: { status: newStatus } },
      {
        onSuccess: () => {
          toast.success(
            `Webhook ${newStatus === "active" ? "activated" : "deactivated"}`,
          );
        },
      },
    );
  }

  function handleDelete() {
    if (!deleteWebhook) return;
    deleteMutation.mutate(deleteWebhook.id, {
      onSuccess: () => {
        toast.success("Webhook deleted");
        setDeleteWebhook(null);
      },
      onError: () => {
        toast.error("Failed to delete webhook");
      },
    });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Webhooks"
        description="Receive real-time notifications for email events."
        badge={items.length > 0 ? `${items.length}` : undefined}
        action={
          <Button
            onClick={() => {
              setEditWebhook(null);
              setFormOpen(true);
            }}
            className="bg-primary text-primary-foreground hover:bg-primary/90"
          >
            <Plus className="mr-1.5 h-4 w-4" />
            Create Webhook
          </Button>
        }
      />

      {isLoading ? (
        <CardGridSkeleton cards={3} />
      ) : items.length === 0 ? (
        <div className="rounded-lg border border-border bg-card">
          <EmptyState
            icon={Webhook}
            title="No webhooks configured"
            description="Set up webhooks to receive real-time notifications when emails are delivered, opened, clicked, or bounce."
            action={{
              label: "Create Webhook",
              onClick: () => {
                setEditWebhook(null);
                setFormOpen(true);
              },
            }}
          />
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-1 lg:grid-cols-2">
          {items.map((wh) => {
            const testResult = testResults[wh.id];
            return (
              <div
                key={wh.id}
                className="rounded-lg border border-border bg-card p-5 space-y-4"
              >
                {/* URL + status toggle */}
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-mono text-sm text-foreground">{wh.url}</p>
                  </div>
                  <Switch
                    checked={wh.status === "active"}
                    onCheckedChange={() => handleToggleStatus(wh)}
                  />
                </div>

                {/* Event badges */}
                <div className="flex flex-wrap gap-1.5">
                  {wh.events.map((evt) => (
                    <Badge
                      key={evt}
                      variant="outline"
                      className={cn(
                        "text-[10px] font-medium",
                        eventColors[evt] ?? "bg-muted text-muted-foreground border-border",
                      )}
                    >
                      {evt}
                    </Badge>
                  ))}
                </div>

                {/* Secret */}
                {wh.secret && (
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-muted-foreground/60">Secret:</span>
                    <code className="font-mono text-xs text-muted-foreground">
                      {secretVisible[wh.id] ? wh.secret : "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022"}
                    </code>
                    <button
                      onClick={() =>
                        setSecretVisible((prev) => ({
                          ...prev,
                          [wh.id]: !prev[wh.id],
                        }))
                      }
                      className="text-muted-foreground/40 hover:text-muted-foreground"
                    >
                      {secretVisible[wh.id] ? (
                        <EyeOff className="h-3.5 w-3.5" />
                      ) : (
                        <Eye className="h-3.5 w-3.5" />
                      )}
                    </button>
                  </div>
                )}

                {/* Meta info */}
                <div className="flex items-center gap-4 text-xs text-muted-foreground/60">
                  <Badge
                    variant="outline"
                    className={cn("text-xs font-medium", statusBadgeClass[wh.status])}
                  >
                    {wh.status}
                  </Badge>
                  <span>
                    Updated{" "}
                    {formatDistanceToNow(parseISO(wh.updatedAt), { addSuffix: true })}
                  </span>
                </div>

                {/* Test result banner */}
                {testResult && (
                  <div
                    className={cn(
                      "flex items-center gap-2 rounded-md px-3 py-2 text-xs",
                      testResult.success
                        ? "border border-emerald-500/30 bg-emerald-500/10 text-emerald-300"
                        : "border border-red-500/30 bg-red-500/10 text-red-300",
                    )}
                  >
                    {testResult.success ? (
                      <CheckCircle2 className="h-3.5 w-3.5" />
                    ) : (
                      <XCircle className="h-3.5 w-3.5" />
                    )}
                    {testResult.success
                      ? `Success (${testResult.statusCode}) in ${testResult.responseTimeMs}ms`
                      : `Failed: ${testResult.error ?? `Status ${testResult.statusCode}`}`}
                  </div>
                )}

                {/* Actions */}
                <div className="flex items-center gap-1 border-t border-border pt-3">
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 text-xs text-muted-foreground hover:text-foreground"
                    onClick={() => {
                      setEditWebhook(wh);
                      setFormOpen(true);
                    }}
                  >
                    <Pencil className="mr-1 h-3 w-3" />
                    Edit
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 text-xs text-muted-foreground hover:text-foreground"
                    onClick={() => handleTest(wh.id)}
                    disabled={testMutation.isPending}
                  >
                    {testMutation.isPending ? (
                      <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                    ) : (
                      <Play className="mr-1 h-3 w-3" />
                    )}
                    Test
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 text-xs text-muted-foreground hover:text-foreground"
                    onClick={() =>
                      setLogsWebhookId(logsWebhookId === wh.id ? null : wh.id)
                    }
                  >
                    <ScrollText className="mr-1 h-3 w-3" />
                    Logs
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 text-xs text-red-400/70 hover:text-red-400"
                    onClick={() => setDeleteWebhook(wh)}
                  >
                    <Trash2 className="mr-1 h-3 w-3" />
                    Delete
                  </Button>
                </div>

                {/* Delivery logs (expandable) */}
                {logsWebhookId === wh.id && (
                  <DeliveryLogs webhookId={wh.id} />
                )}
              </div>
            );
          })}
        </div>
      )}

      <WebhookFormSheet
        open={formOpen}
        onOpenChange={setFormOpen}
        webhook={editWebhook}
      />

      <ConfirmDialog
        open={!!deleteWebhook}
        onOpenChange={() => setDeleteWebhook(null)}
        title="Delete Webhook"
        description={`This will permanently delete the webhook for "${deleteWebhook?.url ?? ""}". All delivery logs will be lost. This cannot be undone.`}
        confirmLabel="Delete Webhook"
        variant="destructive"
        loading={deleteMutation.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
