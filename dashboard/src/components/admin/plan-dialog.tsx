"use client";

import { useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
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
import { Loader2 } from "lucide-react";
import type { Plan, CreatePlanRequest } from "@/types/billing";

const TIER_OPTIONS = [
  { value: "free", label: "Free" },
  { value: "starter", label: "Starter" },
  { value: "pro", label: "Pro" },
  { value: "business", label: "Business" },
  { value: "enterprise", label: "Enterprise" },
];

interface PlanDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  loading?: boolean;
  onSubmit: (data: CreatePlanRequest) => void;
  plan?: Plan | null;
}

const defaultFormState: CreatePlanRequest = {
  name: "",
  tier: "free",
  monthlyPriceUsd: 0,
  annualPriceUsd: 0,
  dailyEmailLimit: 100,
  monthlyEmailLimit: 3000,
  maxApiKeys: 3,
  maxDomains: 2,
  maxTemplates: 10,
  maxWebhooks: 5,
  customDomainBranding: false,
  prioritySupport: false,
};

export function PlanDialog({
  open,
  onOpenChange,
  loading = false,
  onSubmit,
  plan,
}: PlanDialogProps) {
  const [form, setForm] = useState<CreatePlanRequest>(defaultFormState);
  const [prevPlanId, setPrevPlanId] = useState<string | undefined>(undefined);

  const isEdit = !!plan;
  const currentPlanId = plan?.id;

  if (currentPlanId !== prevPlanId) {
    setPrevPlanId(currentPlanId);
    if (plan) {
      setForm({
        name: plan.name,
        tier: plan.tier,
        monthlyPriceUsd: plan.monthlyPriceUsd,
        annualPriceUsd: plan.annualPriceUsd,
        dailyEmailLimit: plan.dailyEmailLimit,
        monthlyEmailLimit: plan.monthlyEmailLimit,
        maxApiKeys: plan.maxApiKeys,
        maxDomains: plan.maxDomains,
        maxTemplates: plan.maxTemplates,
        maxWebhooks: plan.maxWebhooks,
        customDomainBranding: plan.customDomainBranding,
        prioritySupport: plan.prioritySupport,
      });
    } else {
      setForm(defaultFormState);
    }
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    onSubmit(form);
  }

  function handleOpenChange(isOpen: boolean) {
    if (!isOpen) {
      setForm(defaultFormState);
    }
    onOpenChange(isOpen);
  }

  function updateField<K extends keyof CreatePlanRequest>(key: K, value: CreatePlanRequest[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? "Edit Plan" : "Create Plan"}</DialogTitle>
          <DialogDescription>
            {isEdit
              ? "Update the billing plan details."
              : "Add a new billing plan to the platform."}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="plan-name">Name</Label>
              <Input
                id="plan-name"
                value={form.name}
                onChange={(e) => updateField("name", e.target.value)}
                placeholder="Plan name"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="plan-tier">Tier</Label>
              <Select value={form.tier} onValueChange={(v) => v && updateField("tier", v)}>
                <SelectTrigger id="plan-tier">
                  <SelectValue placeholder="Select tier" />
                </SelectTrigger>
                <SelectContent>
                  {TIER_OPTIONS.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>
                      {opt.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="plan-monthly-price">Monthly Price ($)</Label>
              <Input
                id="plan-monthly-price"
                type="number"
                step="0.01"
                min="0"
                value={form.monthlyPriceUsd}
                onChange={(e) => updateField("monthlyPriceUsd", parseFloat(e.target.value) || 0)}
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="plan-annual-price">Annual Price ($)</Label>
              <Input
                id="plan-annual-price"
                type="number"
                step="0.01"
                min="0"
                value={form.annualPriceUsd}
                onChange={(e) => updateField("annualPriceUsd", parseFloat(e.target.value) || 0)}
                required
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="plan-daily-limit">Daily Email Limit</Label>
              <Input
                id="plan-daily-limit"
                type="number"
                min="0"
                value={form.dailyEmailLimit}
                onChange={(e) => updateField("dailyEmailLimit", parseInt(e.target.value) || 0)}
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="plan-monthly-limit">Monthly Email Limit</Label>
              <Input
                id="plan-monthly-limit"
                type="number"
                min="0"
                value={form.monthlyEmailLimit}
                onChange={(e) => updateField("monthlyEmailLimit", parseInt(e.target.value) || 0)}
                required
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="plan-max-api-keys">Max API Keys</Label>
              <Input
                id="plan-max-api-keys"
                type="number"
                min="0"
                value={form.maxApiKeys}
                onChange={(e) => updateField("maxApiKeys", parseInt(e.target.value) || 0)}
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="plan-max-domains">Max Domains</Label>
              <Input
                id="plan-max-domains"
                type="number"
                min="0"
                value={form.maxDomains}
                onChange={(e) => updateField("maxDomains", parseInt(e.target.value) || 0)}
                required
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="plan-max-templates">Max Templates</Label>
              <Input
                id="plan-max-templates"
                type="number"
                min="0"
                value={form.maxTemplates}
                onChange={(e) => updateField("maxTemplates", parseInt(e.target.value) || 0)}
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="plan-max-webhooks">Max Webhooks</Label>
              <Input
                id="plan-max-webhooks"
                type="number"
                min="0"
                value={form.maxWebhooks}
                onChange={(e) => updateField("maxWebhooks", parseInt(e.target.value) || 0)}
                required
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="flex items-center space-x-2">
              <input
                id="plan-custom-branding"
                type="checkbox"
                checked={form.customDomainBranding}
                onChange={(e) => updateField("customDomainBranding", e.target.checked)}
                className="h-4 w-4 rounded border-border"
              />
              <Label htmlFor="plan-custom-branding">Custom Domain Branding</Label>
            </div>
            <div className="flex items-center space-x-2">
              <input
                id="plan-priority-support"
                type="checkbox"
                checked={form.prioritySupport}
                onChange={(e) => updateField("prioritySupport", e.target.checked)}
                className="h-4 w-4 rounded border-border"
              />
              <Label htmlFor="plan-priority-support">Priority Support</Label>
            </div>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => handleOpenChange(false)}
              disabled={loading}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={loading}>
              {loading && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
              {isEdit ? "Update Plan" : "Create Plan"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
