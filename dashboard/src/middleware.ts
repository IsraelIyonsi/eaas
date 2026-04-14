import { NextRequest, NextResponse } from "next/server";
import { SESSION_COOKIE_NAME } from "@/lib/auth/cookie-flags";

const SESSION_SECRET = process.env.SESSION_SECRET ?? "";

// Domain-separation prefix — MUST match `COOKIE_HMAC_DOMAIN` in
// `src/lib/auth/session.ts`. If these two constants drift apart the
// middleware will reject every valid cookie and bounce users back to
// /login (that was the Gate 5 production bug). Do not edit one without
// editing the other.
const COOKIE_HMAC_DOMAIN = "eaas.cookie.v1\n";

interface SessionPayload {
  userId: string;
  email: string;
  displayName: string;
  role: string;
  expiresAt?: number;
}

async function verifySessionEdge(
  token: string,
): Promise<SessionPayload | null> {
  const parts = token.split(".");
  if (parts.length !== 2) return null;
  const [encoded, signature] = parts;

  // Decode payload first to validate structure
  let payload: SessionPayload;
  try {
    const json = atob(
      encoded.replace(/-/g, "+").replace(/_/g, "/") +
        "=".repeat((4 - (encoded.length % 4)) % 4),
    );
    payload = JSON.parse(json);
    if (!payload.userId || !payload.email || !payload.role) return null;
    if (!payload.expiresAt || Date.now() / 1000 > payload.expiresAt) return null;
  } catch {
    return null;
  }

  if (!SESSION_SECRET) {
    // Reject all tokens if SESSION_SECRET is not configured.
    // Without a secret, HMAC verification is impossible and tokens cannot be trusted.
    return null;
  }

  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw",
    encoder.encode(SESSION_SECRET),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const sig = await crypto.subtle.sign(
    "HMAC",
    key,
    encoder.encode(COOKIE_HMAC_DOMAIN + encoded),
  );
  const expected = Array.from(new Uint8Array(sig))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");

  if (signature.length !== expected.length) return null;

  // Constant-time comparison
  let mismatch = 0;
  for (let i = 0; i < signature.length; i++) {
    mismatch |= signature.charCodeAt(i) ^ expected.charCodeAt(i);
  }

  return mismatch === 0 ? payload : null;
}

export async function middleware(request: NextRequest) {
  const isRSC = request.headers.get("RSC") === "1";

  const token = request.cookies.get(SESSION_COOKIE_NAME)?.value;
  const session = token ? await verifySessionEdge(token) : null;

  const isPublicPath =
    request.nextUrl.pathname.startsWith("/login") ||
    request.nextUrl.pathname.startsWith("/signup") ||
    request.nextUrl.pathname.startsWith("/forgot-password") ||
    request.nextUrl.pathname.startsWith("/reset-password") ||
    request.nextUrl.pathname.startsWith("/privacy") ||
    request.nextUrl.pathname.startsWith("/terms") ||
    request.nextUrl.pathname.startsWith("/cookies") ||
    request.nextUrl.pathname.startsWith("/dpa") ||
    request.nextUrl.pathname.startsWith("/sub-processors") ||
    request.nextUrl.pathname.startsWith("/acceptable-use");

  if (!session && !isPublicPath) {
    // RSC prefetch: return 401 instead of redirect to avoid ERR_TOO_MANY_REDIRECTS
    if (isRSC) {
      return new NextResponse(null, { status: 401 });
    }
    return NextResponse.redirect(new URL("/login", request.url));
  }

  // If authenticated and visiting /login or /signup, redirect to dashboard
  if (
    session &&
    (request.nextUrl.pathname.startsWith("/login") ||
      request.nextUrl.pathname.startsWith("/signup"))
  ) {
    return NextResponse.redirect(new URL("/overview", request.url));
  }

  // Role-based access: /admin/* pages are restricted to admin users only (superadmin or admin from AdminUsers table).
  // Tenant sessions use role="tenant" and must never access admin routes.
  if (
    session &&
    request.nextUrl.pathname.startsWith("/admin") &&
    session.role !== "superadmin" &&
    session.role !== "admin"
  ) {
    return NextResponse.redirect(new URL("/overview", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico|api/).*)"],
};
