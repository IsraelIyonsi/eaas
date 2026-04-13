// ============================================================
// EaaS Dashboard - Health React Query Hook
// ============================================================

import { useQuery } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { HEALTH_POLL_INTERVAL_MS } from '@/lib/constants/ui';
import { useSession } from './use-session';

export function useHealth() {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.health,
    queryFn: () => repositories.health.getHealth(),
    refetchInterval: isTenant ? HEALTH_POLL_INTERVAL_MS : false,
    refetchIntervalInBackground: false,
    enabled: isTenant,
  });
}
