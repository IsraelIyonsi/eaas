// ============================================================
// EaaS Dashboard - API Key Repository
// ============================================================

import { HttpClient } from '../client';
import { ApiPaths } from '@/lib/constants/api-paths';
import type { ApiKey, CreateApiKeyRequest, CreateApiKeyResponse } from '@/types/api-key';

export class ApiKeyRepository extends HttpClient {
  async list(): Promise<ApiKey[]> {
    return this.get<ApiKey[]>(ApiPaths.API_KEYS);
  }

  async create(data: CreateApiKeyRequest): Promise<CreateApiKeyResponse> {
    return this.post<CreateApiKeyResponse>(ApiPaths.API_KEYS, data);
  }

  async rotate(id: string): Promise<CreateApiKeyResponse> {
    return this.post<CreateApiKeyResponse>(ApiPaths.API_KEY_ROTATE(id));
  }

  async revoke(id: string): Promise<void> {
    return this.post<void>(ApiPaths.API_KEY_REVOKE(id));
  }
}
