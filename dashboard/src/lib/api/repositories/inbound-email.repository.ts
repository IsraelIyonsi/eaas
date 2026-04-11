// ============================================================
// EaaS Dashboard - Inbound Email Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { InboundEmail, InboundEmailListParams } from '@/types/inbound';
import type { PaginatedResponse } from '@/types/common';

export class InboundEmailRepository extends HttpClient {
  async list(params?: InboundEmailListParams): Promise<PaginatedResponse<InboundEmail>> {
    const queryParams: Record<string, string> = {};
    if (params?.status) queryParams.status = params.status;
    if (params?.from) queryParams.from = params.from;
    if (params?.to) queryParams.to = params.to;
    if (params?.date_from) queryParams.date_from = params.date_from;
    if (params?.date_to) queryParams.date_to = params.date_to;
    if (params?.has_attachments != null) queryParams.has_attachments = String(params.has_attachments);
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    return this.get<PaginatedResponse<InboundEmail>>(ApiPaths.INBOUND_EMAILS, queryParams);
  }

  async getById(id: string): Promise<InboundEmail> {
    return this.get<InboundEmail>(ApiPaths.INBOUND_EMAIL_BY_ID(id));
  }

  async getRawUrl(id: string): Promise<string> {
    return this.get<string>(ApiPaths.INBOUND_EMAIL_RAW(id));
  }

  async getAttachmentUrl(emailId: string, attachmentId: string): Promise<string> {
    return this.get<string>(ApiPaths.INBOUND_EMAIL_ATTACHMENT(emailId, attachmentId));
  }

  async remove(id: string): Promise<void> {
    return this.del(ApiPaths.INBOUND_EMAIL_BY_ID(id));
  }

  async retryWebhook(id: string): Promise<void> {
    return this.post<void>(ApiPaths.INBOUND_EMAIL_RETRY_WEBHOOK(id));
  }
}
