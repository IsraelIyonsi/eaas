// ============================================================
// EaaS Dashboard - Admin Audit Log React Query Hooks
// ============================================================

import { useQuery } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import type { AuditLogListParams } from '@/types/admin';

export function useAdminAuditLogs(params?: AuditLogListParams) {
  return useQuery({
    queryKey: QueryKeys.adminAuditLogs.list(params as Record<string, unknown>),
    queryFn: () => repositories.adminAuditLog.list(params),
    staleTime: STALE_TIME_MS,
  });
}
