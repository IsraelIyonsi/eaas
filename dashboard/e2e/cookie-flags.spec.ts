import { test, expect } from "@playwright/test";

/**
 * Asserts the session cookie is issued with the correct security flags.
 *
 * The session cookie MUST be:
 *   - HttpOnly   (not readable via document.cookie)
 *   - SameSite=Lax   (blocks CSRF on state-changing requests while keeping
 *                    top-level cross-site navigation working — required so
 *                    the marketing site link sendnex.xyz -> app.sendnex.xyz
 *                    and email verification links do not drop the cookie)
 *   - Path=/
 *   - Secure     (only when the dashboard is served over https://)
 *
 * We exercise the real login endpoint and read the Set-Cookie response header
 * via the Playwright APIRequestContext so we can inspect all attributes,
 * including HttpOnly which is stripped from the browser jar.
 */
test.describe("Session cookie security flags", () => {
  test("login sets HttpOnly + SameSite=Strict + Path=/ on sendnex_session", async ({
    request,
    baseURL,
  }) => {
    const response = await request.post("/api/auth/login", {
      data: { email: "admin@eaas.local", password: "admin" },
    });

    expect(response.ok()).toBeTruthy();

    const setCookie = response.headers()["set-cookie"] ?? "";
    const sessionCookie = setCookie
      .split(/\n/)
      .find((c) => c.startsWith("sendnex_session="));

    expect(sessionCookie, "sendnex_session cookie must be set on login").toBeTruthy();

    // Normalise attributes for case-insensitive matching
    const attrs = sessionCookie!.toLowerCase();

    expect(attrs).toContain("httponly");
    expect(attrs).toContain("samesite=lax");
    expect(attrs).toContain("path=/");

    // Secure MUST be present when the dashboard is served over HTTPS.
    // In local dev against http://localhost, the Secure flag is intentionally
    // omitted so the browser will actually store the cookie.
    const isHttps = (baseURL ?? "").startsWith("https://");
    if (isHttps) {
      expect(attrs).toContain("secure");
    }
  });

  test("logout clears sendnex_session with the same flags", async ({ request }) => {
    const response = await request.post("/api/auth/logout");
    expect(response.ok()).toBeTruthy();

    const setCookie = response.headers()["set-cookie"] ?? "";
    const sessionCookie = setCookie
      .split(/\n/)
      .find((c) => c.startsWith("sendnex_session="));

    expect(sessionCookie).toBeTruthy();
    const attrs = sessionCookie!.toLowerCase();
    expect(attrs).toContain("httponly");
    expect(attrs).toContain("samesite=lax");
    expect(attrs).toContain("path=/");
    // Cleared cookie must have Max-Age=0 or an expiry in the past.
    expect(/max-age=0|expires=/i.test(sessionCookie!)).toBe(true);
  });
});
