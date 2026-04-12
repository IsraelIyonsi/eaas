// ============================================================
// EaaS Dashboard - Admin Health Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { AdminSystemHealth } from '@/types/admin';

interface RawHealthResponse {
  status: string;
  database: { status: string; error: string | null };
  redis: { status: string; error: string | null };
  tenantCount: number;
  emailCount: number;
}

export class AdminHealthRepository extends HttpClient {
  async getSystemHealth(): Promise<AdminSystemHealth> {
    try {
      const raw = await this.get<RawHealthResponse>(ApiPaths.ADMIN_SYSTEM_HEALTH);

      return {
        status: raw.status,
        services: [
          { name: 'API', status: raw.status === 'healthy' ? 'healthy' : 'down' },
          { name: 'PostgreSQL', status: raw.database?.status ?? 'down', message: raw.database?.error ?? undefined },
          { name: 'Redis', status: raw.redis?.status ?? 'down', message: raw.redis?.error ?? undefined },
        ],
        metrics: {
          tenantCount: raw.tenantCount ?? 0,
          totalEmailsSent: raw.emailCount ?? 0,
          queueDepth: 0,
          avgLatencyMs: 0,
        },
      };
    } catch {
      return {
        status: 'down',
        services: [
          { name: 'API', status: 'down' },
          { name: 'PostgreSQL', status: 'down' },
          { name: 'Redis', status: 'down' },
        ],
        metrics: {
          tenantCount: 0,
          totalEmailsSent: 0,
          queueDepth: 0,
          avgLatencyMs: 0,
        },
      };
    }
  }
}
