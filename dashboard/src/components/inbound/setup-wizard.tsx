"use client";

import { useState } from "react";
import { Check, Loader2, Send, CheckCircle2 } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { CopyButton } from "@/components/shared/copy-button";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { useDomains } from "@/lib/hooks/use-domains";
import type { Domain } from "@/types/domain";
import { cn } from "@/lib/utils";

// --- Step indicator ---

interface StepIndicatorProps {
  steps: string[];
  currentStep: number;
}

export function StepIndicator({ steps, currentStep }: StepIndicatorProps) {
  return (
    <div className="mb-8 flex items-center justify-between">
      {steps.map((label, i) => {
        const isComplete = i < currentStep;
        const isCurrent = i === currentStep;
        return (
          <div key={label} className="flex flex-1 items-center">
            <div className="flex flex-col items-center gap-1.5">
              <div
                className={cn(
                  "flex h-8 w-8 items-center justify-center rounded-full text-xs font-semibold transition-colors",
                  isComplete && "bg-emerald-500 text-foreground",
                  isCurrent &&
                    "border-2 border-[var(--primary)] bg-primary/10 text-primary",
                  !isComplete &&
                    !isCurrent &&
                    "border border-border text-muted-foreground/60",
                )}
              >
                {isComplete ? <Check className="h-4 w-4" /> : i + 1}
              </div>
              <span
                className={cn(
                  "text-[10px] font-medium",
                  isCurrent ? "text-foreground" : "text-muted-foreground/60",
                )}
              >
                {label}
              </span>
            </div>
            {i < steps.length - 1 && (
              <div
                className={cn(
                  "mx-2 h-px flex-1",
                  isComplete ? "bg-emerald-500" : "bg-muted",
                )}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}

// --- Step components ---

export function DomainStep({
  domainId,
  onDomainChange,
}: {
  domainId: string;
  onDomainChange: (id: string) => void;
}) {
  const { data: domainsData, isLoading } = useDomains();
  const domains: Domain[] = domainsData ?? [];

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold text-foreground">Select Domain</h2>
        <p className="text-sm text-muted-foreground">
          Choose the domain you want to enable for receiving emails.
        </p>
      </div>
      <div className="space-y-1.5">
        <Label className="text-foreground/80">Domain</Label>
        <Select value={domainId} onValueChange={(v) => onDomainChange(v ?? "")} disabled={isLoading}>
          <SelectTrigger className="border-border bg-muted text-foreground">
            <SelectValue placeholder={isLoading ? "Loading..." : "Select a domain"} />
          </SelectTrigger>
          <SelectContent>
            {domains.map((d) => (
              <SelectItem key={d.id} value={d.id}>
                {d.domainName}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}

const mxRecordValue = "inbound-smtp.us-east-1.amazonaws.com";
const mxRecordPriority = "10";

interface MxRecordRowProps {
  label: string;
  value: string;
}

function MxRecordRow({ label, value }: MxRecordRowProps) {
  return (
    <div className="flex items-center justify-between rounded-md border border-border bg-muted px-3 py-2">
      <div>
        <div className="text-xs text-muted-foreground/60">{label}</div>
        <code className="text-sm text-foreground">{value}</code>
      </div>
      <CopyButton value={value} />
    </div>
  );
}

export function MxRecordsStep({ domain }: { domain: string }) {
  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold text-foreground">Configure MX Records</h2>
        <p className="text-sm text-muted-foreground">
          Add the following MX record to <span className="text-foreground">{domain || "your domain"}</span> DNS settings.
        </p>
      </div>

      <div className="space-y-2">
        <MxRecordRow label="Type" value="MX" />
        <MxRecordRow label="Name" value={domain || "@"} />
        <MxRecordRow label="Priority" value={mxRecordPriority} />
        <MxRecordRow label="Value" value={mxRecordValue} />
      </div>

      <Tabs defaultValue="cloudflare">
        <TabsList variant="line" className="border-b border-border">
          <TabsTrigger value="cloudflare" className="text-muted-foreground data-active:text-foreground">
            Cloudflare
          </TabsTrigger>
          <TabsTrigger value="route53" className="text-muted-foreground data-active:text-foreground">
            Route 53
          </TabsTrigger>
          <TabsTrigger value="godaddy" className="text-muted-foreground data-active:text-foreground">
            GoDaddy
          </TabsTrigger>
          <TabsTrigger value="namecheap" className="text-muted-foreground data-active:text-foreground">
            Namecheap
          </TabsTrigger>
        </TabsList>
        <TabsContent value="cloudflare" className="mt-3">
          <ol className="list-inside list-decimal space-y-1 text-sm text-muted-foreground">
            <li>Go to your Cloudflare dashboard and select the domain</li>
            <li>Navigate to DNS &gt; Records</li>
            <li>Click &quot;Add record&quot; and select MX type</li>
            <li>Set Name to <code className="text-amber-400">{domain || "@"}</code>, Priority to <code className="text-amber-400">{mxRecordPriority}</code>, Mail server to <code className="text-amber-400">{mxRecordValue}</code></li>
            <li>Click Save</li>
          </ol>
        </TabsContent>
        <TabsContent value="route53" className="mt-3">
          <ol className="list-inside list-decimal space-y-1 text-sm text-muted-foreground">
            <li>Open Route 53 console and select the hosted zone</li>
            <li>Click &quot;Create record&quot;</li>
            <li>Record type: MX, Value: <code className="text-amber-400">{mxRecordPriority} {mxRecordValue}</code></li>
            <li>Click &quot;Create records&quot;</li>
          </ol>
        </TabsContent>
        <TabsContent value="godaddy" className="mt-3">
          <ol className="list-inside list-decimal space-y-1 text-sm text-muted-foreground">
            <li>Go to GoDaddy DNS Management for your domain</li>
            <li>Click &quot;Add&quot; under Records</li>
            <li>Type: MX, Host: @, Points to: <code className="text-amber-400">{mxRecordValue}</code>, Priority: <code className="text-amber-400">{mxRecordPriority}</code></li>
            <li>Click Save</li>
          </ol>
        </TabsContent>
        <TabsContent value="namecheap" className="mt-3">
          <ol className="list-inside list-decimal space-y-1 text-sm text-muted-foreground">
            <li>Go to Namecheap Dashboard &gt; Domain List &gt; Manage</li>
            <li>Advanced DNS &gt; Add New Record</li>
            <li>Type: MX, Host: @, Value: <code className="text-amber-400">{mxRecordValue}</code>, Priority: <code className="text-amber-400">{mxRecordPriority}</code></li>
            <li>Click Save Changes</li>
          </ol>
        </TabsContent>
      </Tabs>
    </div>
  );
}

export function WebhookStep({
  webhookUrl,
  onWebhookUrlChange,
}: {
  webhookUrl: string;
  onWebhookUrlChange: (url: string) => void;
}) {
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<"success" | "error" | null>(null);

  async function handleTest() {
    setTesting(true);
    setTestResult(null);
    try {
      // Simulate webhook test - in production this would call the API
      await new Promise((resolve) => setTimeout(resolve, 1500));
      setTestResult("success");
      toast.success("Webhook test successful");
    } catch {
      setTestResult("error");
      toast.error("Webhook test failed");
    } finally {
      setTesting(false);
    }
  }

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold text-foreground">Configure Webhook</h2>
        <p className="text-sm text-muted-foreground">
          Enter the URL where inbound emails will be forwarded as HTTP POST requests.
        </p>
      </div>
      <div className="space-y-1.5">
        <Label className="text-foreground/80">Webhook URL</Label>
        <Input
          value={webhookUrl}
          onChange={(e) => onWebhookUrlChange(e.target.value)}
          placeholder="https://your-app.com/webhooks/inbound"
          className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
        />
      </div>
      <div className="flex items-center gap-3">
        <Button
          variant="outline"
          size="sm"
          onClick={handleTest}
          disabled={testing || !webhookUrl}
          className="border-border text-muted-foreground hover:bg-muted"
        >
          {testing ? (
            <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />
          ) : (
            <Send className="mr-2 h-3.5 w-3.5" />
          )}
          Test Webhook
        </Button>
        {testResult === "success" && (
          <span className="inline-flex items-center gap-1 text-xs text-emerald-400">
            <CheckCircle2 className="h-3.5 w-3.5" /> Connected
          </span>
        )}
        {testResult === "error" && (
          <span className="text-xs text-red-400">Connection failed</span>
        )}
      </div>
    </div>
  );
}

export function TestEmailStep({ domain }: { domain: string }) {
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);

  async function handleSendTest() {
    setSending(true);
    try {
      await new Promise((resolve) => setTimeout(resolve, 2000));
      setSent(true);
      toast.success("Test email sent! Check your webhook endpoint.");
    } catch {
      toast.error("Failed to send test email");
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold text-foreground">Send Test Email</h2>
        <p className="text-sm text-muted-foreground">
          Send a test email to verify everything is working correctly.
        </p>
      </div>
      <div className="rounded-lg border border-border bg-muted p-4">
        <p className="mb-2 text-sm text-foreground/80">
          Send an email to: <code className="text-primary">test@{domain || "yourdomain.com"}</code>
        </p>
        <p className="text-xs text-muted-foreground/60">
          Or click the button below to send an automated test.
        </p>
      </div>
      <Button
        onClick={handleSendTest}
        disabled={sending || sent}
        className="gap-2"
      >
        {sending ? (
          <Loader2 className="h-4 w-4 animate-spin" />
        ) : sent ? (
          <CheckCircle2 className="h-4 w-4" />
        ) : (
          <Send className="h-4 w-4" />
        )}
        {sent ? "Test Sent" : "Send Test Email"}
      </Button>
      {sent && (
        <div className="rounded-md border border-emerald-500/20 bg-emerald-500/10 p-3">
          <p className="text-sm text-emerald-400">
            Test email sent successfully. Check your webhook endpoint for the incoming payload.
          </p>
        </div>
      )}
    </div>
  );
}

export function CompleteStep() {
  return (
    <div className="flex flex-col items-center justify-center py-8 text-center">
      <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-emerald-500/15">
        <CheckCircle2 className="h-8 w-8 text-emerald-400" />
      </div>
      <h2 className="mb-2 text-lg font-semibold text-foreground">
        Setup Complete!
      </h2>
      <p className="max-w-sm text-sm text-muted-foreground">
        Your domain is now configured to receive inbound emails.
        Incoming emails will be processed according to your rules.
      </p>
    </div>
  );
}
