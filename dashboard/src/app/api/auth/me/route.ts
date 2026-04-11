import { NextRequest, NextResponse } from "next/server";
import { getSessionData } from "@/lib/auth/session";

export async function GET(request: NextRequest) {
  const token = request.cookies.get("eaas_session")?.value;

  if (!token) {
    return NextResponse.json({ error: "Not authenticated" }, { status: 401 });
  }

  const sessionData = getSessionData(token);

  if (!sessionData) {
    return NextResponse.json({ error: "Invalid session" }, { status: 401 });
  }

  return NextResponse.json({ success: true, data: sessionData });
}
