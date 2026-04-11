// ============================================================
// EaaS Dashboard - System Health Types
// ============================================================

export type HealthStatus = 'healthy' | 'degraded' | 'down';

export interface ServiceHealth {
  name: string;
  status: HealthStatus;
  latencyMs?: number;
}

export interface SystemHealth {
  status: HealthStatus;
  services: ServiceHealth[];
}
