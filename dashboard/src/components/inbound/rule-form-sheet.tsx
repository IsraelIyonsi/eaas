"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetFooter,
} from "@/components/ui/sheet";
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
import { Switch } from "@/components/ui/switch";
import { useDomains } from "@/lib/hooks/use-domains";
import {
  useCreateInboundRule,
  useUpdateInboundRule,
} from "@/lib/hooks/use-inbound";
import type { InboundRule, InboundRuleAction } from "@/types/inbound";

interface RuleFormSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  rule?: InboundRule;
}

export function RuleFormSheet({ open, onOpenChange, rule }: RuleFormSheetProps) {
  const isEditing = !!rule;
  const { data: domainsData } = useDomains();
  const createRule = useCreateInboundRule();
  const updateRule = useUpdateInboundRule();

  const [name, setName] = useState("");
  const [domainId, setDomainId] = useState("");
  const [matchPattern, setMatchPattern] = useState("*");
  const [action, setAction] = useState<InboundRuleAction>("webhook");
  const [webhookUrl, setWebhookUrl] = useState("");
  const [forwardTo, setForwardTo] = useState("");
  const [priority, setPriority] = useState(0);
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (rule) {
      setName(rule.name);
      setDomainId(rule.domainId);
      setMatchPattern(rule.matchPattern);
      setAction(rule.action);
      setWebhookUrl(rule.webhookUrl ?? "");
      setForwardTo(rule.forwardTo ?? "");
      setPriority(rule.priority);
      setIsActive(rule.isActive);
    } else {
      setName("");
      setDomainId("");
      setMatchPattern("*");
      setAction("webhook");
      setWebhookUrl("");
      setForwardTo("");
      setPriority(0);
      setIsActive(true);
    }
  }, [rule, open]);

  const loading = createRule.isPending || updateRule.isPending;

  const domains = domainsData ?? [];

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    if (!name.trim()) {
      toast.error("Name is required");
      return;
    }
    if (!domainId) {
      toast.error("Domain is required");
      return;
    }

    try {
      if (isEditing && rule) {
        await updateRule.mutateAsync({
          id: rule.id,
          data: {
            name,
            matchPattern,
            action,
            webhookUrl: action === "webhook" ? webhookUrl : undefined,
            forwardTo: action === "forward" ? forwardTo : undefined,
            priority,
            isActive,
          },
        });
        toast.success("Rule updated successfully");
      } else {
        await createRule.mutateAsync({
          name,
          domainId,
          matchPattern,
          action,
          webhookUrl: action === "webhook" ? webhookUrl : undefined,
          forwardTo: action === "forward" ? forwardTo : undefined,
          priority,
        });
        toast.success("Rule created successfully");
      }
      onOpenChange(false);
    } catch {
      toast.error(isEditing ? "Failed to update rule" : "Failed to create rule");
    }
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-full border-border bg-card sm:max-w-md">
        <SheetHeader>
          <SheetTitle className="text-foreground">
            {isEditing ? "Edit Rule" : "Create Rule"}
          </SheetTitle>
          <SheetDescription className="text-muted-foreground">
            {isEditing
              ? "Update the inbound routing rule."
              : "Configure how inbound emails are routed."}
          </SheetDescription>
        </SheetHeader>

        <form
          onSubmit={handleSubmit}
          className="flex flex-1 flex-col gap-4 overflow-y-auto px-4"
        >
          {/* Name */}
          <div className="space-y-1.5">
            <Label className="text-foreground/80">Name</Label>
            <Input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Forward support emails"
              className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
            />
          </div>

          {/* Domain */}
          <div className="space-y-1.5">
            <Label className="text-foreground/80">Domain</Label>
            <Select value={domainId} onValueChange={(v) => setDomainId(v ?? "")} disabled={isEditing}>
              <SelectTrigger className="border-border bg-muted text-foreground">
                <SelectValue placeholder="Select domain" />
              </SelectTrigger>
              <SelectContent>
                {domains.map((d: { id: string; domainName: string }) => (
                  <SelectItem key={d.id} value={d.id}>
                    {d.domainName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Match Pattern */}
          <div className="space-y-1.5">
            <Label className="text-foreground/80">Match Pattern</Label>
            <Input
              value={matchPattern}
              onChange={(e) => setMatchPattern(e.target.value)}
              placeholder="* or support@*, info@*"
              className="border-border bg-muted font-mono text-sm text-foreground placeholder:text-muted-foreground/40"
            />
            <p className="text-xs text-muted-foreground/60">
              Use * to match all, or patterns like support@* or *@subdomain.example.com
            </p>
          </div>

          {/* Action */}
          <div className="space-y-1.5">
            <Label className="text-foreground/80">Action</Label>
            <div className="flex gap-2">
              {(["webhook", "forward", "store"] as const).map((a) => (
                <button
                  key={a}
                  type="button"
                  onClick={() => setAction(a)}
                  className={`rounded-md border px-3 py-1.5 text-xs font-medium transition-colors ${
                    action === a
                      ? "border-[var(--primary)]/50 bg-primary/15 text-primary"
                      : "border-border bg-muted text-muted-foreground hover:border-border"
                  }`}
                >
                  {a.charAt(0).toUpperCase() + a.slice(1)}
                </button>
              ))}
            </div>
          </div>

          {/* Conditional: Webhook URL */}
          {action === "webhook" && (
            <div className="space-y-1.5">
              <Label className="text-foreground/80">Webhook URL</Label>
              <Input
                value={webhookUrl}
                onChange={(e) => setWebhookUrl(e.target.value)}
                placeholder="https://your-app.com/webhooks/inbound"
                className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
              />
            </div>
          )}

          {/* Conditional: Forward To */}
          {action === "forward" && (
            <div className="space-y-1.5">
              <Label className="text-foreground/80">Forward To</Label>
              <Input
                value={forwardTo}
                onChange={(e) => setForwardTo(e.target.value)}
                placeholder="team@example.com"
                className="border-border bg-muted text-foreground placeholder:text-muted-foreground/40"
              />
            </div>
          )}

          {/* Priority */}
          <div className="space-y-1.5">
            <Label className="text-foreground/80">Priority</Label>
            <Input
              type="number"
              value={priority}
              onChange={(e) => setPriority(Number(e.target.value))}
              min={0}
              className="border-border bg-muted text-foreground"
            />
            <p className="text-xs text-muted-foreground/60">
              Lower numbers are evaluated first. 0 = highest priority.
            </p>
          </div>

          {/* Active toggle */}
          <div className="flex items-center justify-between">
            <Label className="text-foreground/80">Active</Label>
            <Switch checked={isActive} onCheckedChange={setIsActive} />
          </div>
        </form>

        <SheetFooter className="border-t border-border">
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={loading}
            className="border-border text-muted-foreground hover:bg-muted"
          >
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={loading}>
            {loading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {isEditing ? "Update Rule" : "Create Rule"}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
