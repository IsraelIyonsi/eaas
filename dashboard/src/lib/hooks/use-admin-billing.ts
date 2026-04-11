// ============================================================
// EaaS Dashboard - Admin Billing React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import type { AdminPlanListParams, CreatePlanRequest, UpdatePlanRequest } from '@/types/billing';

export function useAdminPlans(params?: AdminPlanListParams) {
  return useQuery({
    queryKey: QueryKeys.adminBilling.plans(params as Record<string, unknown>),
    queryFn: () => repositories.adminBilling.listPlans(params),
    staleTime: STALE_TIME_MS,
  });
}

export function useCreatePlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreatePlanRequest) =>
      repositories.adminBilling.createPlan(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.adminBilling.all });
    },
  });
}

export function useUpdatePlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdatePlanRequest }) =>
      repositories.adminBilling.updatePlan(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.adminBilling.all });
    },
  });
}
