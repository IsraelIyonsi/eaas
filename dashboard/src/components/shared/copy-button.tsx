"use client";

import { useState } from "react";
import { Copy, Check } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

interface CopyButtonProps {
  value: string;
  label?: string;
  variant?: "icon" | "button";
  className?: string;
}

export function CopyButton({
  value,
  label,
  variant = "icon",
  className,
}: CopyButtonProps) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      toast.success(label ? `${label} copied to clipboard` : "Copied to clipboard");
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error("Failed to copy to clipboard");
    }
  }

  const Icon = copied ? Check : Copy;

  if (variant === "icon") {
    return (
      <button
        type="button"
        onClick={handleCopy}
        className={cn(
          "inline-flex items-center justify-center rounded-md p-1 text-muted-foreground/60 transition-colors hover:bg-muted hover:text-foreground",
          copied && "text-emerald-400",
          className,
        )}
        title="Copy to clipboard"
      >
        <Icon className="h-4 w-4" />
      </button>
    );
  }

  return (
    <Button
      variant="outline"
      size="sm"
      onClick={handleCopy}
      className={cn(
        "border-border bg-transparent text-muted-foreground hover:bg-muted hover:text-foreground",
        copied && "text-emerald-400",
        className,
      )}
    >
      <Icon className="mr-1.5 h-3.5 w-3.5" />
      {copied ? "Copied" : "Copy"}
    </Button>
  );
}
