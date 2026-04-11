// ============================================================
// EaaS Dashboard - Email React Query Hooks
// ============================================================

import { useQuery } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import type { EmailListParams } from '@/types/email';

export function useEmails(params?: EmailListParams) {
  return useQuery({
    queryKey: QueryKeys.emails.list(params as Record<string, unknown>),
    queryFn: () => repositories.email.list(params),
    staleTime: STALE_TIME_MS,
  });
}

export function useEmail(id: string) {
  return useQuery({
    queryKey: QueryKeys.emails.detail(id),
    queryFn: () => repositories.email.getById(id),
    enabled: !!id,
  });
}

export function useEmailEvents(id: string | undefined) {
  return useQuery({
    queryKey: QueryKeys.emails.events(id!),
    queryFn: () => repositories.email.getEvents(id!),
    enabled: !!id,
  });
}
