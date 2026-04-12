"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  Building2,
  Users,
  CreditCard,
  Activity,
  BarChart3,
  ScrollText,
  ArrowLeftRight,
  Zap,
  Shield,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { Routes } from "@/lib/constants/routes";
import type { LucideIcon } from "lucide-react";

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
}

interface NavSection {
  label?: string;
  items: NavItem[];
}

const navSections: NavSection[] = [
  {
    items: [
      { href: Routes.ADMIN_OVERVIEW, label: "Overview", icon: LayoutDashboard },
    ],
  },
  {
    label: "Management",
    items: [
      { href: Routes.ADMIN_TENANTS, label: "Tenants", icon: Building2 },
      { href: Routes.ADMIN_USERS, label: "Users", icon: Users },
      { href: Routes.ADMIN_BILLING, label: "Billing", icon: CreditCard },
    ],
  },
  {
    label: "Monitoring",
    items: [
      { href: Routes.ADMIN_HEALTH, label: "System Health", icon: Activity },
      { href: Routes.ADMIN_ANALYTICS, label: "Analytics", icon: BarChart3 },
      { href: Routes.ADMIN_AUDIT_LOGS, label: "Audit Logs", icon: ScrollText },
    ],
  },
];

interface AdminSidebarProps {
  collapsed: boolean;
  onToggle: () => void;
}

export function AdminSidebar({ collapsed }: AdminSidebarProps) {
  const pathname = usePathname();

  function isActive(href: string) {
    if (href === "/admin") return pathname === "/admin";
    return pathname === href || pathname.startsWith(href + "/");
  }

  return (
    <aside
      className={cn(
        "fixed left-0 top-0 z-40 flex h-screen flex-col bg-[#0f172a] transition-all duration-200",
        collapsed ? "w-16" : "w-[240px]",
      )}
    >
      {/* Logo + ADMIN badge */}
      <div className="flex h-14 items-center border-b border-white/[0.08] px-4">
        <Link href={Routes.ADMIN_OVERVIEW} className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-[#7c3aed]">
            <Zap className="h-4 w-4 text-white" />
          </div>
          {!collapsed && (
            <div className="flex items-center gap-2">
              <span className="text-[16px] font-semibold tracking-[-0.02em] text-[#f1f5f9]">
                SendNex
              </span>
              <span className="flex items-center gap-1 rounded-md bg-[#7c3aed]/20 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-[#a78bfa]">
                <Shield className="h-3 w-3" />
                Admin
              </span>
            </div>
          )}
        </Link>
      </div>

      {/* Navigation */}
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
                        ? "bg-[rgba(124,58,237,0.15)] font-medium text-[#a78bfa]"
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
                      <span className="flex-1 truncate">{item.label}</span>
                    )}
                  </Link>
                );
              })}
            </div>
          </div>
        ))}
      </nav>

      {/* Switch to Customer View */}
      {!collapsed && (
        <div className="border-t border-white/[0.08] px-3 py-3">
          <Link
            href={Routes.OVERVIEW}
            className="flex items-center gap-2.5 rounded-[6px] px-3 py-[7px] text-[13px] font-normal text-[#94a3b8] transition-all duration-150 hover:bg-white/[0.05] hover:text-[#e2e8f0]"
          >
            <ArrowLeftRight className="h-[18px] w-[18px] shrink-0 opacity-70" />
            <span className="flex-1 truncate">Switch to Customer View</span>
          </Link>
        </div>
      )}
    </aside>
  );
}
