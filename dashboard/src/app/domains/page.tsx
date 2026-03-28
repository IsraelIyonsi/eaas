"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { DomainStatusBadge } from "@/components/shared/status-badge";
import { toast } from "sonner";
import { Plus, Copy, CheckCircle2, XCircle, AlertCircle, RefreshCw } from "lucide-react";
import { format, parseISO } from "date-fns";
import type { Domain, DnsRecord } from "@/types";
import { cn } from "@/lib/utils";

export default function DomainsPage() {
  const queryClient = useQueryClient();
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [newDomain, setNewDomain] = useState("");

  const { data: domains, isLoading } = useQuery({
    queryKey: ["domains"],
    queryFn: () => api.getDomains(),
  });

  const addMutation = useMutation({
    mutationFn: (domain: string) => api.addDomain(domain),
    onSuccess: (d) => {
      queryClient.invalidateQueries({ queryKey: ["domains"] });
      toast.success(
        `Domain "${d.domain}" added. Configure the DNS records below, then click Verify.`,
      );
      setAddDialogOpen(false);
      setNewDomain("");
    },
  });

  const verifyMutation = useMutation({
    mutationFn: (id: string) => api.verifyDomain(id),
    onSuccess: (d) => {
      queryClient.invalidateQueries({ queryKey: ["domains"] });
      if (d.status === "verified") {
        toast.success(
          `Domain "${d.domain}" verified. You can now send emails from this domain.`,
        );
      } else {
        toast.error(
          "DNS changes can take up to 48 hours to propagate. Try verifying again later.",
        );
      }
    },
  });

  function copyToClipboard(text: string) {
    navigator.clipboard.writeText(text);
    toast.success("Copied to clipboard.");
  }

  const dnsStatusIcon = (status: DnsRecord["status"]) => {
    switch (status) {
      case "verified":
        return <CheckCircle2 className="h-4 w-4 text-emerald-400" />;
      case "mismatch":
        return <AlertCircle className="h-4 w-4 text-amber-400" />;
      case "missing":
        return <XCircle className="h-4 w-4 text-red-400" />;
    }
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Domains</h1>
          <p className="text-sm text-white/50">
            Register and verify sending domains. Configure SPF, DKIM, and DMARC
            records.
          </p>
        </div>
        <Button
          onClick={() => setAddDialogOpen(true)}
          className="bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
        >
          <Plus className="mr-1.5 h-4 w-4" />
          Add Domain
        </Button>
      </div>

      {/* Domain List */}
      {isLoading ? (
        <Skeleton className="h-[300px] rounded-lg bg-white/5" />
      ) : !domains || domains.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-white/10 bg-[#1E1E2E] py-16 text-center">
          <p className="text-lg font-semibold text-white">
            No sending domains configured
          </p>
          <p className="mt-2 max-w-sm text-sm text-white/50">
            Add a domain to start sending emails. You will need to add SPF,
            DKIM, and DMARC records to your DNS provider.
          </p>
          <Button
            onClick={() => setAddDialogOpen(true)}
            className="mt-4 bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
          >
            Add Domain
          </Button>
        </div>
      ) : (
        <div className="rounded-lg border border-white/10 bg-[#1E1E2E]">
          <Accordion className="w-full">
            {domains.map((domain) => (
              <AccordionItem
                key={domain.id}
                value={domain.id}
                className="border-white/5"
              >
                <AccordionTrigger className="px-4 hover:no-underline hover:bg-white/[0.02]">
                  <div className="flex flex-1 items-center gap-4">
                    <span className="font-medium text-white">
                      {domain.domain}
                    </span>
                    <DomainStatusBadge status={domain.status} />
                    {domain.verified_at && (
                      <span className="text-xs text-white/30">
                        Verified{" "}
                        {format(parseISO(domain.verified_at), "MMM d, yyyy")}
                      </span>
                    )}
                  </div>
                </AccordionTrigger>
                <AccordionContent className="px-4 pb-4">
                  <div className="space-y-3">
                    {/* DNS Records */}
                    <div className="rounded-lg border border-white/10 bg-[#0F0F1A] p-4">
                      <h4 className="mb-3 text-xs font-semibold uppercase tracking-wider text-white/40">
                        DNS Records
                      </h4>
                      <div className="space-y-3">
                        {domain.dns_records.map((record, idx) => (
                          <div
                            key={idx}
                            className="flex items-start gap-3 rounded-lg bg-white/[0.03] p-3"
                          >
                            <div className="mt-0.5">
                              {dnsStatusIcon(record.status)}
                            </div>
                            <div className="flex-1 min-w-0 space-y-1">
                              <div className="flex items-center gap-2">
                                <Badge
                                  variant="outline"
                                  className="text-[10px] font-semibold text-white/60 border-white/20"
                                >
                                  {record.type}
                                </Badge>
                                <code className="truncate text-xs text-[#00E5FF]">
                                  {record.name}
                                </code>
                              </div>
                              <code className="block break-all text-xs text-white/50">
                                {record.value}
                              </code>
                            </div>
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-7 w-7 shrink-0 text-white/30 hover:text-white"
                              onClick={() => copyToClipboard(record.value)}
                            >
                              <Copy className="h-3.5 w-3.5" />
                            </Button>
                          </div>
                        ))}
                      </div>
                    </div>
                    {/* Actions */}
                    {domain.status !== "verified" && (
                      <Button
                        size="sm"
                        onClick={() => verifyMutation.mutate(domain.id)}
                        disabled={verifyMutation.isPending}
                        className="bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
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
        <DialogContent className="max-w-sm border-white/10 bg-[#1E1E2E]">
          <DialogHeader>
            <DialogTitle className="text-white">Add Domain</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label className="text-white/70">Domain Name</Label>
              <Input
                value={newDomain}
                onChange={(e) => setNewDomain(e.target.value)}
                placeholder="e.g., notifications.example.com"
                className="border-white/10 bg-[#27293D] text-white"
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="ghost"
              onClick={() => setAddDialogOpen(false)}
              className="text-white/60"
            >
              Cancel
            </Button>
            <Button
              onClick={() => newDomain && addMutation.mutate(newDomain)}
              disabled={!newDomain || addMutation.isPending}
              className="bg-[#7C4DFF] text-white hover:bg-[#6B3FE8]"
            >
              Add Domain
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
