// ============================================================
// EaaS Dashboard - Admin Types
// ============================================================

import type { PaginationParams, SortParams, DateRangeParams } from './common';

// --- Enums ---

export type TenantStatus = 'active' | 'suspended' | 'deactivated';
export type AdminRole = 'super_admin' | 'superadmin' | 'admin' | 'read_only' | 'readonly';

// --- Entities ---

export interface AdminTenant {
  id: string;
  name: string;
  company?: string;
  email: string;
  status: TenantStatus;
  emailCount: number;
  domainCount: number;
  apiKeyCount: number;
  dailyEmailLimit: number;
  monthlyEmailLimit: number;
  createdAt: string;
  updatedAt: string;
}

export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  role: AdminRole;
  isActive: boolean;
  lastLoginAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface AuditLog {
  id: string;
  adminUserId: string;
  adminEmail: string;
  action: string;
  targetType: string;
  targetId?: string;
  targetName?: string;
  details?: string;
  ipAddress: string;
  createdAt: string;
}

// --- Analytics ---

export interface PlatformSummary {
  totalTenants: number;
  activeTenants: number;
  totalEmails: number;
  totalDomains: number;
  totalApiKeys: number;
  emailsToday: number;
  emailsThisMonth: number;
}

export interface TenantRanking {
  tenantId: string;
  tenantName: string;
  company?: string;
  emailCount: number;
  domainCount: number;
}

export interface GrowthMetrics {
  newTenantsThisMonth: number;
  newTenantsLastMonth: number;
  emailGrowthPercent: number;
  tenantGrowthPercent: number;
}

export interface AdminSystemHealth {
  status: string;
  services: AdminServiceHealth[];
  metrics: AdminHealthMetrics;
}

export interface AdminServiceHealth {
  name: string;
  status: string;
  latencyMs?: number;
  message?: string;
}

export interface AdminHealthMetrics {
  tenantCount: number;
  totalEmailsSent: number;
  queueDepth: number;
  avgLatencyMs: number;
}

// --- Request Types ---

export interface AdminTenantListParams extends PaginationParams, SortParams {
  search?: string;
  status?: TenantStatus;
}

export interface AdminUserListParams extends PaginationParams {
  search?: string;
  role?: AdminRole;
}

export interface AuditLogListParams extends PaginationParams, DateRangeParams {
  action?: string;
  adminUserId?: string;
}

export interface CreateAdminUserRequest {
  email: string;
  displayName: string;
  role: AdminRole;
  password: string;
}

export interface UpdateTenantRequest {
  name?: string;
  company?: string;
  dailyEmailLimit?: number;
  monthlyEmailLimit?: number;
}
