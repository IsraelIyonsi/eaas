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

export async function POST(request: NextRequest) {
  const body = await request.json().catch(() => ({}));
  const { email } = body as { email?: string };

  if (!email || typeof email !== "string") {
    return NextResponse.json(
      { error: "Email is required." },
      { status: 400 },
    );
  }

  // Forward the caller's IP so the backend rate-limiter can bucket per-IP.
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
      `${API_INTERNAL_URL}/api/v1/auth/forgot-password`,
      {
        method: "POST",
        headers,
        body: JSON.stringify({ email }),
      },
    );

    // Whatever the backend returns, we render the same success response to the
    // client to avoid email enumeration.
    if (!backendRes.ok && backendRes.status !== 400) {
      return NextResponse.json({ success: true });
    }

    if (backendRes.status === 400) {
      const data = await backendRes.json().catch(() => ({}));
      return NextResponse.json(
        {
          error:
            (data as { error?: { message?: string } | string }).error &&
            typeof (data as { error?: unknown }).error === "object"
              ? ((data as { error: { message?: string } }).error.message ?? "Invalid email.")
              : ((data as { error?: string }).error ?? "Invalid email."),
        },
        { status: 400 },
      );
    }

    return NextResponse.json({ success: true });
  } catch {
    // Fail-open to preserve the no-enumeration invariant.
    return NextResponse.json({ success: true });
  }
}
