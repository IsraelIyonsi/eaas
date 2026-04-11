// ============================================================
// EaaS Dashboard - Admin User React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import type { AdminUserListParams, CreateAdminUserRequest } from '@/types/admin';

export function useAdminUsers(params?: AdminUserListParams) {
  return useQuery({
    queryKey: QueryKeys.adminUsers.list(params as Record<string, unknown>),
    queryFn: () => repositories.adminUser.list(params),
    staleTime: STALE_TIME_MS,
  });
}

export function useCreateAdminUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateAdminUserRequest) =>
      repositories.adminUser.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.adminUsers.all });
    },
  });
}

export function useDeleteAdminUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.adminUser.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.adminUsers.all });
    },
  });
}
