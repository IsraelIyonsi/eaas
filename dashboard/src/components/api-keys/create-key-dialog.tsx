"use client";

import { useState } from "react";
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
import { CopyButton } from "@/components/shared/copy-button";
import { useCreateApiKey } from "@/lib/hooks/use-api-keys";
import { toast } from "sonner";
import { AlertTriangle, Loader2 } from "lucide-react";
import type { CreateApiKeyResponse } from "@/types/api-key";

interface CreateKeyDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CreateKeyDialog({ open, onOpenChange }: CreateKeyDialogProps) {
  const [name, setName] = useState("");
  const [createdKey, setCreatedKey] = useState<CreateApiKeyResponse | null>(null);

  const createMutation = useCreateApiKey();

  function handleCreate() {
    createMutation.mutate(
      { name },
      {
        onSuccess: (data) => {
          setCreatedKey(data as unknown as CreateApiKeyResponse);
          toast.success("API key created successfully");
        },
        onError: () => {
          toast.error("Failed to create API key");
        },
      },
    );
  }

  function handleClose() {
    onOpenChange(false);
    // Reset state after animation
    setTimeout(() => {
      setName("");
      setCreatedKey(null);
    }, 200);
  }

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-md border-border bg-card">
        <DialogHeader>
          <DialogTitle className="text-foreground">
            {createdKey ? "API Key Created" : "Create API Key"}
          </DialogTitle>
        </DialogHeader>

        {createdKey ? (
          <div className="space-y-4 py-2">
            {/* Warning banner */}
            <div className="flex items-start gap-3 rounded-lg border border-amber-500/30 bg-amber-500/10 p-3">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-400" />
              <p className="text-sm text-amber-200">
                Copy your API key now. It won&apos;t be shown again.
              </p>
            </div>

            {/* Key display */}
            <div className="space-y-2">
              <Label className="text-foreground/80">API Key</Label>
              <div className="flex items-center gap-2 rounded-lg border border-border bg-muted px-3 py-2">
                <code className="flex-1 break-all font-mono text-sm text-foreground">
                  {createdKey.key}
                </code>
                <CopyButton value={createdKey.key} label="API Key" />
              </div>
            </div>

            <DialogFooter>
              <Button
                onClick={handleClose}
                className="bg-primary text-primary-foreground hover:bg-primary/90"
              >
                I&apos;ve copied it
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label className="text-foreground/80">Name</Label>
              <Input
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. Production, Staging"
                className="border-border bg-muted text-foreground"
              />
            </div>

            <DialogFooter>
              <Button
                variant="ghost"
                onClick={handleClose}
                className="text-muted-foreground"
              >
                Cancel
              </Button>
              <Button
                onClick={handleCreate}
                disabled={!name || createMutation.isPending}
                className="bg-primary text-primary-foreground hover:bg-primary/90"
              >
                {createMutation.isPending && (
                  <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
                )}
                Create Key
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
