// ============================================================
// EaaS Dashboard - Billing React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import { useSession } from './use-session';
import type { InvoiceListParams } from '@/types/billing';

export function usePlans() {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.billing.plans,
    queryFn: () => repositories.billing.getPlans(),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}

export function useCurrentSubscription() {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.billing.subscription,
    queryFn: () => repositories.billing.getCurrentSubscription(),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}

export function useCreateSubscription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (planId: string) => repositories.billing.createSubscription(planId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.billing.subscription });
      queryClient.invalidateQueries({ queryKey: QueryKeys.billing.plans });
    },
  });
}

export function useCancelSubscription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (immediate?: boolean) => repositories.billing.cancelSubscription(immediate),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.billing.subscription });
    },
  });
}

export function useInvoices(params?: InvoiceListParams) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.billing.invoices(params as Record<string, unknown>),
    queryFn: () => repositories.billing.getInvoices(params),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}
