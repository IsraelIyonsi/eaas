// ============================================================
// EaaS Dashboard - Admin Audit Log Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { AuditLog, AuditLogListParams } from '@/types/admin';
import type { PaginatedResponse } from '@/types/common';

export class AdminAuditLogRepository extends HttpClient {
  async list(params?: AuditLogListParams): Promise<PaginatedResponse<AuditLog>> {
    const queryParams: Record<string, string> = {};
    if (params?.action) queryParams.action = params.action;
    if (params?.adminUserId) queryParams.adminUserId = params.adminUserId;
    if (params?.date_from) queryParams.date_from = params.date_from;
    if (params?.date_to) queryParams.date_to = params.date_to;
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    return this.get<PaginatedResponse<AuditLog>>(ApiPaths.ADMIN_AUDIT_LOGS, queryParams);
  }
}
