// ============================================================
// EaaS Dashboard - Admin Health Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { AdminSystemHealth } from '@/types/admin';

export class AdminHealthRepository extends HttpClient {
  async getSystemHealth(): Promise<AdminSystemHealth> {
    try {
      return await this.get<AdminSystemHealth>(ApiPaths.ADMIN_SYSTEM_HEALTH);
    } catch {
      return {
        status: 'down',
        services: [
          { name: 'API', status: 'down' },
          { name: 'PostgreSQL', status: 'down' },
          { name: 'Redis', status: 'down' },
          { name: 'RabbitMQ', status: 'down' },
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
