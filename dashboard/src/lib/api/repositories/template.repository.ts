// ============================================================
// EaaS Dashboard - Template Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { Template, CreateTemplateRequest, UpdateTemplateRequest } from '@/types/template';
import type { PaginatedResponse } from '@/types/common';

export class TemplateRepository extends HttpClient {
  async list(params?: { search?: string; page?: number; page_size?: number }): Promise<PaginatedResponse<Template>> {
    const queryParams: Record<string, string> = {};
    if (params?.search) queryParams.search = params.search;
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    return this.get<PaginatedResponse<Template>>(ApiPaths.TEMPLATES, queryParams);
  }

  async getById(id: string): Promise<Template> {
    return this.get<Template>(ApiPaths.TEMPLATE_BY_ID(id));
  }

  async create(data: CreateTemplateRequest): Promise<Template> {
    return this.post<Template>(ApiPaths.TEMPLATES, data);
  }

  async update(id: string, data: UpdateTemplateRequest): Promise<Template> {
    return this.put<Template>(ApiPaths.TEMPLATE_BY_ID(id), data);
  }

  async remove(id: string): Promise<void> {
    return this.del(ApiPaths.TEMPLATE_BY_ID(id));
  }

  async preview(id: string, variables?: Record<string, unknown>): Promise<{ subject: string; htmlBody: string; textBody: string }> {
    return this.post<{ subject: string; htmlBody: string; textBody: string }>(
      ApiPaths.TEMPLATE_PREVIEW(id),
      { variables },
    );
  }
}
