// ============================================================
// EaaS Dashboard - Webhook Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type {
  Webhook,
  WebhookDelivery,
  CreateWebhookRequest,
  UpdateWebhookRequest,
  TestWebhookResult,
} from '@/types/webhook';
import type { PaginatedResponse } from '@/types/common';

export class WebhookRepository extends HttpClient {
  async list(): Promise<Webhook[]> {
    return this.get<Webhook[]>(ApiPaths.WEBHOOKS);
  }

  async getById(id: string): Promise<Webhook> {
    return this.get<Webhook>(ApiPaths.WEBHOOK_BY_ID(id));
  }

  async create(data: CreateWebhookRequest): Promise<Webhook> {
    return this.post<Webhook>(ApiPaths.WEBHOOKS, data);
  }

  async update(id: string, data: UpdateWebhookRequest): Promise<Webhook> {
    return this.put<Webhook>(ApiPaths.WEBHOOK_BY_ID(id), data);
  }

  async remove(id: string): Promise<void> {
    return this.del(ApiPaths.WEBHOOK_BY_ID(id));
  }

  async test(id: string): Promise<TestWebhookResult> {
    return this.post<TestWebhookResult>(ApiPaths.WEBHOOK_TEST(id));
  }

  async getDeliveries(
    id: string,
    params?: { page?: number; page_size?: number; success?: boolean },
  ): Promise<PaginatedResponse<WebhookDelivery>> {
    const queryParams: Record<string, string> = {};
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    if (params?.success != null) queryParams.success = String(params.success);
    return this.get<PaginatedResponse<WebhookDelivery>>(
      ApiPaths.WEBHOOK_DELIVERIES(id),
      queryParams,
    );
  }
}
