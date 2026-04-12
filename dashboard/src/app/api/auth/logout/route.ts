import { NextResponse } from "next/server";
import { getSecureCookieFlag } from "@/lib/auth/cookie-flags";

export async function POST() {
  const response = NextResponse.json({ success: true });

  response.cookies.set("sendnex_session", "", {
    httpOnly: true,
    secure: getSecureCookieFlag(),
    sameSite: "lax",
    path: "/",
    maxAge: 0,
  });

  return response;
}
