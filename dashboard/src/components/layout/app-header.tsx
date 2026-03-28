"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Menu } from "lucide-react";

interface AppHeaderProps {
  onMobileMenuToggle: () => void;
}

export function AppHeader({ onMobileMenuToggle }: AppHeaderProps) {
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center justify-between border-b border-white/10 bg-[#1E1E2E]/80 px-4 backdrop-blur-sm lg:px-6">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          className="lg:hidden text-white/60"
          onClick={onMobileMenuToggle}
        >
          <Menu className="h-5 w-5" />
        </Button>
        <h1 className="text-sm font-semibold text-white">EaaS Dashboard</h1>
        <Badge
          variant="outline"
          className="hidden text-[10px] font-semibold uppercase tracking-wider text-[#00E5FF] border-[#00E5FF]/30 bg-[#00E5FF]/10 sm:inline-flex"
        >
          Development
        </Badge>
      </div>
    </header>
  );
}
