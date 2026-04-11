// ============================================================
// EaaS Dashboard - Admin User Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { AdminUser, AdminUserListParams, CreateAdminUserRequest } from '@/types/admin';
import type { PaginatedResponse } from '@/types/common';

export class AdminUserRepository extends HttpClient {
  async list(params?: AdminUserListParams): Promise<PaginatedResponse<AdminUser>> {
    const queryParams: Record<string, string> = {};
    if (params?.search) queryParams.search = params.search;
    if (params?.role) queryParams.role = params.role;
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    return this.get<PaginatedResponse<AdminUser>>(ApiPaths.ADMIN_USERS, queryParams);
  }

  async create(data: CreateAdminUserRequest): Promise<AdminUser> {
    return this.post<AdminUser>(ApiPaths.ADMIN_USERS, data);
  }

  async remove(id: string): Promise<void> {
    return this.del(ApiPaths.ADMIN_USER_BY_ID(id));
  }
}
