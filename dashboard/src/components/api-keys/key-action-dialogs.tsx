"use client";

import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { useRotateApiKey, useRevokeApiKey } from "@/lib/hooks/use-api-keys";
import { toast } from "sonner";
import type { ApiKey } from "@/types/api-key";

interface RotateKeyDialogProps {
  apiKey: ApiKey | null;
  onClose: () => void;
}

export function RotateKeyDialog({ apiKey, onClose }: RotateKeyDialogProps) {
  const rotateMutation = useRotateApiKey();

  function handleRotate() {
    if (!apiKey) return;
    rotateMutation.mutate(apiKey.id, {
      onSuccess: () => {
        toast.success(
          `Key "${apiKey.name}" is being rotated. The old key will remain active for 24 hours.`,
        );
        onClose();
      },
      onError: () => {
        toast.error("Failed to rotate API key");
      },
    });
  }

  return (
    <ConfirmDialog
      open={!!apiKey}
      onOpenChange={() => onClose()}
      title="Rotate API Key"
      description={`This will generate a new key for "${apiKey?.name ?? ""}". The current key will continue to work for a 24-hour grace period, giving you time to update your applications.`}
      confirmLabel="Rotate Key"
      loading={rotateMutation.isPending}
      onConfirm={handleRotate}
    />
  );
}

interface RevokeKeyDialogProps {
  apiKey: ApiKey | null;
  onClose: () => void;
}

export function RevokeKeyDialog({ apiKey, onClose }: RevokeKeyDialogProps) {
  const revokeMutation = useRevokeApiKey();

  function handleRevoke() {
    if (!apiKey) return;
    revokeMutation.mutate(apiKey.id, {
      onSuccess: () => {
        toast.success(`Key "${apiKey.name}" has been revoked.`);
        onClose();
      },
      onError: () => {
        toast.error("Failed to revoke API key");
      },
    });
  }

  return (
    <ConfirmDialog
      open={!!apiKey}
      onOpenChange={() => onClose()}
      title="Revoke API Key"
      description={`This will permanently revoke the key "${apiKey?.name ?? ""}". Any applications using this key will immediately lose access. This cannot be undone.`}
      confirmLabel="Revoke Key"
      variant="destructive"
      loading={revokeMutation.isPending}
      onConfirm={handleRevoke}
    />
  );
}
