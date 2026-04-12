"use client";

import { useState } from "react";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetFooter,
} from "@/components/ui/sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { CopyButton } from "@/components/shared/copy-button";
import {
  useCreateWebhook,
  useUpdateWebhook,
} from "@/lib/hooks/use-webhooks";
import { WEBHOOK_EVENT_TYPES } from "@/types/webhook";
import type { Webhook } from "@/types/webhook";
import { toast } from "sonner";
import { Loader2, RefreshCw } from "lucide-react";

interface WebhookFormSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  webhook: Webhook | null;
}

function generateSecret(): string {
  const chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
  let result = "whsec_";
  for (let i = 0; i < 32; i++) {
    result += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return result;
}

export function WebhookFormSheet({
  open,
  onOpenChange,
  webhook,
}: WebhookFormSheetProps) {
  const isEdit = !!webhook;
  const createMutation = useCreateWebhook();
  const updateMutation = useUpdateWebhook();

  const webhookKey = webhook?.id ?? "";
  const [prevWebhookKey, setPrevWebhookKey] = useState(webhookKey);
  const [prevOpen, setPrevOpen] = useState(open);
  const [url, setUrl] = useState("");
  const [events, setEvents] = useState<string[]>([]);
  const [secret, setSecret] = useState("");
  const [active, setActive] = useState(true);

  if (prevOpen !== open || prevWebhookKey !== webhookKey) {
    setPrevOpen(open);
    setPrevWebhookKey(webhookKey);
    if (open) {
      if (webhook) {
        setUrl(webhook.url);
        setEvents([...webhook.events]);
        setSecret(webhook.secret ?? "");
        setActive(webhook.status === "active");
      } else {
        setUrl("");
        setEvents([]);
        setSecret(generateSecret());
        setActive(true);
      }
    }
  }

  function toggleEvent(evt: string) {
    setEvents((prev) =>
      prev.includes(evt) ? prev.filter((e) => e !== evt) : [...prev, evt],
    );
  }

  function handleSubmit() {
    if (isEdit && webhook) {
      updateMutation.mutate(
        {
          id: webhook.id,
          data: {
            url,
            events,
            secret: secret || undefined,
            status: active ? "active" : "inactive",
          },
        },
        {
          onSuccess: () => {
            toast.success("Webhook updated");
            onOpenChange(false);
          },
          onError: () => toast.error("Failed to update webhook"),
        },
      );
    } else {
      createMutation.mutate(
        { url, events, secret: secret || undefined },
        {
          onSuccess: () => {
            toast.success("Webhook created");
            onOpenChange(false);
          },
          onError: () => toast.error("Failed to create webhook"),
        },
      );
    }
  }

  const isPending = createMutation.isPending || updateMutation.isPending;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="border-border bg-card sm:max-w-md">
        <SheetHeader>
          <SheetTitle className="text-foreground">
            {isEdit ? "Edit Webhook" : "Create Webhook"}
          </SheetTitle>
        </SheetHeader>

        <div className="mt-6 space-y-6 overflow-y-auto px-1">
          {/* URL */}
          <div className="space-y-2">
            <Label className="text-foreground/80">Endpoint URL</Label>
            <Input
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder="https://api.example.com/webhooks"
              className="border-border bg-muted text-foreground"
            />
          </div>

          {/* Events */}
          <div className="space-y-3">
            <Label className="text-foreground/80">Events</Label>
            <div className="grid grid-cols-2 gap-2">
              {WEBHOOK_EVENT_TYPES.map((evt) => (
                <label
                  key={evt}
                  className="flex cursor-pointer items-center gap-2 rounded-md border border-border bg-muted/50 px-3 py-2 text-sm text-foreground/80 transition-colors hover:bg-muted"
                >
                  <input
                    type="checkbox"
                    checked={events.includes(evt)}
                    onChange={() => toggleEvent(evt)}
                    className="h-3.5 w-3.5 rounded border-border bg-transparent accent-[var(--primary)]"
                  />
                  <span className="text-xs">{evt}</span>
                </label>
              ))}
            </div>
          </div>

          {/* Secret */}
          <div className="space-y-2">
            <Label className="text-foreground/80">Signing Secret</Label>
            <div className="flex items-center gap-2">
              <div className="flex flex-1 items-center gap-1 rounded-md border border-border bg-muted px-3 py-2">
                <code className="flex-1 truncate font-mono text-xs text-foreground/80">
                  {secret}
                </code>
                <CopyButton value={secret} label="Secret" />
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setSecret(generateSecret())}
                className="border-border bg-transparent text-muted-foreground hover:bg-muted"
              >
                <RefreshCw className="h-3.5 w-3.5" />
              </Button>
            </div>
          </div>

          {/* Status toggle (edit only) */}
          {isEdit && (
            <div className="flex items-center justify-between">
              <Label className="text-foreground/80">Active</Label>
              <Switch checked={active} onCheckedChange={setActive} />
            </div>
          )}
        </div>

        <SheetFooter className="mt-6">
          <Button
            variant="ghost"
            onClick={() => onOpenChange(false)}
            className="text-muted-foreground"
          >
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={!url || events.length === 0 || isPending}
            className="bg-primary text-primary-foreground hover:bg-primary/90"
          >
            {isPending && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
            {isEdit ? "Update Webhook" : "Create Webhook"}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
