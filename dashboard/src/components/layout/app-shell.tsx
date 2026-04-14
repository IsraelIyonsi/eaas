"use client";

import { useState } from "react";
import { usePathname } from "next/navigation";
import { Sidebar } from "./sidebar";
import { AppHeader } from "./app-header";
import { Sheet, SheetContent } from "@/components/ui/sheet";
import { cn } from "@/lib/utils";
import { useSession } from "@/lib/hooks/use-session";

export function AppShell({ children }: { children: React.ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const pathname = usePathname();

  const isPublicRoute =
    pathname === "/login" ||
    pathname === "/signup" ||
    pathname === "/forgot-password" ||
    pathname === "/reset-password" ||
    pathname.startsWith("/admin") ||
    pathname === "/privacy" ||
    pathname === "/terms" ||
    pathname === "/cookies" ||
    pathname === "/dpa" ||
    pathname === "/sub-processors" ||
    pathname === "/acceptable-use";

  // LOW-9: Skip `/api/auth/me` on public routes (login, signup, legal) so
  // unauthenticated visitors don't see a 401 in the console.
  const { session: userData } = useSession({ enabled: !isPublicRoute });

  if (isPublicRoute) {
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
        <SheetContent side="left" className="w-[240px] border-sidebar-border bg-sidebar p-0">
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
