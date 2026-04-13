"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { PageHeader } from "@/components/shared/page-header";
import { DetailSkeleton } from "@/components/shared/loading-skeleton";
import { EmailStatusBadge } from "@/components/shared/status-badge";
import { CopyButton } from "@/components/shared/copy-button";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { EventTimeline } from "@/components/emails/event-timeline";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { useEmail, useEmailEvents, useDeleteEmail } from "@/lib/hooks/use-emails";
import { Routes } from "@/lib/constants/routes";
import { format, parseISO } from "date-fns";
import { RotateCw, Code, Trash2 } from "lucide-react";
import { toast } from "sonner";

export default function EmailDetailPage() {
  const params = useParams();
  const id = params.id as string;

  const router = useRouter();
  const { data: email, isLoading } = useEmail(id);
  const { data: events } = useEmailEvents(id);
  const deleteEmail = useDeleteEmail();

  const [deleteOpen, setDeleteOpen] = useState(false);

  if (isLoading || !email) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Email Details"
          backHref={Routes.EMAILS}
          backLabel="Back to Emails"
        />
        <DetailSkeleton />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={email.subject || "(No Subject)"}
        backHref={Routes.EMAILS}
        backLabel="Back to Emails"
        badge={email.status}
      />

      {/* Email Info */}
      <Card className="border-border bg-card shadow-none">
        <CardContent className="p-5">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Status</p>
              <EmailStatusBadge status={email.status} />
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">From</p>
              <p className="text-sm text-foreground">{email.from}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">To</p>
              <p className="text-sm text-foreground">{email.to}</p>
            </div>
            {email.cc && email.cc.length > 0 && (
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground/60">CC</p>
                <p className="text-sm text-foreground">{email.cc.join(", ")}</p>
              </div>
            )}
            {email.templateName && (
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground/60">Template</p>
                <p className="text-sm text-primary">{email.templateName}</p>
              </div>
            )}
            {email.tags && email.tags.length > 0 && (
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground/60">Tags</p>
                <div className="flex flex-wrap gap-1">
                  {email.tags.map((tag) => (
                    <Badge
                      key={tag}
                      variant="outline"
                      className="border-border bg-muted text-xs text-muted-foreground"
                    >
                      {tag}
                    </Badge>
                  ))}
                </div>
              </div>
            )}
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Message ID</p>
              <div className="flex items-center gap-1">
                <code className="truncate font-mono text-xs text-muted-foreground">
                  {email.messageId}
                </code>
                <CopyButton value={email.messageId} label="Message ID" />
              </div>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Created</p>
              <p className="text-sm text-foreground">
                {format(parseISO(email.createdAt), "MMM d, yyyy HH:mm:ss")}
              </p>
            </div>
            {email.sentAt && (
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground/60">Sent</p>
                <p className="text-sm text-foreground">
                  {format(parseISO(email.sentAt), "MMM d, yyyy HH:mm:ss")}
                </p>
              </div>
            )}
            {email.deliveredAt && (
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground/60">Delivered</p>
                <p className="text-sm text-foreground">
                  {format(parseISO(email.deliveredAt), "MMM d, yyyy HH:mm:ss")}
                </p>
              </div>
            )}
            {email.openedAt && (
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground/60">First Opened</p>
                <p className="text-sm text-foreground">
                  {format(parseISO(email.openedAt), "MMM d, yyyy HH:mm:ss")}
                </p>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Body Tabs */}
      <Card className="border-border bg-card shadow-none">
        <CardHeader>
          <CardTitle className="text-sm font-semibold text-foreground">
            Email Body
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Tabs defaultValue="html">
            <TabsList className="border-border bg-muted">
              <TabsTrigger value="html" className="text-xs">
                HTML Preview
              </TabsTrigger>
              <TabsTrigger value="text" className="text-xs">
                Plain Text
              </TabsTrigger>
              <TabsTrigger value="raw" className="text-xs">
                Raw Headers
              </TabsTrigger>
            </TabsList>
            <TabsContent value="html">
              <div className="mt-4 rounded-md border border-border bg-white p-4">
                {email.htmlBody ? (
                  <iframe
                    srcDoc={email.htmlBody}
                    title="HTML Preview"
                    className="h-[400px] w-full border-0"
                    sandbox=""
                  />
                ) : (
                  <p className="text-sm text-gray-500">No HTML body</p>
                )}
              </div>
            </TabsContent>
            <TabsContent value="text">
              <pre className="mt-4 max-h-[400px] overflow-auto whitespace-pre-wrap rounded-md border border-border bg-muted p-4 font-mono text-xs text-foreground/80">
                {email.textBody || "No plain text body"}
              </pre>
            </TabsContent>
            <TabsContent value="raw">
              <pre className="mt-4 max-h-[400px] overflow-auto whitespace-pre-wrap rounded-md border border-border bg-muted p-4 font-mono text-xs text-muted-foreground">
                Message-ID: {email.messageId}
                {"\n"}From: {email.from}
                {"\n"}To: {email.to}
                {email.cc ? `\nCc: ${email.cc.join(", ")}` : ""}
                {"\n"}Subject: {email.subject}
                {"\n"}Date: {email.createdAt}
              </pre>
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>

      {/* Event Timeline */}
      <Card className="border-border bg-card shadow-none">
        <CardHeader>
          <CardTitle className="text-sm font-semibold text-foreground">
            Event Timeline
          </CardTitle>
        </CardHeader>
        <CardContent>
          <EventTimeline events={events ?? []} />
        </CardContent>
      </Card>

      {/* Actions */}
      <div className="flex items-center gap-3">
        <Button
          variant="outline"
          size="sm"
          disabled
          title="Resend is not yet available"
          className="border-border text-muted-foreground opacity-50"
        >
          <RotateCw className="mr-1.5 h-3.5 w-3.5" />
          Resend
        </Button>
        <Button
          variant="outline"
          size="sm"
          className="border-border text-muted-foreground hover:bg-muted hover:text-foreground"
          onClick={() => toast.info("Raw view coming soon")}
        >
          <Code className="mr-1.5 h-3.5 w-3.5" />
          View Raw
        </Button>
        <Button
          variant="destructive"
          size="sm"
          onClick={() => setDeleteOpen(true)}
        >
          <Trash2 className="mr-1.5 h-3.5 w-3.5" />
          Delete
        </Button>
      </div>

      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete Email"
        description="Are you sure you want to delete this email record? This action cannot be undone."
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteEmail.isPending}
        onConfirm={() => {
          deleteEmail.mutate(id, {
            onSuccess: () => {
              toast.success("Email deleted");
              setDeleteOpen(false);
              router.push(Routes.EMAILS);
            },
            onError: () => {
              toast.error("Failed to delete email");
            },
          });
        }}
      />
    </div>
  );
}
