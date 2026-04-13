"use client";

import { useState, useCallback } from "react";
import { PageHeader } from "@/components/shared/page-header";
import { CopyButton } from "@/components/shared/copy-button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Play,
  Plus,
  Trash2,
  ChevronDown,
  ChevronRight,
  Clock,
  Loader2,
} from "lucide-react";
import { cn } from "@/lib/utils";

// ---------------------------------------------------------------------------
// Endpoint definitions
// ---------------------------------------------------------------------------

interface Endpoint {
  method: "GET" | "POST" | "PUT" | "DELETE";
  path: string;
  label: string;
  sampleBody?: string;
}

interface EndpointGroup {
  section: string;
  endpoints: Endpoint[];
}

const ENDPOINT_GROUPS: EndpointGroup[] = [
  {
    section: "Emails",
    endpoints: [
      { method: "GET", path: "/api/v1/emails", label: "List sent emails" },
      {
        method: "GET",
        path: "/api/v1/emails/:id",
        label: "Get email detail",
      },
    ],
  },
  {
    section: "Inbound Emails",
    endpoints: [
      {
        method: "GET",
        path: "/api/v1/inbound/emails",
        label: "List received emails",
      },
      {
        method: "GET",
        path: "/api/v1/inbound/emails/:id",
        label: "Get inbound email detail",
      },
    ],
  },
  {
    section: "Inbound Rules",
    endpoints: [
      { method: "GET", path: "/api/v1/inbound/rules", label: "List rules" },
      {
        method: "POST",
        path: "/api/v1/inbound/rules",
        label: "Create rule",
        sampleBody: JSON.stringify(
          {
            name: "Support Inbox",
            domainId: "your-domain-id",
            matchPattern: "support@",
            action: "webhook",
            webhookUrl: "https://example.com/webhooks/email",
            priority: 0,
          },
          null,
          2,
        ),
      },
      {
        method: "PUT",
        path: "/api/v1/inbound/rules/:id",
        label: "Update rule",
        sampleBody: JSON.stringify(
          {
            name: "Support Inbox",
            domainId: "your-domain-id",
            matchPattern: "support@",
            action: "webhook",
            webhookUrl: "https://example.com/webhooks/email",
            priority: 0,
          },
          null,
          2,
        ),
      },
      {
        method: "DELETE",
        path: "/api/v1/inbound/rules/:id",
        label: "Delete rule",
      },
    ],
  },
  {
    section: "Templates",
    endpoints: [
      { method: "GET", path: "/api/v1/templates", label: "List templates" },
      {
        method: "POST",
        path: "/api/v1/templates",
        label: "Create template",
        sampleBody: JSON.stringify(
          {
            name: "Welcome Email",
            subjectTemplate: "Welcome {{name}}!",
            htmlBody:
              "<h1>Welcome {{name}}</h1><p>Thanks for signing up.</p>",
            textBody: "Welcome {{name}}! Thanks for signing up.",
          },
          null,
          2,
        ),
      },
    ],
  },
  {
    section: "Domains",
    endpoints: [
      { method: "GET", path: "/api/v1/domains", label: "List domains" },
    ],
  },
  {
    section: "API Keys",
    endpoints: [
      { method: "GET", path: "/api/v1/keys", label: "List API keys" },
    ],
  },
  {
    section: "Suppressions",
    endpoints: [
      {
        method: "GET",
        path: "/api/v1/suppressions",
        label: "List suppressions",
      },
      {
        method: "POST",
        path: "/api/v1/suppressions",
        label: "Add suppression",
        sampleBody: JSON.stringify(
          {
            email: "blocked@example.com",
            reason: "manual",
          },
          null,
          2,
        ),
      },
    ],
  },
  {
    section: "Webhooks",
    endpoints: [
      { method: "GET", path: "/api/v1/webhooks", label: "List webhooks" },
    ],
  },
  {
    section: "Analytics",
    endpoints: [
      {
        method: "GET",
        path: "/api/v1/analytics/summary",
        label: "Analytics summary",
      },
      {
        method: "GET",
        path: "/api/v1/analytics/timeline",
        label: "Analytics timeline",
      },
    ],
  },
];

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const METHOD_COLORS: Record<string, string> = {
  GET: "bg-emerald-500/15 text-emerald-400 border-emerald-500/30",
  POST: "bg-blue-500/15 text-blue-400 border-blue-500/30",
  PUT: "bg-amber-500/15 text-amber-400 border-amber-500/30",
  DELETE: "bg-red-500/15 text-red-400 border-red-500/30",
};

