// ============================================================
// EaaS Dashboard - Analytics Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { AnalyticsSummary, AnalyticsTimeline, InboundAnalyticsSummary, TopSender } from '@/types/analytics';

export class AnalyticsRepository extends HttpClient {
  async getSummary(params?: {
    date_from?: string;
    date_to?: string;
    domainId?: string;
  }): Promise<AnalyticsSummary> {
    const queryParams: Record<string, string> = {};
    if (params?.date_from) queryParams.date_from = params.date_from;
    if (params?.date_to) queryParams.date_to = params.date_to;
    if (params?.domainId) queryParams.domainId = params.domainId;
    return this.get<AnalyticsSummary>(ApiPaths.ANALYTICS_SUMMARY, queryParams);
  }

  async getTimeline(params?: {
    granularity?: 'hour' | 'day';
    date_from?: string;
    date_to?: string;
    domainId?: string;
  }): Promise<AnalyticsTimeline> {
    const queryParams: Record<string, string> = {};
    if (params?.granularity) queryParams.granularity = params.granularity;
    if (params?.date_from) queryParams.date_from = params.date_from;
    if (params?.date_to) queryParams.date_to = params.date_to;
    if (params?.domainId) queryParams.domainId = params.domainId;
    return this.get<AnalyticsTimeline>(ApiPaths.ANALYTICS_TIMELINE, queryParams);
  }

  async getInboundSummary(params?: {
    date_from?: string;
    date_to?: string;
  }): Promise<InboundAnalyticsSummary> {
    const queryParams: Record<string, string> = {};
    if (params?.date_from) queryParams.date_from = params.date_from;
    if (params?.date_to) queryParams.date_to = params.date_to;
    return this.get<InboundAnalyticsSummary>(ApiPaths.ANALYTICS_INBOUND_SUMMARY, queryParams);
  }

  async getInboundTimeline(params?: {
    granularity?: 'hour' | 'day';
    date_from?: string;
    date_to?: string;
  }): Promise<AnalyticsTimeline> {
    const queryParams: Record<string, string> = {};
    if (params?.granularity) queryParams.granularity = params.granularity;
    if (params?.date_from) queryParams.date_from = params.date_from;
    if (params?.date_to) queryParams.date_to = params.date_to;
    return this.get<AnalyticsTimeline>(ApiPaths.ANALYTICS_INBOUND_TIMELINE, queryParams);
  }

  async getTopSenders(params?: {
    date_from?: string;
    date_to?: string;
    limit?: number;
  }): Promise<TopSender[]> {
    const queryParams: Record<string, string> = {};
    if (params?.date_from) queryParams.date_from = params.date_from;
    if (params?.date_to) queryParams.date_to = params.date_to;
    if (params?.limit) queryParams.limit = String(params.limit);
    return this.get<TopSender[]>(ApiPaths.ANALYTICS_TOP_SENDERS, queryParams);
  }
}
