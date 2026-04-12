"use client";

import { useState } from "react";
import { PageHeader } from "@/components/shared/page-header";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { GeneralForm } from "@/components/settings/general-form";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { EmptyState } from "@/components/shared/empty-state";
import { CardGridSkeleton, TableSkeleton } from "@/components/shared/loading-skeleton";
import { Button } from "@/components/ui/button";
import { Routes } from "@/lib/constants/routes";
import { Key, ExternalLink, Trash2, Check, Receipt, ArrowUpRight } from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";
import { usePlans, useCurrentSubscription, useInvoices } from "@/lib/hooks/use-billing";
import { extractItems } from "@/lib/utils/api-response";
import type { Plan } from "@/types/billing";

export default function SettingsPage() {
  const [deleteAccountOpen, setDeleteAccountOpen] = useState(false);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Settings"
        description="Manage your account, team, billing, and API configuration."
      />

      <Tabs defaultValue="general">
        <TabsList className="border-border bg-muted">
          <TabsTrigger value="general" className="text-xs">
            General
          </TabsTrigger>
          <TabsTrigger value="team" className="text-xs">
            Team
          </TabsTrigger>
          <TabsTrigger value="billing" className="text-xs">
            Billing
          </TabsTrigger>
          <TabsTrigger value="api" className="text-xs">
            API
          </TabsTrigger>
        </TabsList>

        {/* General Tab */}
        <TabsContent value="general">
          <div className="mt-6 space-y-6">
            <GeneralForm />

            {/* Danger Zone */}
            <div className="rounded-lg border border-red-500/20 bg-red-500/5 p-5 space-y-3">
              <h3 className="text-sm font-semibold text-red-400">
                Danger Zone
              </h3>
              <p className="text-xs text-muted-foreground">
                Permanently delete your account and all associated data. This
                action cannot be undone.
              </p>
              <Button
                variant="destructive"
                size="sm"
                onClick={() => setDeleteAccountOpen(true)}
              >
                <Trash2 className="mr-1.5 h-3.5 w-3.5" />
                Delete Account
              </Button>
            </div>
          </div>
        </TabsContent>

        {/* Team Tab */}
        <TabsContent value="team">
          <div className="mt-6 flex flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center">
            <p className="text-lg font-semibold text-foreground">Coming Soon</p>
            <p className="mt-2 max-w-sm text-sm text-muted-foreground">
              Team management features including invitations, roles, and
              permissions are under development.
            </p>
          </div>
        </TabsContent>

        {/* Billing Tab */}
        <TabsContent value="billing">
          <BillingTabContent />
        </TabsContent>

        {/* API Tab */}
        <TabsContent value="api">
          <div className="mt-6 space-y-6">
            <div className="rounded-lg border border-border bg-card p-5 space-y-4">
              <h3 className="text-sm font-semibold text-foreground">
                API Access
              </h3>
              <p className="text-sm text-muted-foreground">
                Use your API keys to authenticate requests to the SendNex API.
                Keys can be managed from the API Keys page.
              </p>
              <div className="flex items-center gap-3">
                <Link href={Routes.API_KEYS}>
                  <Button
                    variant="outline"
                    size="sm"
                    className="border-border text-muted-foreground hover:bg-muted hover:text-foreground"
                  >
                    <Key className="mr-1.5 h-3.5 w-3.5" />
                    Manage API Keys
                  </Button>
                </Link>
                <a
                  href="https://docs.sendnex.xyz"
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <Button
                    variant="outline"
                    size="sm"
                    className="border-border text-muted-foreground hover:bg-muted hover:text-foreground"
                  >
                    <ExternalLink className="mr-1.5 h-3.5 w-3.5" />
                    API Documentation
                  </Button>
                </a>
              </div>
            </div>

            {/* API Base URL */}
            <div className="rounded-lg border border-border bg-card p-5 space-y-3">
              <h3 className="text-sm font-semibold text-foreground">Base URL</h3>
              <code className="block rounded-md border border-border bg-muted px-4 py-2.5 font-mono text-sm text-foreground/80">
                https://api.sendnex.xyz/v1
              </code>
            </div>

            {/* Authentication */}
            <div className="rounded-lg border border-border bg-card p-5 space-y-3">
              <h3 className="text-sm font-semibold text-foreground">Authentication</h3>
              <p className="text-sm text-muted-foreground">
                Include your API key in the <code className="text-foreground">Authorization</code> header:
              </p>
              <code className="block rounded-md border border-border bg-muted px-4 py-2.5 font-mono text-xs text-foreground/80">
                Authorization: Bearer eaas_your_api_key_here
              </code>
            </div>
          </div>
        </TabsContent>
      </Tabs>

      <ConfirmDialog
        open={deleteAccountOpen}
        onOpenChange={setDeleteAccountOpen}
        title="Delete Account"
        description="Are you sure you want to permanently delete your account? All domains, API keys, templates, and email data will be permanently removed. This action cannot be undone."
        confirmLabel="Delete Account"
        variant="destructive"
        onConfirm={() => {
          toast.error("Account deletion is not available in this demo.");
          setDeleteAccountOpen(false);
        }}
      />
    </div>
  );
}

