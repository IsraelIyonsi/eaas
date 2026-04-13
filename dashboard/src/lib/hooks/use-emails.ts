// ============================================================
// EaaS Dashboard - Email React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS, DETAIL_STALE_TIME_MS } from '@/lib/constants/ui';
import { useSession } from './use-session';
import type { EmailListParams } from '@/types/email';

export function useEmails(params?: EmailListParams) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.emails.list(params as Record<string, unknown>),
    queryFn: () => repositories.email.list(params),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}

export function useEmail(id: string) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.emails.detail(id),
    queryFn: () => repositories.email.getById(id),
    enabled: isTenant && !!id,
    staleTime: DETAIL_STALE_TIME_MS,
  });
}

export function useEmailEvents(id: string | undefined) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.emails.events(id ?? ""),
    queryFn: () => repositories.email.getEvents(id ?? ""),
    enabled: isTenant && !!id,
    staleTime: DETAIL_STALE_TIME_MS,
  });
}

export function useDeleteEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.email.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.emails.all });
    },
  });
}
