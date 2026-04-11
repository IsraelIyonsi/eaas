"use client";

import { useState } from "react";
import { PageHeader } from "@/components/shared/page-header";
import { AlertCard } from "@/components/notifications/alert-card";
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
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { useDomains } from "@/lib/hooks/use-domains";
import { toast } from "sonner";
import { Bell, TrendingUp, XCircle, ShieldAlert, Loader2 } from "lucide-react";

const intervalOptions = [
  { value: "5", label: "5 minutes" },
  { value: "15", label: "15 minutes" },
  { value: "30", label: "30 minutes" },
  { value: "60", label: "1 hour" },
];

export default function NotificationsPage() {
  const { data: domains } = useDomains();
  const domainList = Array.isArray(domains) ? domains : [];

  // Volume Spike
  const [volumeEnabled, setVolumeEnabled] = useState(false);
  const [volumeThreshold, setVolumeThreshold] = useState("100");
  const [volumeInterval, setVolumeInterval] = useState("15");

  // Processing Failures
  const [failureEnabled, setFailureEnabled] = useState(false);
  const [failureThreshold, setFailureThreshold] = useState("10");
  const [failureInterval, setFailureInterval] = useState("15");

  // Spam Threshold
  const [spamEnabled, setSpamEnabled] = useState(false);
  const [spamThreshold, setSpamThreshold] = useState("5.0");

  // Notification channel
  const [channel, setChannel] = useState<"webhook" | "email">("webhook");
  const [webhookUrl, setWebhookUrl] = useState("");
  const [saving, setSaving] = useState(false);

  function handleSave() {
    setSaving(true);
    // Simulated save
    setTimeout(() => {
      setSaving(false);
      toast.success("Notification preferences saved");
    }, 600);
  }

  function handleTest() {
    toast.info("Test alert sent to your configured channel");
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Notification Preferences"
        description="Configure alerts for volume spikes, processing failures, and spam thresholds."
      />

      {/* Domain selector */}
      {domainList.length > 1 && (
        <Tabs defaultValue="all">
          <TabsList className="border-border bg-muted">
            <TabsTrigger value="all" className="text-xs">
              All Domains
            </TabsTrigger>
            {domainList.map((d) => (
              <TabsTrigger key={d.id} value={d.id} className="text-xs">
                {d.domainName}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>
      )}

      {/* Alert Cards */}
      <div className="space-y-4">
        {/* Volume Spike */}
        <AlertCard
          title="Volume Spike Alert"
          description="Get notified when inbound email volume exceeds a threshold."
          enabled={volumeEnabled}
          onToggle={setVolumeEnabled}
        >
          <div className="flex items-center gap-3">
            <Label className="shrink-0 text-xs text-muted-foreground">
              Alert when received &gt;
            </Label>
            <Input
              type="number"
              value={volumeThreshold}
              onChange={(e) => setVolumeThreshold(e.target.value)}
              className="w-20 border-border bg-muted text-center text-sm text-foreground"
            />
            <Label className="shrink-0 text-xs text-muted-foreground">in</Label>
            <Select value={volumeInterval} onValueChange={(v) => v && setVolumeInterval(v)}>
              <SelectTrigger className="w-[140px] border-border bg-muted text-sm text-foreground">
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="border-border bg-muted">
                {intervalOptions.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </AlertCard>

        {/* Processing Failures */}
        <AlertCard
          title="Processing Failure Alert"
          description="Get notified when email processing failures exceed a threshold."
          enabled={failureEnabled}
          onToggle={setFailureEnabled}
        >
          <div className="flex items-center gap-3">
            <Label className="shrink-0 text-xs text-muted-foreground">
              Alert when failed &gt;
            </Label>
            <Input
              type="number"
              value={failureThreshold}
              onChange={(e) => setFailureThreshold(e.target.value)}
              className="w-20 border-border bg-muted text-center text-sm text-foreground"
            />
            <Label className="shrink-0 text-xs text-muted-foreground">in</Label>
            <Select value={failureInterval} onValueChange={(v) => v && setFailureInterval(v)}>
              <SelectTrigger className="w-[140px] border-border bg-muted text-sm text-foreground">
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="border-border bg-muted">
                {intervalOptions.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </AlertCard>

        {/* Spam Threshold */}
        <AlertCard
          title="Spam Threshold Alert"
          description="Get notified when spam score exceeds a threshold."
          enabled={spamEnabled}
          onToggle={setSpamEnabled}
        >
          <div className="flex items-center gap-3">
            <Label className="shrink-0 text-xs text-muted-foreground">
              Alert when spam score &gt;
            </Label>
            <Input
              type="number"
              step="0.1"
              value={spamThreshold}
              onChange={(e) => setSpamThreshold(e.target.value)}
              className="w-20 border-border bg-muted text-center text-sm text-foreground"
            />
          </div>
        </AlertCard>
      </div>

      {/* Notification Channel */}
      <div className="rounded-lg border border-border bg-card p-5 space-y-4">
        <h3 className="text-sm font-semibold text-foreground">
          Notification Channel
        </h3>

        <div className="flex items-center gap-6">
          <label className="flex cursor-pointer items-center gap-2 text-sm text-foreground/80">
            <input
              type="radio"
              name="channel"
              checked={channel === "webhook"}
              onChange={() => setChannel("webhook")}
              className="accent-[var(--primary)]"
            />
            Webhook
          </label>
          <label className="flex cursor-pointer items-center gap-2 text-sm text-foreground/80">
            <input
              type="radio"
              name="channel"
              checked={channel === "email"}
              onChange={() => setChannel("email")}
              className="accent-[var(--primary)]"
            />
            Email
          </label>
        </div>

        {channel === "webhook" && (
          <div className="space-y-2">
            <Label className="text-foreground/80">Webhook URL</Label>
            <Input
              value={webhookUrl}
              onChange={(e) => setWebhookUrl(e.target.value)}
              placeholder="https://api.example.com/alerts"
              className="border-border bg-muted text-foreground"
            />
          </div>
        )}

        <div className="flex items-center gap-3">
          <Button
            variant="outline"
            size="sm"
            onClick={handleTest}
            className="border-border text-muted-foreground hover:bg-muted hover:text-foreground"
          >
            Test Alert
          </Button>
        </div>
      </div>

      {/* Save */}
      <div className="flex justify-end">
        <Button
          onClick={handleSave}
          disabled={saving}
          className="bg-primary text-primary-foreground hover:bg-primary/90"
        >
          {saving && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
          Save Preferences
        </Button>
      </div>
    </div>
  );
}
