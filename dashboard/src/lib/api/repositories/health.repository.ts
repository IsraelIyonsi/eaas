// ============================================================
// EaaS Dashboard - Health Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { SystemHealth } from '@/types/health';

export class HealthRepository extends HttpClient {
  async getHealth(): Promise<SystemHealth> {
    try {
      return await this.get<SystemHealth>(ApiPaths.HEALTH);
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
