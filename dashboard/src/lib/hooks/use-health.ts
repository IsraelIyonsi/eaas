// ============================================================
// EaaS Dashboard - Health React Query Hook
// ============================================================

import { useQuery } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { HEALTH_POLL_INTERVAL_MS } from '@/lib/constants/ui';

export function useHealth() {
  return useQuery({
    queryKey: QueryKeys.health,
    queryFn: () => repositories.health.getHealth(),
    refetchInterval: HEALTH_POLL_INTERVAL_MS,
    refetchIntervalInBackground: false,
  });
}
