"use client";

import { useState } from "react";
import {
  MutationCache,
  QueryClient,
  QueryClientProvider,
} from "@tanstack/react-query";
import { toast } from "sonner";
import { ThemeProvider } from "next-themes";
import { TooltipProvider } from "@/components/ui/tooltip";
import { Toaster } from "@/components/ui/sonner";
import { ApiError } from "@/lib/api/client";
import {
  GENERIC_MUTATION_ERROR_MESSAGE,
  STALE_TIME_MS,
  TOOLTIP_DELAY_MS,
} from "@/lib/constants/ui";

export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: STALE_TIME_MS,
            retry: 1,
            refetchOnWindowFocus: false,
            refetchOnReconnect: false,
          },
        },
        mutationCache: new MutationCache({
          onError: (error, _variables, _context, mutation) => {
            // Fallback toast: only fires when the mutation did not define its
            // own onError handler, so existing per-mutation error toasts are
            // never duplicated. Mutations that want to silence the fallback
            // can set `meta: { suppressErrorToast: true }`.
            if (mutation.options.onError) return;
            if (mutation.meta?.suppressErrorToast) return;

            const message =
              error instanceof ApiError || error instanceof Error
                ? error.message
                : GENERIC_MUTATION_ERROR_MESSAGE;
            toast.error(message || GENERIC_MUTATION_ERROR_MESSAGE);
          },
        }),
      }),
  );

  return (
    <ThemeProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      disableTransitionOnChange
    >
      <QueryClientProvider client={queryClient}>
        <TooltipProvider delay={TOOLTIP_DELAY_MS}>
          {children}
          <Toaster richColors position="bottom-right" />
        </TooltipProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}
