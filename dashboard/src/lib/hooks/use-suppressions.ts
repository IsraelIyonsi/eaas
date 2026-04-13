// ============================================================
// EaaS Dashboard - Suppression React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import { useSession } from './use-session';
import type { CreateSuppressionRequest } from '@/types/suppression';

export function useSuppressions(params?: {
  search?: string;
  reason?: string;
  page?: number;
  page_size?: number;
}) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.suppressions.list(params),
    queryFn: () => repositories.suppression.list(params),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}

export function useCreateSuppression() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateSuppressionRequest) => repositories.suppression.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.suppressions.all });
    },
  });
}

export function useDeleteSuppression() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.suppression.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.suppressions.all });
    },
  });
}
