// ============================================================
// EaaS Dashboard - Inbound Rule Repository
// ============================================================

import { CrudRepository } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { InboundRule, CreateInboundRuleRequest, UpdateInboundRuleRequest } from '@/types/inbound';

export class InboundRuleRepository extends CrudRepository<
  InboundRule,
  CreateInboundRuleRequest,
  UpdateInboundRuleRequest
> {
  protected readonly basePath = ApiPaths.INBOUND_RULES;
}