// Tier ordering for plan comparison
const TIER_ORDER: Record<string, number> = {
  free: 0,
  starter: 1,
  pro: 2,
  business: 3,
  enterprise: 4,
};

function formatNumber(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(0)}K`;
  return String(n);
}

function PlanCard({ plan, currentTier }: { plan: Plan; currentTier: string }) {
  const isCurrent = plan.tier === currentTier;
  const isHigher = (TIER_ORDER[plan.tier] ?? 0) > (TIER_ORDER[currentTier] ?? 0);

  return (
    <div
      className={`relative rounded-lg p-5 space-y-4 ${
        isCurrent
          ? "border-2 border-primary bg-card"
          : "border border-border bg-card"
      }`}
    >
      {isCurrent && (
        <div className="absolute -top-3 right-4">
          <span className="inline-flex items-center gap-1 rounded-full bg-primary px-3 py-0.5 text-xs font-semibold text-primary-foreground">
            <Check className="h-3 w-3" />
            Current Plan
          </span>
        </div>
      )}
      <div>
        <h3 className="text-lg font-semibold text-foreground">{plan.name}</h3>
        <p className="text-2xl font-bold text-foreground">
          {plan.monthlyPriceUsd === 0 ? (
            "$0"
          ) : (
            <>${plan.monthlyPriceUsd.toFixed(2)}</>
          )}
          <span className="text-sm font-normal text-muted-foreground">/mo</span>
        </p>
      </div>
      <ul className="space-y-2 text-sm text-muted-foreground">
        <li className="flex items-center gap-2">
          <Check className={`h-3.5 w-3.5 ${isCurrent ? "text-primary" : "text-muted-foreground/50"}`} />
          {formatNumber(plan.dailyEmailLimit)} emails/day
        </li>
        <li className="flex items-center gap-2">
          <Check className={`h-3.5 w-3.5 ${isCurrent ? "text-primary" : "text-muted-foreground/50"}`} />
          {formatNumber(plan.monthlyEmailLimit)} emails/month
        </li>
        <li className="flex items-center gap-2">
          <Check className={`h-3.5 w-3.5 ${isCurrent ? "text-primary" : "text-muted-foreground/50"}`} />
          {plan.maxApiKeys} API keys
        </li>
        <li className="flex items-center gap-2">
          <Check className={`h-3.5 w-3.5 ${isCurrent ? "text-primary" : "text-muted-foreground/50"}`} />
          {plan.maxDomains} domains
        </li>
      </ul>
      {!isCurrent && isHigher && (
        <Button size="sm" className="w-full">
          <ArrowUpRight className="mr-1.5 h-3.5 w-3.5" />
          Upgrade
        </Button>
      )}
      {!isCurrent && !isHigher && (
        <Button
          variant="outline"
          size="sm"
          className="w-full border-border text-muted-foreground"
          disabled
        >
          Downgrade
        </Button>
      )}
    </div>
  );
}

function BillingTabContent() {
  const { data: plans, isLoading: plansLoading } = usePlans();
  const { data: subscription, isLoading: subLoading } = useCurrentSubscription();
  const { data: invoicesData, isLoading: invoicesLoading } = useInvoices();

  const planList = extractItems(plans);
  const invoices = extractItems(invoicesData);
  const currentTier = subscription?.planTier ?? "free";

  if (plansLoading || subLoading) {
    return (
      <div className="mt-6 space-y-6">
        <CardGridSkeleton cards={5} />
      </div>
    );
  }

  return (
    <div className="mt-6 space-y-8">
      {/* Plan Cards */}
      <div className="grid gap-4 md:grid-cols-3 lg:grid-cols-5">
        {planList.map((plan) => (
          <PlanCard key={plan.id} plan={plan} currentTier={currentTier} />
        ))}
      </div>

      {/* Invoice History */}
      <div className="space-y-4">
        <h3 className="text-sm font-semibold text-foreground">Invoice History</h3>
        {invoicesLoading ? (
          <CardGridSkeleton cards={5} />
        ) : invoices.length === 0 ? (
          <EmptyState
            icon={Receipt}
            title="No invoices yet"
            description="Invoices will appear here once you upgrade to a paid plan."
          />
        ) : (
          <div className="rounded-lg border border-border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-muted/50">
                  <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Invoice</th>
                  <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Amount</th>
                  <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Date</th>
                </tr>
              </thead>
              <tbody>
                {invoices.map((invoice) => (
                  <tr key={invoice.id} className="border-b border-border last:border-0">
                    <td className="px-4 py-2.5 text-foreground">{invoice.invoiceNumber}</td>
                    <td className="px-4 py-2.5 text-foreground">${invoice.amountUsd.toFixed(2)}</td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                        invoice.status === "paid"
                          ? "bg-emerald-500/10 text-emerald-400"
                          : "bg-amber-500/10 text-amber-400"
                      }`}>
                        {invoice.status}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-muted-foreground">
                      {new Date(invoice.createdAt).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
