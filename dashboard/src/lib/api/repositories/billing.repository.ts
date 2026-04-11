// ============================================================
// EaaS Dashboard - Billing Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { Plan, Subscription, Invoice, InvoiceListParams } from '@/types/billing';
import type { PaginatedResponse } from '@/types/common';

export class BillingRepository extends HttpClient {
  async getPlans(): Promise<Plan[]> {
    return this.get<Plan[]>(ApiPaths.BILLING_PLANS);
  }

  async getCurrentSubscription(): Promise<Subscription> {
    return this.get<Subscription>(ApiPaths.BILLING_SUBSCRIPTION);
  }

  async createSubscription(planId: string): Promise<Subscription> {
    return this.post<Subscription>(ApiPaths.BILLING_SUBSCRIBE, { planId });
  }

  async cancelSubscription(immediate?: boolean): Promise<void> {
    return this.post<void>(ApiPaths.BILLING_CANCEL, { immediate: immediate ?? false });
  }

  async getInvoices(params?: InvoiceListParams): Promise<PaginatedResponse<Invoice>> {
    const queryParams: Record<string, string> = {};
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    return this.get<PaginatedResponse<Invoice>>(ApiPaths.BILLING_INVOICES, queryParams);
  }
}
