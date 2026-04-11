import { NextRequest, NextResponse } from "next/server";

const SESSION_SECRET = process.env.SESSION_SECRET ?? "";

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
    if (payload.expiresAt && Date.now() / 1000 > payload.expiresAt) return null;
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
  const sig = await crypto.subtle.sign("HMAC", key, encoder.encode(encoded));
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
  const token = request.cookies.get("eaas_session")?.value;
  const session = token ? await verifySessionEdge(token) : null;

  if (
    !session &&
    !request.nextUrl.pathname.startsWith("/login") &&
    !request.nextUrl.pathname.startsWith("/signup") &&
    !request.nextUrl.pathname.startsWith("/privacy") &&
    !request.nextUrl.pathname.startsWith("/terms") &&
    !request.nextUrl.pathname.startsWith("/cookies") &&
    !request.nextUrl.pathname.startsWith("/dpa") &&
    !request.nextUrl.pathname.startsWith("/sub-processors") &&
    !request.nextUrl.pathname.startsWith("/acceptable-use")
  ) {
    return NextResponse.redirect(new URL("/login", request.url));
  }

  // If authenticated and visiting /login or /signup, redirect to overview
  if (
    session &&
    (request.nextUrl.pathname.startsWith("/login") ||
      request.nextUrl.pathname.startsWith("/signup"))
  ) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  // Role-based access: admin pages require superadmin or admin role
  if (
    session &&
    request.nextUrl.pathname.startsWith("/admin") &&
    session.role !== "superadmin" &&
    session.role !== "admin"
  ) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico|api/).*)"],
};
