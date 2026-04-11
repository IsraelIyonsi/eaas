"use client";

import { Paperclip, Download } from "lucide-react";
import { Button } from "@/components/ui/button";
import { repositories } from "@/lib/api/index";
import type { InboundAttachment } from "@/types/inbound";

interface AttachmentListProps {
  attachments: InboundAttachment[];
  emailId: string;
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return "0 B";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function AttachmentList({ attachments, emailId }: AttachmentListProps) {
  if (attachments.length === 0) return null;

  async function handleDownload(attachment: InboundAttachment) {
    try {
      const url = await repositories.inboundEmail.getAttachmentUrl(
        emailId,
        attachment.id,
      );
      window.open(url, "_blank");
    } catch {
      // Silently fail - toast could be added
    }
  }

  return (
    <div className="space-y-2">
      <h3 className="text-sm font-medium text-foreground/80">
        Attachments ({attachments.length})
      </h3>
      <div className="space-y-1">
        {attachments.map((attachment) => (
          <div
            key={attachment.id}
            className="flex items-center justify-between rounded-md border border-border bg-muted px-3 py-2"
          >
            <div className="flex items-center gap-2 overflow-hidden">
              <Paperclip className="h-4 w-4 shrink-0 text-muted-foreground/60" />
              <span className="truncate text-sm text-foreground">
                {attachment.filename}
              </span>
              <span className="shrink-0 text-xs text-muted-foreground/60">
                {attachment.contentType}
              </span>
              <span className="shrink-0 text-xs text-muted-foreground/60">
                {formatBytes(attachment.sizeBytes)}
              </span>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => handleDownload(attachment)}
              className="shrink-0 text-muted-foreground hover:text-foreground"
            >
              <Download className="h-3.5 w-3.5" />
            </Button>
          </div>
        ))}
      </div>
    </div>
  );
}
