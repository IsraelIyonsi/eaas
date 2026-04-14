/**
 * Centralised cookie-flag policy for the session cookie.
 *
 * Rules (enforced unconditionally — no env-var override):
 *   - httpOnly: always true (JS cannot read the cookie)
 *   - secure:   true in production (NODE_ENV === 'production'), false otherwise.
 *               The legacy SECURE_COOKIES env var is intentionally ignored;
 *               it previously allowed operators to ship cookies without the
 *               Secure flag over HTTPS, which is a cookie-theft risk.
 *   - sameSite: 'lax'   — the session cookie MUST survive top-level cross-site
 *               navigation (e.g. user clicks a link on the marketing site at
 *               sendnex.xyz that points to app.sendnex.xyz, or follows a
 *               verification link from their email inbox). Under 'strict' the
 *               browser drops the cookie on those first cross-site requests,
 *               which caused Gate 5 production login to silently redirect back
 *               to /login after a 200 from /api/auth/login. 'lax' is the
 *               industry-standard default for session cookies: state-changing
 *               cross-site requests (POST/PUT/DELETE) still do not carry the
 *               cookie, so CSRF on authenticated endpoints remains blocked.
 *   - path:     '/'      — cookie covers the whole dashboard.
 *   - domain:   unset    — default to the exact host (app.sendnex.xyz on
 *               Vercel, localhost in dev). We deliberately do NOT set
 *               .sendnex.xyz because the dashboard lives on a single
 *               subdomain and broadening the scope would leak the session to
 *               the marketing site and any future sibling subdomain.
 *
 * The shape returned matches the Next.js cookies().set options.
 */
export const SESSION_COOKIE_NAME = "sendnex_session" as const;

export interface SessionCookieFlags {
  readonly httpOnly: true;
  readonly secure: boolean;
  readonly sameSite: "lax";
  readonly path: "/";
}

export function getSessionCookieFlags(): SessionCookieFlags {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
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
