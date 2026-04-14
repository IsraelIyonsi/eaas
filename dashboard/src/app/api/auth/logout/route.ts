import { NextResponse } from "next/server";
import { getSessionCookieFlags, SESSION_COOKIE_NAME } from "@/lib/auth/cookie-flags";

export async function POST() {
  const response = NextResponse.json({ success: true });

  response.cookies.set(SESSION_COOKIE_NAME, "", {
    ...getSessionCookieFlags(),
    maxAge: 0,
  });

  return response;
}
