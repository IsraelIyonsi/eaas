// ============================================================
// EaaS Dashboard - Session React Query Hook
// ============================================================
// Provides session data (role, userId, etc.) to client components.
// Shares the same ["session"] cache key as AppShell, so no extra fetch.

import { useQuery } from '@tanstack/react-query';
import { DETAIL_STALE_TIME_MS } from '@/lib/constants/ui';
import type { SessionData } from '@/lib/auth/types';

interface SessionResponse {
  success: boolean;
  data: SessionData;
}

interface UseSessionOptions {
  /**
   * When false, the session fetch is skipped. Use on public routes
   * (login, signup, legal pages) to avoid a noisy 401 on `/api/auth/me`.
   */
  enabled?: boolean;
}

export function useSession(options: UseSessionOptions = {}) {
  const { enabled = true } = options;

  const query = useQuery<SessionResponse | null>({
    queryKey: ['session'],
    queryFn: async () => {
      const r = await fetch('/api/auth/me');
      // LOW-9: 401 is expected on public routes if the hook is invoked
      // before the `enabled` guard short-circuits (e.g. hydration races).
      // Treat it as "no session" rather than a hard error so the console
      // stays clean for unauthenticated visitors.
      if (r.status === 401) return null;
      if (!r.ok) throw new Error(`Session fetch failed: ${r.status}`);
      return r.json();
    },
    retry: false,
    staleTime: DETAIL_STALE_TIME_MS,
    refetchOnWindowFocus: true,
    enabled,
  });

  return {
    session: query.data?.data,
    isLoading: query.isLoading,
    isTenant: query.data?.data?.role === 'tenant',
    isAdmin: query.data?.data?.role === 'superadmin' || query.data?.data?.role === 'admin',
  };
}
