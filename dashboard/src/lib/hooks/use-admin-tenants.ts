// ============================================================
// EaaS Dashboard - Admin Tenant React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import type { AdminTenantListParams, UpdateTenantRequest } from '@/types/admin';

export function useAdminTenants(params?: AdminTenantListParams) {
  return useQuery({
    queryKey: QueryKeys.adminTenants.list(params as Record<string, unknown>),
    queryFn: () => repositories.adminTenant.list(params),
    staleTime: STALE_TIME_MS,
  });
}

export function useAdminTenant(id: string | undefined) {
  return useQuery({
    queryKey: QueryKeys.adminTenants.detail(id!),
    queryFn: () => repositories.adminTenant.getById(id!),
    enabled: !!id,
  });
}

export function useUpdateTenant() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateTenantRequest }) =>
      repositories.adminTenant.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.adminTenants.all });
    },
  });
}

export function useSuspendTenant() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.adminTenant.suspend(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.adminTenants.all });
    },
  });
}

export function useActivateTenant() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.adminTenant.activate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.adminTenants.all });
    },
  });
}

export function useDeleteTenant() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.adminTenant.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.adminTenants.all });
    },
  });
}
