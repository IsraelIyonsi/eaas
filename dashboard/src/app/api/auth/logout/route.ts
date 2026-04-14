import { NextResponse } from "next/server";
import { getSessionCookieFlags } from "@/lib/auth/cookie-flags";

export async function POST() {
  const response = NextResponse.json({ success: true });

  response.cookies.set("sendnex_session", "", {
    ...getSessionCookieFlags(),
    maxAge: 0,
  });

  return response;
}
