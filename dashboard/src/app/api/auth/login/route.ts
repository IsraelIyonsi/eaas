import { NextRequest, NextResponse } from "next/server";
import { signSession } from "@/lib/auth/session";
import type { SessionData } from "@/lib/auth/types";

const API_INTERNAL_URL =
  process.env.EAAS_API_INTERNAL_URL ?? "http://localhost:5000";
const SESSION_TTL_SECONDS = 8 * 60 * 60; // 8 hours

export async function POST(request: NextRequest) {
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
    // Try backend authentication (10s timeout — EF cold start can take 3-5s)
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10000);

    const backendResponse = await fetch(
      `${API_INTERNAL_URL}/api/v1/admin/auth/login`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
        signal: controller.signal,
      },
    );

    clearTimeout(timeout);

    if (!backendResponse.ok) {
      const errorBody = await backendResponse.json().catch(() => null);
      const message =
        errorBody?.error?.message ?? "Invalid email or password.";
      return NextResponse.json({ error: message }, { status: backendResponse.status });
    }

    const result = await backendResponse.json();
    const data = result.data ?? result;

    return createSessionResponse({
      userId: data.userId,
      email: data.email,
      displayName: data.displayName,
      role: (data.role as string).toLowerCase() as SessionData["role"],
    });
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

  response.cookies.set("eaas_session", sessionToken, {
    httpOnly: true,
    secure: process.env.SECURE_COOKIES === "true",
    sameSite: "lax",
    path: "/",
    maxAge: SESSION_TTL_SECONDS,
  });

  return response;
}
