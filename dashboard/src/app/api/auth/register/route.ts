import { NextRequest, NextResponse } from "next/server";
import { signSession } from "@/lib/auth/session";
import type { SessionData } from "@/lib/auth/types";
import { getSessionCookieFlags, SESSION_COOKIE_NAME } from "@/lib/auth/cookie-flags";

const API_INTERNAL_URL =
  process.env.EAAS_API_INTERNAL_URL ?? "http://localhost:5000";

export async function POST(request: NextRequest) {
  const body = await request.json();
  const { name, email, password, companyName, legalEntityName, postalAddress } =
    body;

  if (!name || !email || !password || !legalEntityName || !postalAddress) {
    return NextResponse.json(
      {
        error:
          "Name, email, password, legal entity name, and postal address are required.",
      },
      { status: 400 },
    );
  }

  try {
    const backendRes = await fetch(
      `${API_INTERNAL_URL}/api/v1/auth/register`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name,
          email,
          password,
          companyName,
          legalEntityName,
          postalAddress,
        }),
      },
    );

    const backendData = await backendRes.json().catch(() => ({}));

    if (!backendRes.ok) {
      // Surface FluentValidation/ProblemDetails field errors when present,
      // so users see "Password must contain at least one uppercase letter."
      // instead of the opaque "One or more validation errors occurred."
      let message: string | undefined =
        backendData.error?.message ?? backendData.error;
      if (!message && backendData.errors) {
        const firstField = Object.keys(backendData.errors)[0];
        const firstMessages = backendData.errors[firstField];
        if (Array.isArray(firstMessages) && firstMessages.length > 0) {
          message = firstMessages[0];
        }
      }
      return NextResponse.json(
        { error: message ?? backendData.title ?? "Registration failed." },
        { status: backendRes.status },
      );
    }

    const data = backendData.data ?? backendData;

    const SESSION_TTL_SECONDS = 8 * 60 * 60; // 8 hours

    const sessionPayload: SessionData = {
      userId: data.tenantId ?? data.userId ?? "user-001",
      email,
      displayName: name,
      role: "tenant",
      expiresAt: Math.floor(Date.now() / 1000) + SESSION_TTL_SECONDS,
    };

    const sessionToken = signSession(sessionPayload);
    const response = NextResponse.json({
      success: true,
      data: { apiKey: data.apiKey, tenantId: data.tenantId },
    });

    response.cookies.set(SESSION_COOKIE_NAME, sessionToken, {
      ...getSessionCookieFlags(),
      maxAge: SESSION_TTL_SECONDS,
    });

    return response;
  } catch {
    return NextResponse.json(
      { error: "Unable to connect. Please try again." },
      { status: 502 },
    );
  }
}
