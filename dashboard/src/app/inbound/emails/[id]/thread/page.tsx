"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { ArrowRight, ArrowLeft, Paperclip } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/shared/page-header";
import { DetailSkeleton } from "@/components/shared/loading-skeleton";
import { useInboundEmail } from "@/lib/hooks/use-inbound";
import { Routes } from "@/lib/constants/routes";
import { cn } from "@/lib/utils";

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleString();
}

function truncate(text: string, max: number): string {
  if (text.length <= max) return text;
  return text.slice(0, max) + "...";
}

interface ThreadEmail {
  id: string;
  direction: "sent" | "received";
  from: string;
  to: string;
  subject: string;
  bodyPreview: string;
  timestamp: string;
  attachmentCount: number;
  detailHref: string;
}

export default function InboundThreadPage() {
  const params = useParams();
  const id = params.id as string;

  const { data: email, isLoading } = useInboundEmail(id);

  if (isLoading || !email) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Thread"
          backHref={Routes.INBOUND_EMAIL_DETAIL(id)}
          backLabel="Back to Email"
        />
        <DetailSkeleton />
      </div>
    );
  }

  // Build thread from the inbound email and its linked outbound email
  const threadEmails: ThreadEmail[] = [];

  // If there's an outbound email this is a reply to, add it first
  if (email.outboundEmailId) {
    threadEmails.push({
      id: email.outboundEmailId,
      direction: "sent",
      from: email.toEmails[0]?.email ?? "you",
      to: email.fromEmail,
      subject: email.subject ? `Re: ${email.subject}` : "(no subject)",
      bodyPreview: "Original outbound email",
      timestamp: email.createdAt, // Approximation
      attachmentCount: 0,
      detailHref: Routes.EMAIL_DETAIL(email.outboundEmailId),
    });
  }

  // Add the inbound email
  threadEmails.push({
    id: email.id,
    direction: "received",
    from: email.fromName
      ? `${email.fromName} <${email.fromEmail}>`
      : email.fromEmail,
    to: email.toEmails.map((r) => r.email).join(", "),
    subject: email.subject || "(no subject)",
    bodyPreview: truncate(email.textBody || email.htmlBody?.replace(/<[^>]+>/g, "") || "", 200),
    timestamp: email.receivedAt,
    attachmentCount: email.attachments.length,
    detailHref: Routes.INBOUND_EMAIL_DETAIL(email.id),
  });

  const participantEmails = new Set<string>();
  threadEmails.forEach((e) => {
    participantEmails.add(e.from.split("<").pop()?.replace(">", "").trim() || e.from);
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Thread: ${email.subject || "(no subject)"}`}
        description={`${threadEmails.length} email${threadEmails.length !== 1 ? "s" : ""} · ${participantEmails.size} participant${participantEmails.size !== 1 ? "s" : ""}`}
        backHref={Routes.INBOUND_EMAIL_DETAIL(id)}
        backLabel="Back to Email"
      />

      {/* Vertical Timeline */}
      <div className="relative ml-4">
        {/* Connector line */}
        <div className="absolute bottom-0 left-4 top-0 w-px bg-muted" />

        <div className="space-y-4">
          {threadEmails.map((threadEmail, index) => {
            const isSent = threadEmail.direction === "sent";
            return (
              <div key={threadEmail.id} className="relative pl-12">
                {/* Dot on timeline */}
                <div
                  className={cn(
                    "absolute left-[11px] top-4 h-3 w-3 rounded-full border-2",
                    isSent
                      ? "border-blue-400 bg-blue-400/30"
                      : "border-emerald-400 bg-emerald-400/30",
                  )}
                />

                {/* Card */}
                <Link
                  href={threadEmail.detailHref}
                  className="block rounded-lg border border-border bg-card p-4 transition-colors hover:border-border"
                >
                  <div className="mb-2 flex items-center gap-2">
                    <Badge
                      variant="outline"
                      className={cn(
                        "text-xs font-medium",
                        isSent
                          ? "border-blue-500/30 bg-blue-500/15 text-blue-400"
                          : "border-emerald-500/30 bg-emerald-500/15 text-emerald-400",
                      )}
                    >
                      {isSent ? (
                        <span className="flex items-center gap-1">
                          <ArrowRight className="h-3 w-3" /> Sent
                        </span>
                      ) : (
                        <span className="flex items-center gap-1">
                          <ArrowLeft className="h-3 w-3" /> Received
                        </span>
                      )}
                    </Badge>
                    <span className="text-xs text-muted-foreground/60">
                      {formatDate(threadEmail.timestamp)}
                    </span>
                  </div>

                  <div className="mb-1 text-sm text-foreground/80">
                    <span className="text-muted-foreground/60">From:</span>{" "}
                    <span className="text-foreground">{threadEmail.from}</span>
                  </div>
                  <div className="mb-2 text-sm text-foreground/80">
                    <span className="text-muted-foreground/60">To:</span>{" "}
                    {threadEmail.to}
                  </div>

                  {threadEmail.bodyPreview && (
                    <p className="text-sm text-muted-foreground">
                      {threadEmail.bodyPreview}
                    </p>
                  )}

                  {threadEmail.attachmentCount > 0 && (
                    <div className="mt-2 flex items-center gap-1 text-xs text-muted-foreground/60">
                      <Paperclip className="h-3 w-3" />
                      {threadEmail.attachmentCount} attachment
                      {threadEmail.attachmentCount !== 1 ? "s" : ""}
                    </div>
                  )}
                </Link>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
