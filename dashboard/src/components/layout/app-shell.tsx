"use client";

import { useState } from "react";
import { usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Sidebar } from "./sidebar";
import { AppHeader } from "./app-header";
import { Sheet, SheetContent } from "@/components/ui/sheet";
import { cn } from "@/lib/utils";
import type { SessionData } from "@/lib/auth/types";

export function AppShell({ children }: { children: React.ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const pathname = usePathname();

  const { data: session } = useQuery<{ success: boolean; data: SessionData }>({
    queryKey: ["session"],
    queryFn: () => fetch("/api/auth/me").then((r) => r.json()),
    retry: false,
    staleTime: 5 * 60 * 1000,
  });

  const userData = session?.data;

  if (
    pathname === "/login" ||
    pathname === "/signup" ||
    pathname.startsWith("/admin") ||
    pathname === "/privacy" ||
    pathname === "/terms" ||
    pathname === "/cookies" ||
    pathname === "/dpa" ||
    pathname === "/sub-processors" ||
    pathname === "/acceptable-use"
  ) {
    return <>{children}</>;
  }

  function handleToggle() {
    // On mobile, toggle the sheet. On desktop, toggle collapse.
    if (typeof window !== "undefined" && window.innerWidth < 1024) {
      setMobileOpen((prev) => !prev);
    } else {
      setCollapsed((prev) => !prev);
    }
  }

  return (
    <div className="min-h-screen bg-background">
      {/* Desktop sidebar */}
      <div className="hidden lg:block">
        <Sidebar
          collapsed={collapsed}
          onToggle={() => setCollapsed(!collapsed)}
          userName={userData?.displayName}
          userEmail={userData?.email}
          userRole={userData?.role}
        />
      </div>

      {/* Mobile sidebar */}
      <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
        <SheetContent side="left" className="w-[240px] border-sidebar-border bg-[#0f172a] p-0">
          <Sidebar
            collapsed={false}
            onToggle={() => setMobileOpen(false)}
            userName={userData?.displayName}
            userEmail={userData?.email}
            userRole={userData?.role}
          />
        </SheetContent>
      </Sheet>

      {/* Main content */}
      <div
        className={cn(
          "flex flex-col transition-all duration-200",
          collapsed ? "lg:ml-16" : "lg:ml-[240px]",
        )}
      >
        <AppHeader
          sidebarCollapsed={collapsed}
          onSidebarToggle={handleToggle}
          userName={userData?.displayName}
          userEmail={userData?.email}
        />
        <main className="flex-1 p-4 lg:p-6">{children}</main>
      </div>
    </div>
  );
}
