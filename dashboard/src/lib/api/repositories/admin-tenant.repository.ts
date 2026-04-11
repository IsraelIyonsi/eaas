// ============================================================
// EaaS Dashboard - Admin Tenant Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { AdminTenant, AdminTenantListParams, UpdateTenantRequest } from '@/types/admin';
import type { PaginatedResponse } from '@/types/common';

export class AdminTenantRepository extends HttpClient {
  async list(params?: AdminTenantListParams): Promise<PaginatedResponse<AdminTenant>> {
    const queryParams: Record<string, string> = {};
    if (params?.search) queryParams.search = params.search;
    if (params?.status) queryParams.status = params.status;
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    if (params?.sort_by) queryParams.sort_by = params.sort_by;
    if (params?.sort_dir) queryParams.sort_dir = params.sort_dir;
    return this.get<PaginatedResponse<AdminTenant>>(ApiPaths.ADMIN_TENANTS, queryParams);
  }

  async getById(id: string): Promise<AdminTenant> {
    return this.get<AdminTenant>(ApiPaths.ADMIN_TENANT_BY_ID(id));
  }

  async update(id: string, data: UpdateTenantRequest): Promise<AdminTenant> {
    return this.put<AdminTenant>(ApiPaths.ADMIN_TENANT_BY_ID(id), data);
  }

  async suspend(id: string): Promise<void> {
    return this.post<void>(ApiPaths.ADMIN_TENANT_SUSPEND(id));
  }

  async activate(id: string): Promise<void> {
    return this.post<void>(ApiPaths.ADMIN_TENANT_ACTIVATE(id));
  }

  async remove(id: string): Promise<void> {
    return this.del(ApiPaths.ADMIN_TENANT_BY_ID(id));
  }
}
