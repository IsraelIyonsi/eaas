import { NextRequest, NextResponse } from "next/server";

export async function POST(request: NextRequest) {
  const body = await request.json();
  const { username, password } = body;

  // Simple auth: in production this checks bcrypt hash against DASHBOARD_PASSWORD_HASH
  const expectedUser = process.env.DASHBOARD_USERNAME ?? "admin";
  const expectedPass = process.env.DASHBOARD_PASSWORD ?? "admin";

  if (username !== expectedUser || password !== expectedPass) {
    return NextResponse.json(
      { error: "Invalid username or password. Please try again." },
      { status: 401 },
    );
  }

  const response = NextResponse.json({ success: true });

  response.cookies.set("eaas_session", "authenticated", {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 24, // 24 hours
  });

  return response;
}
