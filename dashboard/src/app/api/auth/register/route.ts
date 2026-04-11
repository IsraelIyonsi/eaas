import { NextRequest, NextResponse } from "next/server";
import { signSession } from "@/lib/auth/session";
import type { SessionData } from "@/lib/auth/types";

const API_INTERNAL_URL =
  process.env.EAAS_API_INTERNAL_URL ?? "http://localhost:5000";

export async function POST(request: NextRequest) {
  const body = await request.json();
  const { name, email, password, companyName } = body;

  if (!name || !email || !password) {
    return NextResponse.json(
      { error: "Name, email, and password are required." },
      { status: 400 },
    );
  }

  try {
    const backendRes = await fetch(
      `${API_INTERNAL_URL}/api/v1/auth/register`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, email, password, companyName }),
      },
    );

    const backendData = await backendRes.json().catch(() => ({}));

    if (!backendRes.ok) {
      return NextResponse.json(
        { error: backendData.error?.message ?? backendData.error ?? "Registration failed." },
        { status: backendRes.status },
      );
    }

    const data = backendData.data ?? backendData;

    const SESSION_TTL_SECONDS = 8 * 60 * 60; // 8 hours

    const sessionPayload: SessionData = {
      userId: data.tenantId ?? data.userId ?? "user-001",
      email,
      displayName: name,
      role: "admin",
      expiresAt: Math.floor(Date.now() / 1000) + SESSION_TTL_SECONDS,
    };

    const sessionToken = signSession(sessionPayload);
    const response = NextResponse.json({
      success: true,
      data: { apiKey: data.apiKey, tenantId: data.tenantId },
    });

    response.cookies.set("eaas_session", sessionToken, {
      httpOnly: true,
      secure: process.env.NODE_ENV === "production",
      sameSite: "lax",
      path: "/",
      maxAge: SESSION_TTL_SECONDS,
    });

    return response;
  } catch {
    return NextResponse.json(
      { error: "Unable to connect to the EaaS API. Please try again." },
      { status: 502 },
    );
  }
}
