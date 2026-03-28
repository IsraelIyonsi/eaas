import type {
  AnalyticsSummary,
  AnalyticsTimeline,
  Email,
  EmailEvent,
  Template,
  Domain,
  Suppression,
  PaginatedResponse,
  SystemHealth,
} from "@/types";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

class ApiClient {
  private headers(): HeadersInit {
    return { "Content-Type": "application/json" };
  }

  private async request<T>(
    method: string,
    path: string,
    body?: unknown,
    params?: Record<string, string>,
  ): Promise<T> {
    // Route through the Next.js proxy to avoid CORS and keep API key server-side
    const url = new URL(`/api/proxy${path}`, window.location.origin);
    if (params) {
      Object.entries(params).forEach(([k, v]) => {
        if (v) url.searchParams.set(k, v);
      });
    }

    const res = await fetch(url.toString(), {
      method,
      headers: this.headers(),
      credentials: "include",
      body: body ? JSON.stringify(body) : undefined,
    });

    if (res.status === 401) {
      // Session expired — redirect to login
      if (typeof window !== "undefined") {
        window.location.href = "/login";
      }
      throw new ApiError(401, "Session expired");
    }

    if (!res.ok) {
      const text = await res.text();
      throw new ApiError(res.status, text || `HTTP ${res.status}`);
    }

    const json = await res.json();
    if (json.success === false) {
      throw new ApiError(400, json.error ?? "Unknown error");
    }
    return json.data ?? json;
  }

  // --- Analytics ---
  async getAnalyticsSummary(): Promise<AnalyticsSummary> {
    return this.request("GET", "/api/v1/analytics/summary");
  }

  async getAnalyticsTimeline(
    granularity: "hour" | "day" = "day",
  ): Promise<AnalyticsTimeline> {
    return this.request("GET", "/api/v1/analytics/timeline", undefined, {
      granularity,
    });
  }

  // --- Emails ---
  async getEmails(params?: {
    status?: string;
    date_from?: string;
    date_to?: string;
    search?: string;
    page?: number;
    page_size?: number;
  }): Promise<PaginatedResponse<Email>> {
    const p: Record<string, string> = {};
    if (params?.status) p.status = params.status;
    if (params?.date_from) p.date_from = params.date_from;
    if (params?.date_to) p.date_to = params.date_to;
    if (params?.search) p.to = params.search;
    if (params?.page) p.page = String(params.page);
    if (params?.page_size) p.page_size = String(params.page_size);
    p.sort_by = "created_at";
    p.sort_dir = "desc";
    return this.request("GET", "/api/v1/emails", undefined, p);
  }

  async getEmail(id: string): Promise<Email | undefined> {
    return this.request("GET", `/api/v1/emails/${id}`);
  }

  async getEmailEvents(email: Email): Promise<EmailEvent[]> {
    return this.request("GET", `/api/v1/emails/${email.id}/events`);
  }

  // --- Templates ---
  async getTemplates(
    search?: string,
    page = 1,
    pageSize = 20,
  ): Promise<PaginatedResponse<Template>> {
    return this.request("GET", "/api/v1/templates", undefined, {
      search: search ?? "",
      page: String(page),
      page_size: String(pageSize),
    });
  }

  async createTemplate(
    data: Pick<Template, "name" | "subject" | "html_body" | "text_body">,
  ): Promise<Template> {
    return this.request("POST", "/api/v1/templates", data);
  }

  async updateTemplate(
    id: string,
    data: Partial<Pick<Template, "name" | "subject" | "html_body" | "text_body">>,
  ): Promise<Template> {
    return this.request("PUT", `/api/v1/templates/${id}`, data);
  }

  async deleteTemplate(id: string): Promise<void> {
    await this.request("DELETE", `/api/v1/templates/${id}`);
  }

  // --- Domains ---
  async getDomains(): Promise<Domain[]> {
    const res = await this.request<PaginatedResponse<Domain>>(
      "GET",
      "/api/v1/domains",
    );
    return res.items;
  }

  async addDomain(domain: string): Promise<Domain> {
    return this.request("POST", "/api/v1/domains", { domain });
  }

  async verifyDomain(id: string): Promise<Domain> {
    return this.request("POST", `/api/v1/domains/${id}/verify`);
  }

  // --- Suppressions ---
  async getSuppressions(params?: {
    search?: string;
    reason?: string;
    page?: number;
    page_size?: number;
  }): Promise<PaginatedResponse<Suppression>> {
    const p: Record<string, string> = {};
    if (params?.search) p.search = params.search;
    if (params?.reason) p.reason = params.reason;
    if (params?.page) p.page = String(params.page);
    if (params?.page_size) p.page_size = String(params.page_size);
    return this.request("GET", "/api/v1/suppressions", undefined, p);
  }

  async addSuppression(
    email: string,
    reason: string,
  ): Promise<Suppression> {
    return this.request("POST", "/api/v1/suppressions", { email, reason });
  }

  async removeSuppression(id: string): Promise<void> {
    await this.request("DELETE", `/api/v1/suppressions/${id}`);
  }

  // --- Health ---
  async getHealth(): Promise<SystemHealth> {
    try {
      return await this.request("GET", "/health");
    } catch {
      return {
        status: "down",
        services: [
          { name: "API", status: "down" },
          { name: "Worker", status: "down" },
          { name: "RabbitMQ", status: "down" },
          { name: "PostgreSQL", status: "down" },
          { name: "Redis", status: "down" },
        ],
      };
    }
  }
}

export const api = new ApiClient();