function statusColor(status: number): string {
  if (status >= 200 && status < 300) return "bg-emerald-500/15 text-emerald-400 border-emerald-500/30";
  if (status >= 400 && status < 500) return "bg-amber-500/15 text-amber-400 border-amber-500/30";
  return "bg-red-500/15 text-red-400 border-red-500/30";
}

interface QueryParam {
  key: string;
  value: string;
}

interface ResponseData {
  status: number;
  statusText: string;
  time: number;
  headers: Record<string, string>;
  body: string;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export default function SandboxPage() {
  // Endpoint selection
  const [selectedEndpoint, setSelectedEndpoint] = useState<Endpoint>(
    ENDPOINT_GROUPS[0].endpoints[0],
  );
  const [expandedSections, setExpandedSections] = useState<Set<string>>(
    new Set(ENDPOINT_GROUPS.map((g) => g.section)),
  );

  // Request state
  const [method, setMethod] = useState<string>("GET");
  const [path, setPath] = useState<string>("/api/v1/emails");
  const [queryParams, setQueryParams] = useState<QueryParam[]>([]);
  const [body, setBody] = useState<string>("");

  // Response state
  const [response, setResponse] = useState<ResponseData | null>(null);
  const [loading, setLoading] = useState(false);

  // Headers collapse
  const [headersOpen, setHeadersOpen] = useState(false);

  // --- Handlers ---

  const selectEndpoint = useCallback((ep: Endpoint) => {
    setSelectedEndpoint(ep);
    setMethod(ep.method);
    setPath(ep.path);
    setBody(ep.sampleBody ?? "");
    setQueryParams([]);
    setResponse(null);
  }, []);

  const toggleSection = useCallback((section: string) => {
    setExpandedSections((prev) => {
      const next = new Set(prev);
      if (next.has(section)) next.delete(section);
      else next.add(section);
      return next;
    });
  }, []);

  const addParam = useCallback(() => {
    setQueryParams((prev) => [...prev, { key: "", value: "" }]);
  }, []);

  const updateParam = useCallback(
    (index: number, field: "key" | "value", val: string) => {
      setQueryParams((prev) =>
        prev.map((p, i) => (i === index ? { ...p, [field]: val } : p)),
      );
    },
    [],
  );

  const removeParam = useCallback((index: number) => {
    setQueryParams((prev) => prev.filter((_, i) => i !== index));
  }, []);

  const sendRequest = useCallback(async () => {
    setLoading(true);
    setResponse(null);

    const startTime = performance.now();

    try {
      // Build URL through the proxy
      const proxyPath = `/api/proxy${path}`;
      const url = new URL(proxyPath, window.location.origin);
      for (const p of queryParams) {
        if (p.key.trim()) url.searchParams.set(p.key.trim(), p.value);
      }

      const fetchOptions: RequestInit = {
        method,
        credentials: "include",
        headers: { "Content-Type": "application/json" },
      };

      if (method !== "GET" && method !== "HEAD" && body.trim()) {
        fetchOptions.body = body;
      }

      const res = await fetch(url.toString(), fetchOptions);
      const elapsed = Math.round(performance.now() - startTime);

      // Collect response headers
      const resHeaders: Record<string, string> = {};
      res.headers.forEach((v, k) => {
        resHeaders[k] = v;
      });

      const text = await res.text();
      let prettyBody: string;
      try {
        prettyBody = JSON.stringify(JSON.parse(text), null, 2);
      } catch {
        prettyBody = text;
      }

      setResponse({
        status: res.status,
        statusText: res.statusText,
        time: elapsed,
        headers: resHeaders,
        body: prettyBody,
      });
    } catch (err) {
      const elapsed = Math.round(performance.now() - startTime);
      setResponse({
        status: 0,
        statusText: "Network Error",
        time: elapsed,
        headers: {},
        body:
          err instanceof Error
            ? err.message
            : "Failed to send request",
      });
    } finally {
      setLoading(false);
    }
  }, [method, path, queryParams, body]);

  // --- Render ---

  const showBody = method === "POST" || method === "PUT";

  return (
    <div className="space-y-6">
      <PageHeader
        title="API Sandbox"
        description="Test API endpoints directly from your browser."
        backHref="/docs"
        backLabel="Documentation"
      />

      <div className="grid gap-4 lg:grid-cols-5">
        {/* ---------------------------------------------------------------- */}
        {/* LEFT PANEL — Endpoint selector + request builder (40%)          */}
        {/* ---------------------------------------------------------------- */}
        <div className="lg:col-span-2 space-y-4">
          {/* Endpoint Selector */}
          <Card className="border-border bg-card">
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-semibold text-foreground">
                Endpoints
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-0">
              <ScrollArea className="h-[200px] lg:h-[320px] pr-2">
                <div className="space-y-1">
                  {ENDPOINT_GROUPS.map((group) => (
                    <div key={group.section}>
                      <button
                        type="button"
                        onClick={() => toggleSection(group.section)}
                        className="flex w-full items-center gap-1.5 py-1.5 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
                      >
                        {expandedSections.has(group.section) ? (
                          <ChevronDown className="h-3 w-3" />
                        ) : (
                          <ChevronRight className="h-3 w-3" />
                        )}
                        {group.section}
                      </button>
                      {expandedSections.has(group.section) && (
                        <div className="ml-4 space-y-0.5">
                          {group.endpoints.map((ep) => {
                            const isActive =
                              selectedEndpoint.path === ep.path &&
                              selectedEndpoint.method === ep.method;
                            return (
                              <button
                                key={`${ep.method}-${ep.path}`}
                                type="button"
                                onClick={() => selectEndpoint(ep)}
                                className={cn(
                                  "flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-xs transition-colors",
                                  isActive
                                    ? "bg-primary/10 text-foreground"
                                    : "text-muted-foreground hover:bg-muted hover:text-foreground",
                                )}
                              >
                                <Badge
                                  variant="outline"
                                  className={cn(
                                    "shrink-0 px-1.5 py-0 text-[10px] font-mono font-semibold leading-5",
                                    METHOD_COLORS[ep.method],
                                  )}
                                >
                                  {ep.method}
                                </Badge>
                                <span className="truncate">{ep.label}</span>
                              </button>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>

          {/* Request Builder */}
          <Card className="border-border bg-card">
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-semibold text-foreground">
                Request
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 pt-0">
              {/* Method + Path */}
              <div className="flex items-center gap-2">
                <Badge
                  variant="outline"
                  className={cn(
                    "shrink-0 px-2 py-0.5 text-xs font-mono font-semibold",
                    METHOD_COLORS[method],
                  )}
                >
                  {method}
                </Badge>
                <Input
                  value={path}
                  onChange={(e) => setPath(e.target.value)}
                  className="font-mono text-xs bg-muted/50 border-border"
                  placeholder="/api/v1/..."
                />
              </div>

              {/* Query Params */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-medium text-muted-foreground">
                    Query Parameters
                  </span>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={addParam}
                    className="h-6 px-2 text-xs text-muted-foreground hover:text-foreground"
                  >
                    <Plus className="mr-1 h-3 w-3" />
                    Add
                  </Button>
                </div>
                {queryParams.map((param, i) => (
                  <div key={i} className="flex items-center gap-1.5">
                    <Input
                      value={param.key}
                      onChange={(e) => updateParam(i, "key", e.target.value)}
                      placeholder="key"
                      className="h-7 text-xs font-mono bg-muted/50 border-border"
                    />
                    <Input
                      value={param.value}
                      onChange={(e) => updateParam(i, "value", e.target.value)}
                      placeholder="value"
                      className="h-7 text-xs font-mono bg-muted/50 border-border"
                    />
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => removeParam(i)}
                      className="h-7 w-7 shrink-0 p-0 text-muted-foreground hover:text-red-400"
                    >
                      <Trash2 className="h-3 w-3" />
                    </Button>
                  </div>
                ))}
              </div>

              {/* Request Body */}
              {showBody && (
                <div className="space-y-2">
                  <span className="text-xs font-medium text-muted-foreground">
                    Request Body (JSON)
                  </span>
                  <textarea
                    value={body}
                    onChange={(e) => setBody(e.target.value)}
                    rows={10}
                    spellCheck={false}
                    className="w-full rounded-md border border-border bg-sidebar px-3 py-2 font-mono text-xs text-emerald-300 placeholder:text-muted-foreground/40 focus:outline-none focus:ring-1 focus:ring-primary resize-y"
                    placeholder="{}"
                  />
                </div>
              )}

              <Separator className="bg-border" />

              {/* Send */}
              <Button
                onClick={sendRequest}
                disabled={loading}
                className="w-full"
              >
                {loading ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Play className="mr-2 h-4 w-4" />
                )}
                {loading ? "Sending..." : "Send Request"}
              </Button>
            </CardContent>
          </Card>
        </div>

        {/* ---------------------------------------------------------------- */}
        {/* RIGHT PANEL — Response viewer (60%)                             */}
        {/* ---------------------------------------------------------------- */}
        <div className="lg:col-span-3">
          <Card className="border-border bg-card h-full">
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <CardTitle className="text-sm font-semibold text-foreground">
                  Response
                </CardTitle>
                {response && (
                  <div className="flex items-center gap-3">
                    <Badge
                      variant="outline"
                      className={cn(
                        "px-2 py-0.5 text-xs font-mono font-semibold",
                        statusColor(response.status),
                      )}
                    >
                      {response.status} {response.statusText}
                    </Badge>
                    <span className="flex items-center gap-1 text-xs text-muted-foreground">
                      <Clock className="h-3 w-3" />
                      {response.time}ms
                    </span>
                  </div>
                )}
              </div>
            </CardHeader>
            <CardContent className="pt-0">
              {!response && !loading && (
                <div className="flex h-[200px] lg:h-[400px] items-center justify-center text-sm text-muted-foreground">
                  Send a request to see the response here.
                </div>
              )}

              {loading && (
                <div className="flex h-[200px] lg:h-[400px] items-center justify-center">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              )}

              {response && !loading && (
                <div className="space-y-3">
                  {/* Response Headers */}
                  {Object.keys(response.headers).length > 0 && (
                    <div>
                      <button
                        type="button"
                        onClick={() => setHeadersOpen(!headersOpen)}
                        className="flex items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
                      >
                        {headersOpen ? (
                          <ChevronDown className="h-3 w-3" />
                        ) : (
                          <ChevronRight className="h-3 w-3" />
                        )}
                        Response Headers
                      </button>
                      {headersOpen && (
                        <div className="mt-1.5 rounded-md border border-border bg-muted/30 p-2">
                          {Object.entries(response.headers).map(([k, v]) => (
                            <div
                              key={k}
                              className="flex gap-2 text-[11px] font-mono leading-5"
                            >
                              <span className="text-muted-foreground shrink-0">
                                {k}:
                              </span>
                              <span className="text-foreground break-all">
                                {v}
                              </span>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  )}

                  {/* Response Body */}
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-muted-foreground">
                        Body
                      </span>
                      <CopyButton value={response.body} label="Response" />
                    </div>
                    <ScrollArea className="h-[280px] lg:h-[450px]">
                      <pre className="rounded-md border border-border bg-sidebar p-3 font-mono text-xs leading-5 text-emerald-300 whitespace-pre-wrap break-all">
                        {response.body}
                      </pre>
                    </ScrollArea>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
