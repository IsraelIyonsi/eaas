// ============================================================
// EaaS Dashboard - Template React Query Hooks
// ============================================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositories } from '@/lib/api/index';
import { QueryKeys } from '@/lib/constants/query-keys';
import { STALE_TIME_MS } from '@/lib/constants/ui';
import { useSession } from './use-session';
import type { CreateTemplateRequest, UpdateTemplateRequest, TemplateVersion } from '@/types/template';

export function useTemplates(params?: { search?: string; page?: number; page_size?: number }) {
  const { isTenant } = useSession();
  return useQuery({
    queryKey: QueryKeys.templates.list(params),
    queryFn: () => repositories.template.list(params),
    staleTime: STALE_TIME_MS,
    enabled: isTenant,
  });
}

export function useTemplate(id: string | undefined) {
  return useQuery({
    queryKey: QueryKeys.templates.detail(id!),
    queryFn: () => repositories.template.getById(id!),
    enabled: !!id,
  });
}

export function useCreateTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateTemplateRequest) => repositories.template.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.templates.all });
    },
  });
}

export function useUpdateTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateTemplateRequest }) =>
      repositories.template.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.templates.all });
    },
  });
}

export function useDeleteTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => repositories.template.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.templates.all });
    },
  });
}

export function usePreviewTemplate() {
  return useMutation({
    mutationFn: ({ id, variables }: { id: string; variables?: Record<string, unknown> }) =>
      repositories.template.preview(id, variables),
  });
}

export function useTemplateVersions(id: string | undefined) {
  return useQuery<{ items: TemplateVersion[]; totalCount: number }>({
    queryKey: QueryKeys.templates.versions(id!),
    queryFn: () => repositories.template.listVersions(id!),
    enabled: !!id,
  });
}

export function useRollbackTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, version }: { id: string; version: number }) =>
      repositories.template.rollback(id, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QueryKeys.templates.all });
    },
  });
}
