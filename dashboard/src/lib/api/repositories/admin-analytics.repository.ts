// ============================================================
// EaaS Dashboard - Admin Analytics Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { PlatformSummary, TenantRanking, GrowthMetrics } from '@/types/admin';

export class AdminAnalyticsRepository extends HttpClient {
  async getPlatformSummary(): Promise<PlatformSummary> {
    return this.get<PlatformSummary>(ApiPaths.ADMIN_PLATFORM_SUMMARY);
  }

  async getTenantRankings(params?: {
    limit?: number;
    sort_by?: string;
  }): Promise<TenantRanking[]> {
    const queryParams: Record<string, string> = {};
    if (params?.limit) queryParams.limit = String(params.limit);
    if (params?.sort_by) queryParams.sort_by = params.sort_by;
    return this.get<TenantRanking[]>(ApiPaths.ADMIN_TENANT_RANKINGS, queryParams);
  }

  async getGrowthMetrics(): Promise<GrowthMetrics> {
    return this.get<GrowthMetrics>(ApiPaths.ADMIN_GROWTH_METRICS);
  }
}
