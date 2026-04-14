/**
 * Centralised cookie-flag policy for the session cookie.
 *
 * Rules (enforced unconditionally — no env-var override):
 *   - httpOnly: always true (JS cannot read the cookie)
 *   - secure:   true in production (NODE_ENV === 'production'), false otherwise.
 *               The legacy SECURE_COOKIES env var is intentionally ignored;
 *               it previously allowed operators to ship cookies without the
 *               Secure flag over HTTPS, which is a cookie-theft risk.
 *   - sameSite: 'strict' — admin session never needs to be sent cross-site.
 *               Blocks CSRF at the browser boundary.
 *   - path:     '/'      — cookie covers the whole dashboard.
 *
 * The shape returned matches the Next.js cookies().set options.
 */
export interface SessionCookieFlags {
  readonly httpOnly: true;
  readonly secure: boolean;
  readonly sameSite: "strict";
  readonly path: "/";
}

export function getSessionCookieFlags(): SessionCookieFlags {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "strict",
    path: "/",
  };
}

/**
 * @deprecated Use getSessionCookieFlags() instead. Kept only to avoid breaking
 * callers mid-migration; returns the same boolean as the `secure` flag above.
 */
export function getSecureCookieFlag(): boolean {
  return getSessionCookieFlags().secure;
}
