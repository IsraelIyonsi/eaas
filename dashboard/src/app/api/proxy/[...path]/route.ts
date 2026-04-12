import { NextRequest, NextResponse } from "next/server";
import { verifySession, getSessionData } from "@/lib/auth/session";

// Internal URL for server-side calls (container-to-container in Docker)
const API_INTERNAL_URL =
  process.env.EAAS_API_INTERNAL_URL ?? "http://localhost:5000";

// API key for authenticating with the EaaS API
const API_KEY = process.env.EAAS_API_KEY ?? "";

async function proxyRequest(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
): Promise<NextResponse> {
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

  // For admin routes, forward session data as trusted headers
  if (apiPath.startsWith("/api/v1/admin/")) {
    headers["X-Admin-User-Id"] = session.userId;
    headers["X-Admin-Email"] = session.email;
    headers["X-Admin-Role"] = session.role;
    // Still send API key for proxy identification
    headers["Authorization"] = `Bearer ${API_KEY}`;
  } else {
    headers["Authorization"] = `Bearer ${API_KEY}`;
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
