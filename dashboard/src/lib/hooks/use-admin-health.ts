// ============================================================
// EaaS Dashboard - Admin Health React Query Hook
// ============================================================

import { useQuery } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { HEALTH_POLL_INTERVAL_MS } from '@/lib/constants/ui';

export function useAdminSystemHealth() {
  return useQuery({
    queryKey: QueryKeys.adminHealth,
    queryFn: () => repositories.adminHealth.getSystemHealth(),
    refetchInterval: HEALTH_POLL_INTERVAL_MS,
  });
}
