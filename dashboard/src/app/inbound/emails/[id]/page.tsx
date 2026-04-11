"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { toast } from "sonner";
import {
  Download,
  RefreshCw,
  Trash2,
  ExternalLink,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { PageHeader } from "@/components/shared/page-header";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { DetailSkeleton } from "@/components/shared/loading-skeleton";
import { SecurityVerdicts } from "@/components/inbound/security-verdicts";
import { AttachmentList } from "@/components/inbound/attachment-list";
import {
  useInboundEmail,
  useRetryWebhook,
  useDeleteInboundEmail,
} from "@/lib/hooks/use-inbound";
import { repositories } from "@/lib/api/index";
import { Routes } from "@/lib/constants/routes";
import { InboundEmailStatusConfig } from "@/lib/constants/status";
import { cn } from "@/lib/utils";
import type { WebhookDeliveryLog } from "@/types/inbound";

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleString();
}

function InfoRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 sm:flex-row sm:gap-4">
      <span className="w-24 shrink-0 text-xs font-medium text-muted-foreground/60">
        {label}
      </span>
      <span className="text-sm text-foreground">{children}</span>
    </div>
  );
}

export default function InboundEmailDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const { data: email, isLoading } = useInboundEmail(id);
  const retryWebhook = useRetryWebhook();
  const deleteEmail = useDeleteInboundEmail();

  const [deleteOpen, setDeleteOpen] = useState(false);

  if (isLoading || !email) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Email Detail"
          backHref={Routes.INBOUND_EMAILS}
          backLabel="Back to Received"
        />
        <DetailSkeleton />
      </div>
    );
  }

  const statusConfig = InboundEmailStatusConfig[email.status];

  async function handleDownloadRaw() {
    try {
      const url = await repositories.inboundEmail.getRawUrl(id);
      window.open(url, "_blank");
    } catch {
      toast.error("Failed to download raw MIME");
    }
  }

  async function handleRetryWebhook() {
    try {
      await retryWebhook.mutateAsync(id);
      toast.success("Webhook retry initiated");
    } catch {
      toast.error("Failed to retry webhook");
    }
  }

  async function handleDelete() {
    try {
      await deleteEmail.mutateAsync(id);
      toast.success("Email deleted");
      router.push(Routes.INBOUND_EMAILS);
    } catch {
      toast.error("Failed to delete email");
    }
  }

  // Mock webhook delivery logs (would come from API in production)
  const webhookLogs: WebhookDeliveryLog[] = [];

  return (
    <div className="space-y-6">
      <PageHeader
        title={email.subject || "(no subject)"}
        backHref={Routes.INBOUND_EMAILS}
        backLabel="Back to Received"
        badge={statusConfig.label}
        action={
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleDownloadRaw}
              className="border-border text-muted-foreground hover:bg-muted"
            >
              <Download className="mr-1.5 h-3.5 w-3.5" />
              Raw MIME
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={handleRetryWebhook}
              disabled={retryWebhook.isPending}
              className="border-border text-muted-foreground hover:bg-muted"
            >
              <RefreshCw
                className={cn(
                  "mr-1.5 h-3.5 w-3.5",
                  retryWebhook.isPending && "animate-spin",
                )}
              />
              Retry Webhook
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setDeleteOpen(true)}
              className="border-red-500/20 text-red-400 hover:bg-red-500/10"
            >
              <Trash2 className="mr-1.5 h-3.5 w-3.5" />
              Delete
            </Button>
          </div>
        }
      />

      {/* Email Info Card */}
      <div className="rounded-lg border border-border bg-card p-5">
        <div className="space-y-3">
          <InfoRow label="From">
            {email.fromName && (
              <span className="mr-1 font-medium text-foreground">
                {email.fromName}
              </span>
            )}
            &lt;{email.fromEmail}&gt;
          </InfoRow>
          <InfoRow label="To">
            {email.toEmails.map((r) => r.email).join(", ")}
          </InfoRow>
          {email.ccEmails.length > 0 && (
            <InfoRow label="CC">
              {email.ccEmails.map((r) => r.email).join(", ")}
            </InfoRow>
          )}
          <InfoRow label="Subject">{email.subject || "(no subject)"}</InfoRow>
          <InfoRow label="Received">{formatDate(email.receivedAt)}</InfoRow>
          {email.processedAt && (
            <InfoRow label="Processed">{formatDate(email.processedAt)}</InfoRow>
          )}
        </div>
      </div>

      {/* Security Verdicts */}
      <div className="rounded-lg border border-border bg-card p-5">
        <h3 className="mb-3 text-sm font-semibold text-foreground">
          Security Verdicts
        </h3>
        <SecurityVerdicts
          spf={email.spfVerdict}
          dkim={email.dkimVerdict}
          dmarc={email.dmarcVerdict}
          spam={email.spamVerdict}
          virus={email.virusVerdict}
        />
      </div>

      {/* Thread link */}
      {email.outbound_emailId && (
        <div className="rounded-lg border border-border bg-card p-4">
          <div className="flex items-center gap-2">
            <span className="text-sm text-muted-foreground">Reply to:</span>
            <Link
              href={Routes.EMAIL_DETAIL(email.outbound_emailId)}
              className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
            >
              Original email
              <ExternalLink className="h-3 w-3" />
            </Link>
            <span className="text-muted-foreground/40">|</span>
            <Link
              href={Routes.INBOUND_THREAD(email.id)}
              className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
            >
              View thread
              <ExternalLink className="h-3 w-3" />
            </Link>
          </div>
        </div>
      )}

      {/* Body Tabs */}
      <div className="rounded-lg border border-border bg-card">
        <Tabs defaultValue="html">
          <div className="border-b border-border px-4 pt-3">
            <TabsList variant="line">
              <TabsTrigger value="html" className="text-muted-foreground data-active:text-foreground">
                HTML Preview
              </TabsTrigger>
              <TabsTrigger value="text" className="text-muted-foreground data-active:text-foreground">
                Plain Text
              </TabsTrigger>
              <TabsTrigger value="headers" className="text-muted-foreground data-active:text-foreground">
                Headers
              </TabsTrigger>
              <TabsTrigger value="json" className="text-muted-foreground data-active:text-foreground">
                JSON
              </TabsTrigger>
            </TabsList>
          </div>

          <TabsContent value="html" className="p-4">
            {email.htmlBody ? (
              <div className="rounded-md border border-border bg-white p-4">
                <iframe
                  srcDoc={email.htmlBody}
                  className="h-[400px] w-full border-0"
                  title="Email HTML Preview"
                  sandbox=""
                />
              </div>
            ) : (
              <p className="text-sm text-muted-foreground/60">No HTML body available.</p>
            )}
          </TabsContent>

          <TabsContent value="text" className="p-4">
            {email.textBody ? (
              <pre className="max-h-[400px] overflow-auto whitespace-pre-wrap rounded-md bg-muted p-4 text-sm text-foreground/80">
                {email.textBody}
              </pre>
            ) : (
              <p className="text-sm text-muted-foreground/60">No plain text body available.</p>
            )}
          </TabsContent>

          <TabsContent value="headers" className="p-4">
            {email.headers && Object.keys(email.headers).length > 0 ? (
              <div className="max-h-[400px] overflow-auto">
                <table className="w-full text-sm">
                  <tbody>
                    {Object.entries(email.headers).map(([key, value]) => (
                      <tr key={key} className="border-b border-border">
                        <td className="whitespace-nowrap py-1.5 pr-4 font-mono text-xs text-primary">
                          {key}
                        </td>
                        <td className="break-all py-1.5 text-xs text-muted-foreground">
                          {value}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground/60">No headers available.</p>
            )}
          </TabsContent>

          <TabsContent value="json" className="p-4">
            <pre className="max-h-[400px] overflow-auto rounded-md bg-muted p-4 font-mono text-xs text-muted-foreground">
              {JSON.stringify(email, null, 2)}
            </pre>
          </TabsContent>
        </Tabs>
      </div>

      {/* Attachments */}
      {email.attachments.length > 0 && (
        <div className="rounded-lg border border-border bg-card p-5">
          <AttachmentList attachments={email.attachments} emailId={email.id} />
        </div>
      )}

      {/* Webhook Delivery Log */}
      {webhookLogs.length > 0 && (
        <div className="rounded-lg border border-border bg-card p-5">
          <h3 className="mb-3 text-sm font-semibold text-foreground">
            Webhook Delivery Log
          </h3>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-muted-foreground">
                <th className="pb-2 text-left text-xs font-medium">Attempt</th>
                <th className="pb-2 text-left text-xs font-medium">Status</th>
                <th className="pb-2 text-left text-xs font-medium">Response Time</th>
                <th className="pb-2 text-left text-xs font-medium">Timestamp</th>
              </tr>
            </thead>
            <tbody>
              {webhookLogs.map((log) => (
                <tr key={log.id} className="border-b border-border">
                  <td className="py-2 text-muted-foreground">#{log.attempt}</td>
                  <td className="py-2">
                    <Badge
                      variant="outline"
                      className={cn(
                        "text-xs",
                        log.statusCode >= 200 && log.statusCode < 300
                          ? "border-emerald-500/30 bg-emerald-500/15 text-emerald-400"
                          : "border-red-500/30 bg-red-500/15 text-red-400",
                      )}
                    >
                      {log.statusCode}
                    </Badge>
                  </td>
                  <td className="py-2 text-muted-foreground">{log.responseTimeMs}ms</td>
                  <td className="py-2 text-muted-foreground">{formatDate(log.timestamp)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Confirm Delete Dialog */}
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete Inbound Email"
        description="This will permanently delete this inbound email and all associated data. This action cannot be undone."
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteEmail.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
