"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  Send,
  Inbox,
  FileText,
  GitBranch,
  Globe,
  Key,
  Link2,
  ShieldBan,
  BarChart3,
  PieChart,
  Bell,
  Settings,
  Zap,
  BookOpen,
  Shield,
  LogOut,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { Routes } from "@/lib/constants/routes";
import type { LucideIcon } from "lucide-react";

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  badge?: string;
}

interface NavSection {
  label?: string;
  items: NavItem[];
}

const navSections: NavSection[] = [
  {
    items: [
      { href: Routes.OVERVIEW, label: "Overview", icon: LayoutDashboard },
    ],
  },
  {
    label: "Email",
    items: [
      { href: Routes.EMAILS, label: "Sent Emails", icon: Send },
      { href: Routes.INBOUND_EMAILS, label: "Received", icon: Inbox, badge: "NEW" },
      { href: Routes.TEMPLATES, label: "Templates", icon: FileText },
    ],
  },
  {
    label: "Inbound",
    items: [
      { href: Routes.INBOUND_RULES, label: "Routing Rules", icon: GitBranch, badge: "NEW" },
    ],
  },
  {
    label: "Configuration",
    items: [
      { href: Routes.DOMAINS, label: "Domains", icon: Globe },
      { href: Routes.API_KEYS, label: "API Keys", icon: Key },
      { href: Routes.WEBHOOKS, label: "Webhooks", icon: Link2 },
      { href: Routes.SUPPRESSIONS, label: "Suppressions", icon: ShieldBan },
    ],
  },
  {
    label: "Analytics",
    items: [
      { href: Routes.ANALYTICS_OUTBOUND, label: "Outbound", icon: BarChart3 },
      { href: Routes.ANALYTICS_INBOUND, label: "Inbound", icon: PieChart, badge: "NEW" },
    ],
  },
  {
    label: "Settings",
    items: [
      { href: Routes.NOTIFICATIONS, label: "Notifications", icon: Bell },
      { href: Routes.SETTINGS, label: "Settings", icon: Settings },
      { href: "/docs", label: "Documentation", icon: BookOpen },
    ],
  },
];

interface SidebarProps {
  collapsed: boolean;
  onToggle: () => void;
  userName?: string;
  userEmail?: string;
  userRole?: "superadmin" | "admin" | "readonly" | "tenant";
}

export function Sidebar({ collapsed, onToggle, userName, userEmail, userRole }: SidebarProps) {
  const pathname = usePathname();

  function isActive(href: string) {
    if (href === "/") return pathname === "/";
    // Exact match for leaf routes like /analytics (don't match /analytics/inbound)
    if (href === Routes.ANALYTICS_OUTBOUND) return pathname === href;
    return pathname === href || pathname.startsWith(href + "/");
  }

  return (
    <aside
      className={cn(
        "fixed left-0 top-0 z-40 flex h-screen flex-col bg-[#0f172a] transition-all duration-200",
        collapsed ? "w-16" : "w-[240px]",
      )}
    >
      {/* Logo */}
      <div className="flex h-14 items-center border-b border-white/[0.08] px-4">
        <Link href={Routes.OVERVIEW} className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-[#2563eb]">
            <Zap className="h-4 w-4 text-white" />
          </div>
          {!collapsed && (
            <span className="text-[16px] font-semibold tracking-[-0.02em] text-[#f1f5f9]">
              SendNex
            </span>
          )}
        </Link>
      </div>

      {/* Navigation — hidden scrollbar */}
      <nav
        className="flex-1 overflow-y-auto px-2 py-3"
        style={{ scrollbarWidth: "none", msOverflowStyle: "none" }}
      >
        <style>{`nav::-webkit-scrollbar { display: none; }`}</style>
        {navSections.map((section, idx) => (
          <div key={section.label ?? idx}>
            {section.label && !collapsed && (
              <div className="px-3 pb-1 pt-4 text-[11px] font-semibold uppercase tracking-[0.06em] text-[#475569]">
                {section.label}
              </div>
            )}
            {section.label && collapsed && (
              <div className="mx-auto my-2 h-px w-6 bg-white/[0.08]" />
            )}
            <div className="space-y-[2px]">
              {section.items.map((item) => {
                const active = isActive(item.href);
                const Icon = item.icon;
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    title={collapsed ? item.label : undefined}
                    className={cn(
                      "flex items-center gap-2.5 rounded-[6px] px-3 py-[7px] text-[13px] transition-all duration-150",
                      active
                        ? "bg-[rgba(37,99,235,0.15)] font-medium text-[#60a5fa]"
                        : "font-normal text-[#94a3b8] hover:bg-white/[0.05] hover:text-[#e2e8f0]",
                      collapsed && "justify-center px-0",
                    )}
                  >
                    <Icon
                      className={cn(
                        "h-[18px] w-[18px] shrink-0",
                        active ? "opacity-100" : "opacity-70",
                      )}
                    />
                    {!collapsed && (
                      <>
                        <span className="flex-1 truncate">{item.label}</span>
                        {item.badge && (
                          <span className="rounded-full bg-blue-500 px-[6px] py-[1px] text-[10px] font-semibold leading-tight text-white">
                            {item.badge}
                          </span>
                        )}
                      </>
                    )}
                  </Link>
                );
              })}
            </div>
          </div>
        ))}
      </nav>

      {/* Admin Panel Link — only for admin/superadmin */}
      {!collapsed && (userRole === "admin" || userRole === "superadmin") && (
        <div className="border-t border-white/[0.08] px-2 py-2">
          <Link
            href={Routes.ADMIN_OVERVIEW}
            className="flex items-center gap-2.5 rounded-[6px] px-3 py-[7px] text-[13px] font-normal text-[#94a3b8] transition-all duration-150 hover:bg-white/[0.05] hover:text-[#e2e8f0]"
          >
            <Shield className="h-[18px] w-[18px] shrink-0 opacity-70" />
            <span className="flex-1 truncate">Admin Panel</span>
          </Link>
        </div>
      )}

      {/* User */}
      {!collapsed && (
        <div className="border-t border-white/[0.08] px-3 py-3 space-y-2">
          <div className="flex items-center gap-2.5">
            <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[#3b82f6]">
              <span className="text-[11px] font-semibold text-white">
                {(userName ?? "User").charAt(0).toUpperCase()}
              </span>
            </div>
            <div className="min-w-0 flex-1">
              <p className="truncate text-[13px] font-medium text-[#e2e8f0]">{userName ?? "User"}</p>
              <p className="truncate text-[11px] text-[#64748b]">{userEmail ?? "user@example.com"}</p>
            </div>
          </div>
          <button
            onClick={async () => {
              await fetch("/api/auth/logout", { method: "POST" });
              window.location.href = "/login";
            }}
            className="flex items-center gap-2 w-full px-2 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:bg-accent rounded-md transition-colors"
          >
            <LogOut className="h-4 w-4" />
            <span>Sign out</span>
          </button>
        </div>
      )}
    </aside>
  );
}
