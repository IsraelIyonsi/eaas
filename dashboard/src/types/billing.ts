// ============================================================
// EaaS Dashboard - Billing Types
// ============================================================

export interface Plan {
  id: string;
  name: string;
  tier: string;
  monthlyPriceUsd: number;
  annualPriceUsd: number;
  dailyEmailLimit: number;
  monthlyEmailLimit: number;
  maxApiKeys: number;
  maxDomains: number;
  maxTemplates: number;
  maxWebhooks: number;
  customDomainBranding: boolean;
  prioritySupport: boolean;
  isActive: boolean;
}

export interface Subscription {
  id: string;
  planId: string;
  planName: string;
  planTier: string;
  status: string;
  provider: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  trialEndsAt?: string;
  cancelledAt?: string;
}

export interface Invoice {
  id: string;
  invoiceNumber: string;
  amountUsd: number;
  currency: string;
  status: string;
  periodStart: string;
  periodEnd: string;
  paidAt?: string;
  createdAt: string;
}

export interface InvoiceListParams {
  page?: number;
  page_size?: number;
}

export interface AdminPlanListParams {
  page?: number;
  page_size?: number;
}

export interface CreatePlanRequest {
  name: string;
  tier: string;
  monthlyPriceUsd: number;
  annualPriceUsd: number;
  dailyEmailLimit: number;
  monthlyEmailLimit: number;
  maxApiKeys: number;
  maxDomains: number;
  maxTemplates: number;
  maxWebhooks: number;
  customDomainBranding: boolean;
  prioritySupport: boolean;
}

export interface UpdatePlanRequest extends CreatePlanRequest {
  isActive: boolean;
}
