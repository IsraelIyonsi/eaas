// ============================================================
// EaaS Dashboard - Analytics React Query Hooks
// ============================================================

import { useQuery } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';

export function useAnalyticsSummary(params?: {
  date_from?: string;
  date_to?: string;
  domainId?: string;
}) {
  return useQuery({
    queryKey: QueryKeys.analytics.summary(params),
    queryFn: () => repositories.analytics.getSummary(params),
    staleTime: STALE_TIME_MS,
  });
}

export function useAnalyticsTimeline(params?: {
  granularity?: 'hour' | 'day';
  date_from?: string;
  date_to?: string;
  domainId?: string;
}) {
  return useQuery({
    queryKey: QueryKeys.analytics.timeline(params),
    queryFn: () => repositories.analytics.getTimeline(params),
    staleTime: STALE_TIME_MS,
  });
}

export function useInboundAnalyticsSummary(params?: {
  date_from?: string;
  date_to?: string;
}) {
  return useQuery({
    queryKey: QueryKeys.analytics.inboundSummary(params),
    queryFn: () => repositories.analytics.getInboundSummary(params),
    staleTime: STALE_TIME_MS,
  });
}

export function useInboundAnalyticsTimeline(params?: {
  granularity?: 'hour' | 'day';
  date_from?: string;
  date_to?: string;
}) {
  return useQuery({
    queryKey: QueryKeys.analytics.inboundTimeline(params),
    queryFn: () => repositories.analytics.getInboundTimeline(params),
    staleTime: STALE_TIME_MS,
  });
}

export function useTopSenders(params?: {
  date_from?: string;
  date_to?: string;
  limit?: number;
}) {
  return useQuery({
    queryKey: QueryKeys.analytics.topSenders(params),
    queryFn: () => repositories.analytics.getTopSenders(params),
    staleTime: STALE_TIME_MS,
  });
}
