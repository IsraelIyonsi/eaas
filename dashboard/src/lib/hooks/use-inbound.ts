// ============================================================
// EaaS Dashboard - Inbound Email & Rule React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import { useSession } from './use-session';
import type { InboundEmailListParams, CreateInboundRuleRequest, UpdateInboundRuleRequest } from '@/types/inbound';

// --- Inbound Emails ---

export function useInboundEmails(params?: InboundEmailListParams) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.inboundEmails.list(params as Record<string, unknown>),
    queryFn: () => repositories.inboundEmail.list(params),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}

export function useInboundEmail(id: string | undefined) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.inboundEmails.detail(id!),
    queryFn: () => repositories.inboundEmail.getById(id!),
    enabled: isTenant && !!id,
  });
}

export function useInboundThread(id: string | undefined) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.inboundEmails.thread(id!),
    queryFn: () => repositories.inboundEmail.getById(id!),
    enabled: isTenant && !!id,
  });
}

export function useRetryWebhook() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.inboundEmail.retryWebhook(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.inboundEmails.all });
    },
  });
}

export function useDeleteInboundEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.inboundEmail.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.inboundEmails.all });
    },
  });
}

// --- Inbound Rules ---

export function useInboundRules() {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.inboundRules.list(),
    queryFn: () => repositories.inboundRule.list(),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}

export function useInboundRule(id: string | undefined) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.inboundRules.detail(id!),
    queryFn: () => repositories.inboundRule.getById(id!),
    enabled: isTenant && !!id,
  });
}

export function useCreateInboundRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateInboundRuleRequest) => repositories.inboundRule.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.inboundRules.all });
    },
  });
}

export function useUpdateInboundRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateInboundRuleRequest }) =>
      repositories.inboundRule.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.inboundRules.all });
    },
  });
}

export function useDeleteInboundRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.inboundRule.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.inboundRules.all });
    },
  });
}
