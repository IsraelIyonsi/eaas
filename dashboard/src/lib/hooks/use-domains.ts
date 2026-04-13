// ============================================================
// EaaS Dashboard - Domain React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import { useSession } from './use-session';

export function useDomains() {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.domains.list(),
    queryFn: () => repositories.domain.list(),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}

export function useDomain(id: string | undefined) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.domains.detail(id!),
    queryFn: () => repositories.domain.getById(id!),
    enabled: isTenant && !!id,
  });
}

export function useAddDomain() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (domain: string) => repositories.domain.add(domain),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.domains.all });
    },
  });
}

export function useVerifyDomain() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.domain.verify(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.domains.all });
    },
  });
}

export function useDeleteDomain() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.domain.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.domains.all });
    },
  });
}
