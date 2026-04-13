// ============================================================
// EaaS Dashboard - Health Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { SystemHealth } from '@/types/health';

export class HealthRepository extends HttpClient {
  async getHealth(): Promise<SystemHealth> {
    try {
      const raw = await this.get<SystemHealth | string>(ApiPaths.HEALTH);
      // Backend /health returns "Healthy" string, not structured data
      if (typeof raw === 'string') {
        const status = raw.toLowerCase() === 'healthy' ? 'healthy' : 'down';
        return {
          status: status as SystemHealth['status'],
          services: [
            { name: 'API', status: status as SystemHealth['status'] },
          ],
        };
      }
      return raw;
    } catch {
      return {
        status: 'down',
        services: [
          { name: 'API', status: 'down' },
          { name: 'Worker', status: 'down' },
          { name: 'RabbitMQ', status: 'down' },
          { name: 'PostgreSQL', status: 'down' },
          { name: 'Redis', status: 'down' },
        ],
      };
    }
  }
}
