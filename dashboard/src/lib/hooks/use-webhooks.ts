// ============================================================
// EaaS Dashboard - Webhook React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import type { CreateWebhookRequest, UpdateWebhookRequest } from '@/types/webhook';

export function useWebhooks() {
  return useQuery({
    queryKey: QueryKeys.webhooks.list(),
    queryFn: () => repositories.webhook.list(),
    staleTime: STALE_TIME_MS,
  });
}

export function useWebhook(id: string | undefined) {
  return useQuery({
    queryKey: QueryKeys.webhooks.detail(id!),
    queryFn: () => repositories.webhook.getById(id!),
    enabled: !!id,
  });
}

export function useCreateWebhook() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateWebhookRequest) => repositories.webhook.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.webhooks.all });
    },
  });
}

export function useUpdateWebhook() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateWebhookRequest }) =>
      repositories.webhook.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.webhooks.all });
    },
  });
}

export function useDeleteWebhook() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.webhook.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.webhooks.all });
    },
  });
}

export function useTestWebhook() {
  return useMutation({
    mutationFn: (id: string) => repositories.webhook.test(id),
  });
}

export function useWebhookDeliveries(
  id: string | undefined,
  params?: { page?: number; page_size?: number; success?: boolean },
) {
  return useQuery({
    queryKey: QueryKeys.webhooks.deliveries(id!, params),
    queryFn: () => repositories.webhook.getDeliveries(id!, params),
    enabled: !!id,
    staleTime: STALE_TIME_MS,
  });
}
