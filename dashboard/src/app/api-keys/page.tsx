"use client";

import { extractItems } from "@/lib/utils/api-response";

import { useState } from "react";
import { PageHeader } from "@/components/shared/page-header";
import { DataTable } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { useApiKeys } from "@/lib/hooks/use-api-keys";
import { CreateKeyDialog } from "@/components/api-keys/create-key-dialog";
import { RotateKeyDialog, RevokeKeyDialog } from "@/components/api-keys/key-action-dialogs";
import { Plus, Key, RotateCw, Ban } from "lucide-react";
import { format, parseISO } from "date-fns";
import { cn } from "@/lib/utils";
import type { ApiKey } from "@/types/api-key";

export default function ApiKeysPage() {
  const { data: apiKeys, isLoading } = useApiKeys();
  const [createOpen, setCreateOpen] = useState(false);
  const [rotateKey, setRotateKey] = useState<ApiKey | null>(null);
  const [revokeKey, setRevokeKey] = useState<ApiKey | null>(null);

  const keys = extractItems(apiKeys);

  const columns = [
    {
      key: "name",
      header: "Name",
      render: (item: ApiKey) => (
        <span className="font-medium text-foreground">{item.name}</span>
      ),
    },
    {
      key: "keyPrefix",
      header: "Prefix",
      render: (item: ApiKey) => (
        <code className="rounded bg-muted px-2 py-0.5 font-mono text-xs text-foreground/80">
          {item.keyPrefix}...
        </code>
      ),
    },
    {
      key: "isActive",
      header: "Status",
      render: (item: ApiKey) => (
        <Badge
          variant="outline"
          className={cn(
            "text-xs font-medium",
            item.isActive
              ? "bg-emerald-500/15 text-emerald-400 border-emerald-500/30"
              : "bg-red-500/15 text-red-400 border-red-500/30",
          )}
        >
          {item.isActive ? "Active" : "Inactive"}
        </Badge>
      ),
    },
    {
      key: "createdAt",
      header: "Created",
      className: "min-w-[100px]",
      render: (item: ApiKey) => (
        <span className="text-xs text-muted-foreground">
          {format(parseISO(item.createdAt), "MMM d, yyyy")}
        </span>
      ),
    },
    {
      key: "actions",
      header: "",
      className: "min-w-[160px]",
      render: (item: ApiKey) =>
        item.isActive ? (
          <div className="flex items-center gap-1 whitespace-nowrap">
            <Button
              variant="ghost"
              size="sm"
              className="h-7 text-xs text-muted-foreground hover:text-foreground"
              onClick={(e) => {
                e.stopPropagation();
                setRotateKey(item);
              }}
            >
              <RotateCw className="mr-1 h-3 w-3" />
              Rotate
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="h-7 text-xs text-red-600 hover:text-red-700"
              onClick={(e) => {
                e.stopPropagation();
                setRevokeKey(item);
              }}
            >
              <Ban className="mr-1 h-3 w-3" />
              Revoke
            </Button>
          </div>
        ) : null,
    },
  ];

  return (
    <div className="space-y-6">
      <PageHeader
        title="API Keys"
        description="Manage API keys for authenticating with the SendNex API."
        badge={keys.length > 0 ? `${keys.length}` : undefined}
        action={
          <Button
            onClick={() => setCreateOpen(true)}
            className="bg-primary text-primary-foreground hover:bg-primary/90"
          >
            <Plus className="mr-1.5 h-4 w-4" />
            Create API Key
          </Button>
        }
      />

      <DataTable
        columns={columns}
        data={keys}
        loading={isLoading}
        getRowId={(item) => item.id}
        emptyState={
          <EmptyState
            icon={Key}
            title="No API keys yet"
            description="Create an API key to start sending emails through the SendNex API."
            action={{ label: "Create API Key", onClick: () => setCreateOpen(true) }}
          />
        }
      />

      <CreateKeyDialog open={createOpen} onOpenChange={setCreateOpen} />
      <RotateKeyDialog apiKey={rotateKey} onClose={() => setRotateKey(null)} />
      <RevokeKeyDialog apiKey={revokeKey} onClose={() => setRevokeKey(null)} />
    </div>
  );
}
