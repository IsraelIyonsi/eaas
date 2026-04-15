"use client";

import { useSyncExternalStore } from "react";
import { useTheme } from "next-themes";
import { Monitor, Moon, Sun } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

const THEMES = {
  LIGHT: "light",
  DARK: "dark",
  SYSTEM: "system",
} as const;

type ThemeValue = (typeof THEMES)[keyof typeof THEMES];

/**
 * Tracks whether the component has mounted on the client. Uses
 * useSyncExternalStore with a no-op subscribe so the server snapshot returns
 * `false` and the client snapshot returns `true` — giving us a clean
 * hydration-safe boolean without a setState-in-effect lint violation.
 */
const emptySubscribe = () => () => {};
const getClientSnapshot = () => true;
const getServerSnapshot = () => false;

/**
 * Theme toggle — cycles between light, dark, and OS preference.
 * Uses next-themes under the hood. Renders a neutral placeholder on the
 * server to avoid hydration mismatch, then swaps in the active icon once
 * mounted on the client.
 */
export function ThemeToggle() {
  const { theme, setTheme, resolvedTheme } = useTheme();
  const mounted = useSyncExternalStore(
    emptySubscribe,
    getClientSnapshot,
    getServerSnapshot,
  );

  const active: ThemeValue =
    (theme as ThemeValue | undefined) ?? THEMES.SYSTEM;
  const effective = mounted
    ? (resolvedTheme ?? THEMES.LIGHT)
    : THEMES.LIGHT;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Toggle theme"
        className="inline-flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {effective === THEMES.DARK ? (
          <Moon className="h-4 w-4" aria-hidden="true" />
        ) : (
          <Sun className="h-4 w-4" aria-hidden="true" />
        )}
        <span className="sr-only">
          Current theme: {mounted ? active : THEMES.SYSTEM}
        </span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" side="bottom" sideOffset={8}>
        <DropdownMenuItem onClick={() => setTheme(THEMES.LIGHT)}>
          <Sun className="mr-2 h-4 w-4" aria-hidden="true" />
          Light
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => setTheme(THEMES.DARK)}>
          <Moon className="mr-2 h-4 w-4" aria-hidden="true" />
          Dark
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => setTheme(THEMES.SYSTEM)}>
          <Monitor className="mr-2 h-4 w-4" aria-hidden="true" />
          System
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
