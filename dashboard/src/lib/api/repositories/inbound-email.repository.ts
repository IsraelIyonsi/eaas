// ============================================================
// EaaS Dashboard - Inbound Email Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { InboundEmail, InboundEmailListParams, VerdictStatus } from '@/types/inbound';
import type { PaginatedResponse } from '@/types/common';

/**
 * SES delivers verdicts as freeform strings ("PASS", "FAIL", "GRAY",
 * "PROCESSING_FAILED", "DISABLED", ...). The backend stores them untouched.
 * We quarantine that raw shape at the API boundary: anything that isn't
 * clearly a pass or fail collapses to "unknown" for the UI.
 */
function normalizeVerdict(raw: unknown): VerdictStatus | undefined {
  if (raw == null) return undefined;
  const value = String(raw).toLowerCase();
  if (value === 'pass') return 'pass';
  if (value === 'fail') return 'fail';
  return 'unknown';
}

function mapInboundEmail(raw: InboundEmail): InboundEmail {
  return {
    ...raw,
    spamVerdict: normalizeVerdict(raw.spamVerdict),
    virusVerdict: normalizeVerdict(raw.virusVerdict),
    spfVerdict: normalizeVerdict(raw.spfVerdict),
    dkimVerdict: normalizeVerdict(raw.dkimVerdict),
    dmarcVerdict: normalizeVerdict(raw.dmarcVerdict),
  };
}

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
    const response = await this.get<PaginatedResponse<InboundEmail>>(ApiPaths.INBOUND_EMAILS, queryParams);
    return { ...response, items: response.items.map(mapInboundEmail) };
  }

  async getById(id: string): Promise<InboundEmail> {
    const raw = await this.get<InboundEmail>(ApiPaths.INBOUND_EMAIL_BY_ID(id));
    return mapInboundEmail(raw);
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
