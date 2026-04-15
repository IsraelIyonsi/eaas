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

  // Use startsWith (matching middleware.ts) so trailing-slash variants like
  // `/terms/` do NOT fall through to the authed shell — which would kick
  // unauthed visitors out via the 401 redirect path. UAT r3 caught /terms
  // bouncing to /login after hydration because `pathname === "/terms"` failed
  // when Next delivered `/terms/` during client-side routing.
  const isPublicRoute =
    pathname.startsWith("/login") ||
    pathname.startsWith("/signup") ||
    pathname.startsWith("/forgot-password") ||
    pathname.startsWith("/reset-password") ||
    pathname.startsWith("/admin") ||
    pathname.startsWith("/privacy") ||
    pathname.startsWith("/terms") ||
    pathname.startsWith("/cookies") ||
    pathname.startsWith("/dpa") ||
    pathname.startsWith("/sub-processors") ||
    pathname.startsWith("/acceptable-use");

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
