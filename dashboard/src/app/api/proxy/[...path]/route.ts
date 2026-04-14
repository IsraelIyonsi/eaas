import crypto from "crypto";
import { NextRequest, NextResponse } from "next/server";
import { verifySession, getSessionData } from "@/lib/auth/session";

/**
 * Builds a short-lived HMAC-signed proxy token bound to the admin user id AND
 * the exact outbound request (method + path). A captured token therefore
 * cannot be replayed against a different endpoint — it can only re-run the
 * same operation, and only within the 60s freshness window.
 *
 * Format: base64url(timestampUnix).hex(hmac-sha256(DOMAIN + method + path + userId + "." + timestamp))
 * Domain prefix ("eaas.proxy.v1\n") separates this HMAC namespace from the
 * session cookie HMAC namespace ("eaas.cookie.v1\n") even though both use
 * SESSION_SECRET.
 */
const PROXY_TOKEN_HMAC_DOMAIN = "eaas.proxy.v1\n";

function signAdminProxyToken(userId: string, method: string, path: string): string {
  const secret = process.env.SESSION_SECRET;
  if (!secret) {
    throw new Error("SESSION_SECRET is required to sign admin proxy tokens.");
  }
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const encodedTs = Buffer.from(timestamp, "utf-8").toString("base64url");
  const hmac = crypto.createHmac("sha256", secret);
  hmac.update(
    `${PROXY_TOKEN_HMAC_DOMAIN}${method.toUpperCase()}\n${path}\n${userId}.${timestamp}`,
  );
  return `${encodedTs}.${hmac.digest("hex")}`;
}

// Internal URL for server-side calls (container-to-container in Docker)
const API_INTERNAL_URL =
  process.env.EAAS_API_INTERNAL_URL ?? "http://localhost:5000";

// API key for authenticating with the EaaS API
const API_KEY = process.env.EAAS_API_KEY;

async function proxyRequest(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
): Promise<NextResponse> {
  if (!API_KEY) {
    return NextResponse.json(
      { error: "API key not configured" },
      { status: 500 },
    );
  }

  // Verify dashboard session
  const sessionCookie = request.cookies.get("sendnex_session");
  if (!sessionCookie || !verifySession(sessionCookie.value)) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const session = getSessionData(sessionCookie.value);
  if (!session) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const { path } = await params;
  const apiPath = "/" + path.join("/");

  // Block admin routes for non-admin sessions
  if (apiPath.startsWith("/api/v1/admin/") && session.role !== "superadmin" && session.role !== "admin") {
    return NextResponse.json({ error: "Forbidden" }, { status: 403 });
  }

  // Reject path traversal attempts
  if (apiPath.includes("..")) {
    return NextResponse.json({ error: "Invalid path" }, { status: 400 });
  }

  const url = new URL(apiPath, API_INTERNAL_URL);

  // Forward query parameters
  request.nextUrl.searchParams.forEach((value, key) => {
    url.searchParams.set(key, value);
  });

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
  };

  headers["Authorization"] = `Bearer ${API_KEY}`;

  // For admin routes, forward session data plus a short-lived signed proxy token.
  // The API verifies the HMAC signature against SESSION_SECRET before trusting the user id.
  if (apiPath.startsWith("/api/v1/admin/")) {
    headers["X-Admin-User-Id"] = session.userId;
    headers["X-Admin-Email"] = session.email;
    headers["X-Admin-Role"] = session.role;
    // Bind token to the exact method+path of the outbound request.
    // Query string is NOT signed — admin endpoints must not rely on query
    // parameters for authorization-affecting state.
    headers["X-Admin-Proxy-Token"] = signAdminProxyToken(
      session.userId,
      request.method,
      apiPath,
    );
  }

  // For tenant sessions, tell the API which tenant to act as.
  // The service key (EAAS_API_KEY) authorizes impersonation server-side.
  if (session.role === "tenant") {
    headers["X-Tenant-Id"] = session.userId;
  }

  const fetchOptions: RequestInit = {
    method: request.method,
    headers,
  };

  // Forward body for non-GET requests
  if (request.method !== "GET" && request.method !== "HEAD") {
    try {
      const body = await request.text();
      if (body) {
        fetchOptions.body = body;
      }
    } catch {
      // No body — that's fine
    }
  }

  try {
    const controller = new AbortController();
    const proxyTimeout = setTimeout(() => controller.abort(), 30000);
    let response: Response;
    try {
      response = await fetch(url.toString(), { ...fetchOptions, signal: controller.signal });
    } finally {
      clearTimeout(proxyTimeout);
    }
    const responseBody = await response.text();

    return new NextResponse(responseBody, {
      status: response.status,
      headers: {
        "Content-Type": response.headers.get("Content-Type") ?? "application/json",
      },
    });
  } catch (error) {
    const message =
      error instanceof Error ? error.message : "Failed to reach SendNex API";
    return NextResponse.json(
      { success: false, error: message },
      { status: 502 },
    );
  }
}

export const GET = proxyRequest;
export const POST = proxyRequest;
export const PUT = proxyRequest;
export const DELETE = proxyRequest;
export const PATCH = proxyRequest;
