"use client";

import { useState, useEffect } from "react";
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
import { Loader2 } from "lucide-react";
import type { AdminTenant, UpdateTenantRequest } from "@/types/admin";

interface TenantFormSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tenant?: AdminTenant;
  loading?: boolean;
  onSubmit: (data: UpdateTenantRequest) => void;
}

export function TenantFormSheet({
  open,
  onOpenChange,
  tenant,
  loading = false,
  onSubmit,
}: TenantFormSheetProps) {
  const [name, setName] = useState("");
  const [company, setCompany] = useState("");
  const [dailyLimit, setDailyLimit] = useState("");
  const [monthlyLimit, setMonthlyLimit] = useState("");

  useEffect(() => {
    if (tenant) {
      setName(tenant.name);
      setCompany(tenant.company ?? "");
      setDailyLimit(String(tenant.dailyEmailLimit));
      setMonthlyLimit(String(tenant.monthlyEmailLimit));
    }
  }, [tenant]);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    onSubmit({
      name: name || undefined,
      company: company || undefined,
      dailyEmailLimit: dailyLimit ? Number(dailyLimit) : undefined,
      monthlyEmailLimit: monthlyLimit ? Number(monthlyLimit) : undefined,
    });
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent>
        <SheetHeader>
          <SheetTitle>Edit Tenant</SheetTitle>
          <SheetDescription>
            Update tenant configuration and limits.
          </SheetDescription>
        </SheetHeader>
        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <div className="space-y-2">
            <Label htmlFor="tenant-name">Name</Label>
            <Input
              id="tenant-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Tenant name"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="tenant-company">Company</Label>
            <Input
              id="tenant-company"
              value={company}
              onChange={(e) => setCompany(e.target.value)}
              placeholder="Company name"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="tenant-daily-limit">Daily Email Limit</Label>
            <Input
              id="tenant-daily-limit"
              type="number"
              value={dailyLimit}
              onChange={(e) => setDailyLimit(e.target.value)}
              placeholder="1000"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="tenant-monthly-limit">Monthly Email Limit</Label>
            <Input
              id="tenant-monthly-limit"
              type="number"
              value={monthlyLimit}
              onChange={(e) => setMonthlyLimit(e.target.value)}
              placeholder="50000"
            />
          </div>
          <SheetFooter className="mt-6">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={loading}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={loading}>
              {loading && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
              Save Changes
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  );
}
