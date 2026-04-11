"use client";

import { useState } from "react";
import { useDomains, useAddDomain, useVerifyDomain } from "@/lib/hooks/use-domains";
import { PageHeader } from "@/components/shared/page-header";
import { EmptyState } from "@/components/shared/empty-state";
import { CopyButton } from "@/components/shared/copy-button";
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { DomainStatusBadge } from "@/components/shared/status-badge";
import { toast } from "sonner";
import { Plus, CheckCircle2, XCircle, RefreshCw, Globe } from "lucide-react";
import { format, parseISO } from "date-fns";
import { cn } from "@/lib/utils";

export default function DomainsPage() {
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [newDomain, setNewDomain] = useState("");

  const { data: domains, isLoading } = useDomains();
  const addMutation = useAddDomain();
  const verifyMutation = useVerifyDomain();

  function handleAddDomain() {
    if (!newDomain) return;
    addMutation.mutate(newDomain, {
      onSuccess: (d) => {
        toast.success(
          `Domain "${d.domainName}" added. Configure the DNS records below, then click Verify.`,
        );
        setAddDialogOpen(false);
        setNewDomain("");
      },
    });
  }

  function handleVerify(id: string) {
    verifyMutation.mutate(id, {
      onSuccess: (d) => {
        if (d.status === "Verified") {
          toast.success(
            `Domain "${d.domainName}" verified. You can now send emails from this domain.`,
          );
        } else {
          toast.error(
            "DNS changes can take up to 48 hours to propagate. Try verifying again later.",
          );
        }
      },
    });
  }

  const dnsStatusIcon = (isVerified: boolean) => {
    return isVerified
      ? <CheckCircle2 className="h-4 w-4 text-emerald-400" />
      : <XCircle className="h-4 w-4 text-red-400" />;
  };

  const domainList = Array.isArray(domains) ? domains : [];

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <PageHeader
        title="Domains"
        description="Register and verify sending domains. Configure SPF, DKIM, and DMARC records."
        action={
          <Button
            onClick={() => setAddDialogOpen(true)}
            className="bg-primary text-primary-foreground hover:bg-primary/90"
          >
            <Plus className="mr-1.5 h-4 w-4" />
            Add Domain
          </Button>
        }
      />

      {/* Domain List */}
      {isLoading ? (
        <Skeleton className="h-[300px] rounded-lg bg-muted" />
      ) : domainList.length === 0 ? (
        <div className="rounded-lg border border-border bg-card">
          <EmptyState
            icon={Globe}
            title="No sending domains configured"
            description="Add a domain to start sending emails. You will need to add SPF, DKIM, and DMARC records to your DNS provider."
            action={{ label: "Add Domain", onClick: () => setAddDialogOpen(true) }}
          />
        </div>
      ) : (
        <div className="rounded-lg border border-border bg-card">
          <Accordion className="w-full">
            {domainList.map((domain) => (
              <AccordionItem
                key={domain.id}
                value={domain.id}
                className="border-border"
              >
                <AccordionTrigger className="px-4 hover:no-underline hover:bg-muted">
                  <div className="flex flex-1 items-center gap-4">
                    <span className="font-medium text-foreground">
                      {domain.domainName}
                    </span>
                    <DomainStatusBadge status={domain.status} />
                    {domain.verifiedAt && (
                      <span className="text-xs text-muted-foreground/40">
                        Verified{" "}
                        {format(parseISO(domain.verifiedAt), "MMM d, yyyy")}
                      </span>
                    )}
                  </div>
                </AccordionTrigger>
                <AccordionContent className="px-4 pb-4">
                  <div className="space-y-3">
                    {/* DNS Records */}
                    <div className="rounded-lg border border-border bg-background p-4">
                      <h4 className="mb-3 text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                        DNS Records
                      </h4>
                      <div className="space-y-3">
                        {domain.dnsRecords.map((record, idx) => (
                          <div
                            key={idx}
                            className="flex items-start gap-3 rounded-lg bg-muted/50 p-3"
                          >
                            <div className="mt-0.5">
                              {dnsStatusIcon(record.isVerified)}
                            </div>
                            <div className="flex-1 min-w-0 space-y-1">
                              <div className="flex items-center gap-2">
                                <Badge
                                  variant="outline"
                                  className="text-[10px] font-semibold text-muted-foreground border-border"
                                >
                                  {record.type}
                                </Badge>
                                <code className="truncate text-xs text-[var(--chart-1)]">
                                  {record.name}
                                </code>
                              </div>
                              <code className="block break-all text-xs text-muted-foreground">
                                {record.value}
                              </code>
                            </div>
                            <CopyButton value={record.value} label="DNS record value" />
                          </div>
                        ))}
                      </div>
                    </div>
                    {/* Actions */}
                    {domain.status !== "Verified" && (
                      <Button
                        size="sm"
                        onClick={() => handleVerify(domain.id)}
                        disabled={verifyMutation.isPending}
                        className="bg-primary text-primary-foreground hover:bg-primary/90"
                      >
                        <RefreshCw
                          className={cn(
                            "mr-1.5 h-3.5 w-3.5",
                            verifyMutation.isPending && "animate-spin",
                          )}
                        />
                        Verify Now
                      </Button>
                    )}
                  </div>
                </AccordionContent>
              </AccordionItem>
            ))}
          </Accordion>
        </div>
      )}

      {/* Add Domain Dialog */}
      <Dialog open={addDialogOpen} onOpenChange={setAddDialogOpen}>
        <DialogContent className="max-w-sm border-border bg-card">
          <DialogHeader>
            <DialogTitle className="text-foreground">Add Domain</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label className="text-foreground/80">Domain Name</Label>
              <Input
                value={newDomain}
                onChange={(e) => setNewDomain(e.target.value)}
                placeholder="e.g., notifications.example.com"
                className="border-border bg-muted text-foreground"
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="ghost"
              onClick={() => setAddDialogOpen(false)}
              className="text-muted-foreground"
            >
              Cancel
            </Button>
            <Button
              onClick={handleAddDomain}
              disabled={!newDomain || addMutation.isPending}
              className="bg-primary text-primary-foreground hover:bg-primary/90"
            >
              Add Domain
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
