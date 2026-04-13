// ============================================================
// EaaS Dashboard - Generic HTTP Client & CRUD Repository
// ============================================================

import type { PaginatedResponse } from '@/types/common';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

// Guard to prevent multiple concurrent 401 redirects
let isRedirectingToLogin = false;

export class HttpClient {
  private baseHeaders(): HeadersInit {
    return { 'Content-Type': 'application/json' };
  }

  protected async request<T>(
    method: string,
    path: string,
    body?: unknown,
    params?: Record<string, string>,
  ): Promise<T> {
    const url = new URL(`/api/proxy${path}`, window.location.origin);
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value != null && value !== '') {
          url.searchParams.set(key, value);
        }
      }
    }

    const res = await fetch(url.toString(), {
      method,
      headers: this.baseHeaders(),
      credentials: 'include',
      body: body ? JSON.stringify(body) : undefined,
    });

    if (res.status === 401) {
      if (typeof window !== 'undefined' && !isRedirectingToLogin) {
        isRedirectingToLogin = true;
        window.location.href = '/login';
      }
      throw new ApiError(401, 'Session expired');
    }

    if (!res.ok) {
      const text = await res.text();
      throw new ApiError(res.status, text || `HTTP ${res.status}`);
    }

    if (res.status === 204) {
      return undefined as T;
    }

    const json = await res.json();
    if (json.success === false) {
      throw new ApiError(400, json.error?.message ?? json.error ?? 'Unknown error');
    }

    return json.data ?? json;
  }

  protected get<T>(path: string, params?: Record<string, string>): Promise<T> {
    return this.request<T>('GET', path, undefined, params);
  }

  protected post<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('POST', path, body);
  }

  protected put<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('PUT', path, body);
  }

  protected del(path: string): Promise<void> {
    return this.request<void>('DELETE', path);
  }
}

export abstract class CrudRepository<
  TEntity,
  TCreateRequest,
  TUpdateRequest = Partial<TCreateRequest>,
> extends HttpClient {
  protected abstract readonly basePath: string;

  async list(params?: Record<string, string>): Promise<PaginatedResponse<TEntity>> {
    return this.get<PaginatedResponse<TEntity>>(this.basePath, params);
  }

  async getById(id: string): Promise<TEntity> {
    return this.get<TEntity>(`${this.basePath}/${id}`);
  }

  async create(data: TCreateRequest): Promise<TEntity> {
    return this.post<TEntity>(this.basePath, data);
  }

  async update(id: string, data: TUpdateRequest): Promise<TEntity> {
    return this.put<TEntity>(`${this.basePath}/${id}`, data);
  }

  async remove(id: string): Promise<void> {
    return this.del(`${this.basePath}/${id}`);
  }
}
