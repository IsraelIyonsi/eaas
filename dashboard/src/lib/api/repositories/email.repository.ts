// ============================================================
// EaaS Dashboard - Email Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { Email, EmailEvent, EmailListParams } from '@/types/email';
import type { PaginatedResponse } from '@/types/common';

export class EmailRepository extends HttpClient {
  async list(params?: EmailListParams): Promise<PaginatedResponse<Email>> {
    const queryParams: Record<string, string> = {};
    if (params?.status) queryParams.status = params.status;
    if (params?.date_from) queryParams.date_from = params.date_from;
    if (params?.date_to) queryParams.date_to = params.date_to;
    if (params?.search) queryParams.to = params.search;
    if (params?.templateId) queryParams.templateId = params.templateId;
    if (params?.tags) queryParams.tags = params.tags;
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    if (params?.sort_by) queryParams.sort_by = params.sort_by;
    if (params?.sort_dir) queryParams.sort_dir = params.sort_dir;
    return this.get<PaginatedResponse<Email>>(ApiPaths.EMAILS, queryParams);
  }

  async getById(id: string): Promise<Email> {
    return this.get<Email>(ApiPaths.EMAIL_BY_ID(id));
  }

  async getEvents(id: string): Promise<EmailEvent[]> {
    return this.get<EmailEvent[]>(ApiPaths.EMAIL_EVENTS(id));
  }

  async remove(id: string): Promise<void> {
    return this.del(ApiPaths.EMAIL_BY_ID(id));
  }
}
