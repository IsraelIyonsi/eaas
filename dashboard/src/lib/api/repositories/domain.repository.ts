// ============================================================
// EaaS Dashboard - Domain Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { Domain } from '@/types/domain';

export class DomainRepository extends HttpClient {
  async list(): Promise<Domain[]> {
    return this.get<Domain[]>(ApiPaths.DOMAINS);
  }

  async getById(id: string): Promise<Domain> {
    return this.get<Domain>(ApiPaths.DOMAIN_BY_ID(id));
  }

  async add(domainName: string): Promise<Domain> {
    return this.post<Domain>(ApiPaths.DOMAINS, { domainName });
  }

  async verify(id: string): Promise<Domain> {
    return this.post<Domain>(ApiPaths.DOMAIN_VERIFY(id));
  }

  async remove(id: string): Promise<void> {
    return this.del(ApiPaths.DOMAIN_BY_ID(id));
  }
}
