import { NextRequest, NextResponse } from "next/server";
import { getSessionData } from "@/lib/auth/session";
import { SESSION_COOKIE_NAME } from "@/lib/auth/cookie-flags";

export async function GET(request: NextRequest) {
  const token = request.cookies.get(SESSION_COOKIE_NAME)?.value;

  if (!token) {
    return NextResponse.json({ error: "Not authenticated" }, { status: 401 });
  }

  const sessionData = getSessionData(token);

  if (!sessionData) {
    return NextResponse.json({ error: "Invalid session" }, { status: 401 });
  }

  return NextResponse.json({
    success: true,
    data: {
      userId: sessionData.userId,
      email: sessionData.email,
      displayName: sessionData.displayName,
      role: sessionData.role,
    },
  });
}
