"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";

const timezones = [
  "UTC",
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Los_Angeles",
  "Europe/London",
  "Europe/Berlin",
  "Asia/Tokyo",
  "Africa/Lagos",
  "Australia/Sydney",
];

const retentionOptions = [
  { value: "7", label: "7 days" },
  { value: "30", label: "30 days" },
  { value: "90", label: "90 days" },
  { value: "180", label: "180 days" },
  { value: "365", label: "1 year" },
];

export function GeneralForm() {
  const [companyName, setCompanyName] = useState("");
  const [accountEmail, setAccountEmail] = useState("");
  const [timezone, setTimezone] = useState("UTC");
  const [defaultFromName, setDefaultFromName] = useState("");
  const [defaultReplyTo, setDefaultReplyTo] = useState("");
  const [trackOpens, setTrackOpens] = useState(true);
  const [trackClicks, setTrackClicks] = useState(true);
  const [emailLogRetention, setEmailLogRetention] = useState("90");
  const [inboundRetention, setInboundRetention] = useState("30");
  const [webhookLogRetention, setWebhookLogRetention] = useState("30");
  const [saving, setSaving] = useState(false);

  function handleSave() {
    setSaving(true);
    setTimeout(() => {
      setSaving(false);
      toast.success("Settings saved successfully");
    }, 600);
  }

  return (
    <div className="space-y-8">
      {/* Account Info */}
      <section className="space-y-4">
        <h3 className="text-sm font-semibold text-foreground">Account Information</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label className="text-muted-foreground">Company Name</Label>
            <Input
              value={companyName}
              onChange={(e) => setCompanyName(e.target.value)}
              placeholder="Acme Inc."
              className="border-border bg-muted text-foreground"
            />
          </div>
          <div className="space-y-2">
            <Label className="text-muted-foreground">Account Email</Label>
            <Input
              type="email"
              value={accountEmail}
              onChange={(e) => setAccountEmail(e.target.value)}
              placeholder="admin@example.com"
              className="border-border bg-muted text-foreground"
            />
          </div>
          <div className="space-y-2">
            <Label className="text-muted-foreground">Timezone</Label>
            <Select value={timezone} onValueChange={(v) => v && setTimezone(v)}>
              <SelectTrigger className="border-border bg-muted text-foreground">
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="border-border bg-muted">
                {timezones.map((tz) => (
                  <SelectItem key={tz} value={tz}>
                    {tz}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      </section>

      {/* Sending Defaults */}
      <section className="space-y-4">
        <h3 className="text-sm font-semibold text-foreground">Sending Defaults</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label className="text-muted-foreground">Default From Name</Label>
            <Input
              value={defaultFromName}
              onChange={(e) => setDefaultFromName(e.target.value)}
              placeholder="Acme Support"
              className="border-border bg-muted text-foreground"
            />
          </div>
          <div className="space-y-2">
            <Label className="text-muted-foreground">Default Reply-To</Label>
            <Input
              value={defaultReplyTo}
              onChange={(e) => setDefaultReplyTo(e.target.value)}
              placeholder="reply@example.com"
              className="border-border bg-muted text-foreground"
            />
          </div>
        </div>
        <div className="flex items-center gap-6">
          <label className="flex items-center gap-2 text-sm text-muted-foreground">
            <Switch checked={trackOpens} onCheckedChange={setTrackOpens} />
            Track Opens
          </label>
          <label className="flex items-center gap-2 text-sm text-muted-foreground">
            <Switch checked={trackClicks} onCheckedChange={setTrackClicks} />
            Track Clicks
          </label>
        </div>
      </section>

      {/* Rate Limits (read-only) */}
      <section className="space-y-4">
        <h3 className="text-sm font-semibold text-foreground">Rate Limits</h3>
        <div className="grid gap-4 sm:grid-cols-3">
          <div className="rounded-md border border-border bg-muted/50 p-3">
            <p className="text-xs text-muted-foreground/60">Per Second</p>
            <p className="text-lg font-bold text-foreground">
              14 <span className="text-sm font-normal text-muted-foreground">emails/sec</span>
            </p>
          </div>
          <div className="rounded-md border border-border bg-muted/50 p-3">
            <p className="text-xs text-muted-foreground/60">Per Day</p>
            <p className="text-lg font-bold text-foreground">
              50,000 <span className="text-sm font-normal text-muted-foreground">emails/day</span>
            </p>
          </div>
          <div className="rounded-md border border-border bg-muted/50 p-3">
            <p className="text-xs text-muted-foreground/60">Per Month</p>
            <p className="text-lg font-bold text-foreground">
              1,000,000 <span className="text-sm font-normal text-muted-foreground">emails/mo</span>
            </p>
          </div>
        </div>
      </section>

      {/* Data Retention */}
      <section className="space-y-4">
        <h3 className="text-sm font-semibold text-foreground">Data Retention</h3>
        <div className="grid gap-4 sm:grid-cols-3">
          <div className="space-y-2">
            <Label className="text-muted-foreground">Email Logs</Label>
            <Select value={emailLogRetention} onValueChange={(v) => v && setEmailLogRetention(v)}>
              <SelectTrigger className="border-border bg-muted text-foreground">
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="border-border bg-muted">
                {retentionOptions.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label className="text-muted-foreground">Inbound Emails</Label>
            <Select value={inboundRetention} onValueChange={(v) => v && setInboundRetention(v)}>
              <SelectTrigger className="border-border bg-muted text-foreground">
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="border-border bg-muted">
                {retentionOptions.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label className="text-muted-foreground">Webhook Logs</Label>
            <Select value={webhookLogRetention} onValueChange={(v) => v && setWebhookLogRetention(v)}>
              <SelectTrigger className="border-border bg-muted text-foreground">
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="border-border bg-muted">
                {retentionOptions.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      </section>

      {/* Save */}
      <div className="flex justify-end">
        <Button
          onClick={handleSave}
          disabled={saving}
          className="bg-primary text-primary-foreground hover:bg-primary/90"
        >
          {saving && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
          Save Settings
        </Button>
      </div>
    </div>
  );
}
