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

export function useSession() {
  const query = useQuery<SessionResponse>({
    queryKey: ['session'],
    queryFn: () => fetch('/api/auth/me').then((r) => r.json()),
    retry: false,
    staleTime: DETAIL_STALE_TIME_MS,
    refetchOnWindowFocus: true,
  });

  return {
    session: query.data?.data,
    isLoading: query.isLoading,
    isTenant: query.data?.data?.role === 'tenant',
    isAdmin: query.data?.data?.role === 'superadmin' || query.data?.data?.role === 'admin',
  };
}
