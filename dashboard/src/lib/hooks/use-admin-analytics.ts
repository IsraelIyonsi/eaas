// ============================================================
// EaaS Dashboard - Admin Analytics React Query Hooks
// ============================================================

import { useQuery } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';

export function useAdminPlatformSummary() {
  return useQuery({
    queryKey: QueryKeys.adminAnalytics.platformSummary,
    queryFn: () => repositories.adminAnalytics.getPlatformSummary(),
    staleTime: STALE_TIME_MS,
  });
}

export function useAdminTenantRankings(params?: { limit?: number; sort_by?: string }) {
  return useQuery({
    queryKey: QueryKeys.adminAnalytics.tenantRankings(params as Record<string, unknown>),
    queryFn: () => repositories.adminAnalytics.getTenantRankings(params),
    staleTime: STALE_TIME_MS,
  });
}

export function useAdminGrowthMetrics() {
  return useQuery({
    queryKey: QueryKeys.adminAnalytics.growthMetrics,
    queryFn: () => repositories.adminAnalytics.getGrowthMetrics(),
    staleTime: STALE_TIME_MS,
  });
}
