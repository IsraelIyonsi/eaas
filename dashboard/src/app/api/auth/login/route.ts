import { NextRequest, NextResponse } from "next/server";
import { signSession } from "@/lib/auth/session";
import type { SessionData } from "@/lib/auth/types";
import { getSecureCookieFlag } from "@/lib/auth/cookie-flags";

const API_INTERNAL_URL =
  process.env.EAAS_API_INTERNAL_URL ?? "http://localhost:5000";
const SESSION_TTL_SECONDS = 8 * 60 * 60; // 8 hours

// In-memory rate limiting: 5 attempts per IP per 60 seconds
const LOGIN_WINDOW_MS = 60_000;
const LOGIN_MAX_ATTEMPTS = 5;
const loginAttempts = new Map<string, { count: number; resetAt: number }>();

function isRateLimited(ip: string): boolean {
  const now = Date.now();
  const entry = loginAttempts.get(ip);
  if (!entry || now > entry.resetAt) {
    loginAttempts.set(ip, { count: 1, resetAt: now + LOGIN_WINDOW_MS });
    return false;
  }
  entry.count++;
  return entry.count > LOGIN_MAX_ATTEMPTS;
}

// Periodic cleanup to prevent memory leak (runs at most every 5 minutes)
let lastCleanup = Date.now();
function cleanupStaleEntries() {
  const now = Date.now();
  if (now - lastCleanup < 300_000) return;
  lastCleanup = now;
  for (const [ip, entry] of loginAttempts) {
    if (now > entry.resetAt) loginAttempts.delete(ip);
  }
}

async function fetchWithTimeout(url: string, init: RequestInit): Promise<Response> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 10000);
  try {
    return await fetch(url, { ...init, signal: controller.signal });
  } finally {
    clearTimeout(timeout);
  }
}

export async function POST(request: NextRequest) {
  cleanupStaleEntries();

  const ip = request.headers.get("x-forwarded-for")?.split(",")[0]?.trim()
    ?? request.headers.get("x-real-ip")
    ?? "unknown";

  if (isRateLimited(ip)) {
    return NextResponse.json(
      { error: "Too many login attempts. Please try again in a minute." },
      { status: 429 },
    );
  }

  const body = await request.json();
  const { email, password } = body;

  if (!email || !password) {
    return NextResponse.json(
      { error: "Email and password are required." },
      { status: 400 },
    );
  }

  // Dev/test fallback: only active in development (disabled in production)
  const devUsername = process.env.DASHBOARD_USERNAME;
  const devPassword = process.env.DASHBOARD_PASSWORD;

  if (process.env.NODE_ENV !== "production" && devUsername && devPassword && email === devUsername && password === devPassword) {
    return createSessionResponse({
      userId: "00000000-0000-0000-0000-000000000001",
      email: devUsername,
      displayName: "Dev Admin",
      role: "superadmin",
    });
  }

  try {
    // Try customer (tenant) login first, then admin login.
    // Most users are tenants; admin accounts are in a separate table.
    const authPayload = JSON.stringify({ email, password });
    const headers = { "Content-Type": "application/json" };

    // 1. Try customer auth
    const customerRes = await fetchWithTimeout(
      `${API_INTERNAL_URL}/api/v1/auth/login`,
      { method: "POST", headers, body: authPayload },
    );

    if (customerRes.ok) {
      const result = await customerRes.json();
      const data = result.data ?? result;
      return createSessionResponse({
        userId: data.tenantId ?? data.userId,
        email: data.email,
        displayName: data.displayName ?? data.name,
        role: "tenant",
      });
    }

    // 2. Fall back to admin auth
    const adminRes = await fetchWithTimeout(
      `${API_INTERNAL_URL}/api/v1/admin/auth/login`,
      { method: "POST", headers, body: authPayload },
    );

    if (adminRes.ok) {
      const result = await adminRes.json();
      const data = result.data ?? result;
      return createSessionResponse({
        userId: data.userId,
        email: data.email,
        displayName: data.displayName,
        role: (data.role as string).toLowerCase() as SessionData["role"],
      });
    }

    const errorBody = await adminRes.json().catch(() => null);
    const message =
      errorBody?.error?.message ?? "Invalid email or password.";
    return NextResponse.json({ error: message }, { status: adminRes.status });
  } catch {
    return NextResponse.json(
      { error: "Invalid email or password." },
      { status: 401 },
    );
  }
}

function createSessionResponse(
  sessionData: Omit<SessionData, "expiresAt">,
) {
  const payload: SessionData = {
    ...sessionData,
    expiresAt: Math.floor(Date.now() / 1000) + SESSION_TTL_SECONDS,
  };

  const sessionToken = signSession(payload);
  const response = NextResponse.json({ success: true });

  response.cookies.set("sendnex_session", sessionToken, {
    httpOnly: true,
    secure: getSecureCookieFlag(),
    sameSite: "lax",
    path: "/",
    maxAge: SESSION_TTL_SECONDS,
  });

  return response;
}
