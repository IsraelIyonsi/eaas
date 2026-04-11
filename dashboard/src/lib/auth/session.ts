import crypto from "crypto";
import type { SessionData } from "./types";

function getSessionSecret(): string {
  const secret = process.env.SESSION_SECRET;
  if (!secret) {
    throw new Error(
      "SESSION_SECRET environment variable is required. " +
      "Generate one with: node -e \"console.log(require('crypto').randomBytes(32).toString('hex'))\""
    );
  }
  return secret;
}

export function signSession(payload: SessionData): string {
  const encoded = Buffer.from(JSON.stringify(payload))
    .toString("base64url");
  const hmac = crypto.createHmac("sha256", getSessionSecret());
  hmac.update(encoded);
  return `${encoded}.${hmac.digest("hex")}`;
}

export function verifySession(token: string): boolean {
  const parts = token.split(".");
  if (parts.length !== 2) return false;
  const [encoded, signature] = parts;
  const hmac = crypto.createHmac("sha256", getSessionSecret());
  hmac.update(encoded);
  const expected = hmac.digest("hex");
  if (signature.length !== expected.length) return false;
  return crypto.timingSafeEqual(Buffer.from(signature), Buffer.from(expected));
}

export function getSessionData(token: string): SessionData | null {
  if (!verifySession(token)) return null;
  const [encoded] = token.split(".");
  try {
    const json = Buffer.from(encoded, "base64url").toString("utf-8");
    const data = JSON.parse(json) as SessionData;
    if (!data.expiresAt || Date.now() / 1000 > data.expiresAt) return null;
    return data;
  } catch {
    return null;
  }
}
