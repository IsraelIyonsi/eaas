"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { PageHeader } from "@/components/shared/page-header";
import { DetailSkeleton } from "@/components/shared/loading-skeleton";
import { DomainStatusBadge } from "@/components/shared/status-badge";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { DnsRecordsTable } from "@/components/domains/dns-records-table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import {
  useDomain,
  useVerifyDomain,
  useDeleteDomain,
} from "@/lib/hooks/use-domains";
import { Routes } from "@/lib/constants/routes";
import { toast } from "sonner";
import { RefreshCw, Trash2, Loader2, ExternalLink } from "lucide-react";
import { format, parseISO } from "date-fns";
import type { DomainDetail } from "@/types/domain";

export default function DomainDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const { data: domain, isLoading } = useDomain(id);
  const verifyMutation = useVerifyDomain();
  const deleteMutation = useDeleteDomain();

  const [deleteOpen, setDeleteOpen] = useState(false);

  // Treat domain as possibly having DomainDetail fields
  const detail = domain as DomainDetail | undefined;

  if (isLoading || !domain) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Domain Details"
          backHref={Routes.DOMAINS}
          backLabel="Back to Domains"
        />
        <DetailSkeleton />
      </div>
    );
  }

  function handleVerify() {
    verifyMutation.mutate(id, {
      onSuccess: () => toast.success("DNS verification triggered"),
      onError: () => toast.error("Failed to verify DNS records"),
    });
  }

  function handleDelete() {
    deleteMutation.mutate(id, {
      onSuccess: () => {
        toast.success("Domain removed");
        router.push(Routes.DOMAINS);
      },
      onError: () => toast.error("Failed to remove domain"),
    });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={domain.domainName}
        backHref={Routes.DOMAINS}
        backLabel="Back to Domains"
        badge={domain.status}
        action={<DomainStatusBadge status={domain.status} />}
      />

      {/* Domain Info */}
      <Card className="border-border bg-card shadow-none">
        <CardContent className="p-5">
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Status</p>
              <DomainStatusBadge status={domain.status} />
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground/60">Created</p>
              <p className="text-sm text-foreground">
                {format(parseISO(domain.createdAt), "MMM d, yyyy")}
              </p>
            </div>
            {domain.verifiedAt && (
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground/60">Verified</p>
                <p className="text-sm text-foreground">
                  {format(parseISO(domain.verifiedAt), "MMM d, yyyy")}
                </p>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* DNS Records */}
      <Card className="border-border bg-card shadow-none">
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="text-sm font-semibold text-foreground">
            DNS Records
          </CardTitle>
          <Button
            variant="outline"
            size="sm"
            onClick={handleVerify}
            disabled={verifyMutation.isPending}
            className="border-border text-muted-foreground hover:bg-muted hover:text-foreground"
          >
            {verifyMutation.isPending ? (
              <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
            ) : (
              <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
            )}
            Verify Records
          </Button>
        </CardHeader>
        <CardContent>
          <DnsRecordsTable records={domain.dnsRecords ?? []} />
        </CardContent>
      </Card>

      {/* Inbound Email Section */}
      <Card className="border-border bg-card shadow-none">
        <CardHeader>
          <CardTitle className="text-sm font-semibold text-foreground">
            Inbound Email
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-between">
            <div className="space-y-1">
              <p className="text-sm text-foreground/80">
                Receive inbound emails on this domain
              </p>
              <p className="text-xs text-muted-foreground/60">
                {detail?.inbound_enabled
                  ? `${detail.inbound_rule_count} active rule(s)`
                  : "Not configured"}
              </p>
            </div>
            <Switch
              checked={detail?.inbound_enabled ?? false}
              onCheckedChange={() =>
                toast.info("Toggle inbound via the setup wizard")
              }
            />
          </div>
          {domain.status === "Verified" && (
            <Button
              variant="outline"
              size="sm"
              className="border-border text-muted-foreground hover:bg-muted hover:text-foreground"
              onClick={() => router.push(Routes.INBOUND_SETUP(id))}
            >
              <ExternalLink className="mr-1.5 h-3.5 w-3.5" />
              Set Up Inbound
            </Button>
          )}
        </CardContent>
      </Card>

      {/* Danger Zone */}
      <div className="rounded-lg border border-red-500/20 bg-red-500/5 p-5 space-y-3">
        <h3 className="text-sm font-semibold text-red-400">Danger Zone</h3>
        <p className="text-xs text-muted-foreground">
          Remove this domain from your account. All DNS records, inbound rules,
          and associated configuration will be permanently deleted.
        </p>
        <Button
          variant="destructive"
          size="sm"
          onClick={() => setDeleteOpen(true)}
        >
          <Trash2 className="mr-1.5 h-3.5 w-3.5" />
          Remove Domain
        </Button>
      </div>

      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Remove Domain"
        description={`Are you sure you want to remove "${domain.domainName}"? All DNS records, API keys scoped to this domain, and inbound rules will be permanently deleted. This cannot be undone.`}
        confirmLabel="Remove Domain"
        variant="destructive"
        loading={deleteMutation.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
