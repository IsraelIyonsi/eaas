// ============================================================
// EaaS Dashboard - Suppression Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { Suppression, CreateSuppressionRequest } from '@/types/suppression';
import type { PaginatedResponse } from '@/types/common';

export class SuppressionRepository extends HttpClient {
  async list(params?: {
    search?: string;
    reason?: string;
    page?: number;
    page_size?: number;
  }): Promise<PaginatedResponse<Suppression>> {
    const queryParams: Record<string, string> = {};
    if (params?.search) queryParams.search = params.search;
    if (params?.reason) queryParams.reason = params.reason;
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    return this.get<PaginatedResponse<Suppression>>(ApiPaths.SUPPRESSIONS, queryParams);
  }

  async create(data: CreateSuppressionRequest): Promise<Suppression> {
    return this.post<Suppression>(ApiPaths.SUPPRESSIONS, data);
  }

  async remove(id: string): Promise<void> {
    return this.del(ApiPaths.SUPPRESSION_BY_ID(id));
  }
}
