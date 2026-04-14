import { NextRequest, NextResponse } from "next/server";

const API_INTERNAL_URL =
  process.env.EAAS_API_INTERNAL_URL ?? "http://localhost:5000";

async function fetchWithTimeout(url: string, init: RequestInit): Promise<Response> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 10000);
  try {
    return await fetch(url, { ...init, signal: controller.signal });
  } finally {
    clearTimeout(timeout);
  }
}

function extractErrorMessage(data: unknown, fallback: string): string {
  if (!data || typeof data !== "object") return fallback;
  const err = (data as { error?: unknown }).error;
  if (typeof err === "string") return err;
  if (err && typeof err === "object") {
    const message = (err as { message?: unknown }).message;
    if (typeof message === "string") return message;
  }
  return fallback;
}

export async function POST(request: NextRequest) {
  const body = await request.json().catch(() => ({}));
  const { token, newPassword } = body as {
    token?: string;
    newPassword?: string;
  };

  if (!token || typeof token !== "string" || !newPassword || typeof newPassword !== "string") {
    return NextResponse.json(
      { error: "Token and new password are required." },
      { status: 400 },
    );
  }

  const ip =
    request.headers.get("x-forwarded-for")?.split(",")[0]?.trim() ??
    request.headers.get("x-real-ip") ??
    undefined;

  try {
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    if (ip) {
      headers["X-Forwarded-For"] = ip;
    }

    const backendRes = await fetchWithTimeout(
      `${API_INTERNAL_URL}/api/v1/auth/reset-password`,
      {
        method: "POST",
        headers,
        body: JSON.stringify({ token, newPassword }),
      },
    );

    if (backendRes.ok) {
      return NextResponse.json({ success: true });
    }

    const data = await backendRes.json().catch(() => ({}));

    if (backendRes.status === 400) {
      return NextResponse.json(
        { error: extractErrorMessage(data, "Password does not meet requirements.") },
        { status: 400 },
      );
    }

    if (backendRes.status === 401) {
      return NextResponse.json(
        {
          error: extractErrorMessage(
            data,
            "This reset link is invalid or has expired.",
          ),
        },
        { status: 401 },
      );
    }

    return NextResponse.json(
      { error: "Unable to reset password. Please request a new link." },
      { status: 500 },
    );
  } catch {
    return NextResponse.json(
      { error: "Unable to connect. Please try again." },
      { status: 500 },
    );
  }
}
