// ============================================================
// EaaS Dashboard - API Key React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import { useSession } from './use-session';
import type { CreateApiKeyRequest } from '@/types/api-key';

export function useApiKeys() {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.apiKeys.list(),
    queryFn: () => repositories.apiKey.list(),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}

export function useCreateApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateApiKeyRequest) => repositories.apiKey.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.apiKeys.all });
    },
  });
}

export function useRotateApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.apiKey.rotate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.apiKeys.all });
    },
  });
}

export function useRevokeApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.apiKey.revoke(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.apiKeys.all });
    },
  });
}
