// ============================================================
// EaaS Dashboard - Admin Billing Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { Plan, AdminPlanListParams, CreatePlanRequest, UpdatePlanRequest } from '@/types/billing';
import type { PaginatedResponse } from '@/types/common';

export class AdminBillingRepository extends HttpClient {
  async listPlans(params?: AdminPlanListParams): Promise<PaginatedResponse<Plan>> {
    const queryParams: Record<string, string> = {};
    if (params?.page) queryParams.page = String(params.page);
    if (params?.page_size) queryParams.page_size = String(params.page_size);
    return this.get<PaginatedResponse<Plan>>(ApiPaths.ADMIN_BILLING_PLANS, queryParams);
  }

  async createPlan(data: CreatePlanRequest): Promise<Plan> {
    return this.post<Plan>(ApiPaths.ADMIN_BILLING_PLANS, data);
  }

  async updatePlan(id: string, data: UpdatePlanRequest): Promise<Plan> {
    return this.put<Plan>(ApiPaths.ADMIN_BILLING_PLAN_BY_ID(id), data);
  }
}
