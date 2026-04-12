"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Avatar,
  AvatarFallback,
} from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { PanelLeftClose, PanelLeftOpen, Search, Bell, LogOut, Settings, User } from "lucide-react";
import { Routes } from "@/lib/constants/routes";
import Link from "next/link";
import { useRouter } from "next/navigation";

interface AppHeaderProps {
  sidebarCollapsed: boolean;
  onSidebarToggle: () => void;
  userName?: string;
  userEmail?: string;
}

export function AppHeader({ sidebarCollapsed, onSidebarToggle, userName, userEmail }: AppHeaderProps) {
  const router = useRouter();

  async function handleSignOut() {
    await fetch("/api/auth/logout", { method: "POST" });
    router.push(Routes.LOGIN);
    router.refresh();
  }

  return (
    <header className="sticky top-0 z-30 flex h-14 items-center justify-between border-b border-border bg-background px-4 lg:px-6">
      <div className="flex items-center gap-3">
        {/* Sidebar toggle — always visible */}
        <button
          onClick={onSidebarToggle}
          className="flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
          aria-label={sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar"}
        >
          {sidebarCollapsed ? (
            <PanelLeftOpen className="h-[18px] w-[18px]" />
          ) : (
            <PanelLeftClose className="h-[18px] w-[18px]" />
          )}
        </button>

        <div className="h-5 w-px bg-border" />

        <h1 className="text-[13px] font-semibold text-foreground">SendNex Dashboard</h1>
        {process.env.NEXT_PUBLIC_ENV_LABEL && (
          <Badge
            variant="outline"
            className="hidden text-[10px] font-semibold uppercase tracking-[0.04em] text-primary border-primary/30 bg-primary/10 sm:inline-flex"
          >
            {process.env.NEXT_PUBLIC_ENV_LABEL}
          </Badge>
        )}
      </div>

      <div className="flex items-center gap-2">
        {/* Global Search */}
        <Button
          variant="ghost"
          size="sm"
          className="hidden gap-2 border border-border bg-muted text-muted-foreground hover:bg-accent hover:text-foreground sm:flex"
        >
          <Search className="h-4 w-4" />
          <span className="text-xs">Search...</span>
          <kbd className="ml-4 rounded border border-border bg-background px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground">
            /
          </kbd>
        </Button>

        {/* Mobile search icon */}
        <Button
          variant="ghost"
          size="icon"
          className="text-muted-foreground hover:text-foreground sm:hidden"
        >
          <Search className="h-4 w-4" />
        </Button>

        {/* Notification bell */}
        <Link
          href={Routes.NOTIFICATIONS}
          className="relative inline-flex items-center justify-center rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
        >
          <Bell className="h-4 w-4" />
          <span className="absolute right-1 top-1 h-2 w-2 rounded-full bg-destructive" />
        </Link>

        {/* User avatar + dropdown */}
        <DropdownMenu>
          <DropdownMenuTrigger
            className="flex items-center gap-2 rounded-md px-1.5 py-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus:outline-none"
          >
            <Avatar size="sm">
              <AvatarFallback className="bg-primary/20 text-xs text-primary">
                {(userName ?? "User").charAt(0).toUpperCase()}
              </AvatarFallback>
            </Avatar>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" side="bottom" sideOffset={8}>
            <div className="px-2 py-1.5">
              <p className="text-[13px] font-medium">{userName ?? "User"}</p>
              <p className="text-xs text-muted-foreground">{userEmail ?? "user@example.com"}</p>
            </div>
            <DropdownMenuSeparator />
            <DropdownMenuItem>
              <User className="mr-2 h-4 w-4" />
              Profile
            </DropdownMenuItem>
            <DropdownMenuItem>
              <Settings className="mr-2 h-4 w-4" />
              Settings
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={handleSignOut}>
              <LogOut className="mr-2 h-4 w-4" />
              Sign Out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
