import { test, expect } from "@playwright/test";
import { setupMockApi } from "./helpers/mock-api";

/**
 * End-to-end regression for the Gate 5 production outage: after a successful
 * POST /api/auth/login the browser held an HttpOnly session cookie but every
 * subsequent navigation to a protected route was bounced to /login by the
 * middleware.
 *
 * Root causes:
 *   1. Middleware HMAC verifier omitted the "eaas.cookie.v1\n" domain-separation
 *      prefix that `signSession` includes, so every valid cookie failed
 *      verification in the edge runtime.
 *   2. SameSite=Strict on the session cookie dropped the cookie on the first
 *      top-level cross-site navigation (marketing -> app subdomain, email
 *      verification link, etc.).
 *
 * These tests lock down the fixed behaviour end-to-end.
 */

test.describe("Auth flow — login, logout, protected routes", () => {
  test("unauthenticated protected-route access redirects to /login", async ({
    page,
  }) => {
    await page.goto("/overview");
    await page.waitForURL(/\/login/, { timeout: 10000 });
    await expect(page.getByRole("button", { name: "Sign In" })).toBeVisible();
  });

  test("login with dev credentials lands on /overview, not /login", async ({
    page,
  }) => {
    await setupMockApi(page);
    await page.goto("/login");
    await page.getByLabel("Email").fill("admin@eaas.local");
    await page.getByLabel("Password").fill("admin");
    await page.getByRole("button", { name: "Sign In" }).click();

    // The fix: middleware must accept the freshly issued cookie, so we land
    // on the dashboard root (which itself server-redirects to /overview).
    await page.waitForURL(/\/overview/, { timeout: 10000 });
    await expect(page).not.toHaveURL(/\/login/);
  });

  test("logout returns user to /login and protects subsequent access", async ({
    page,
  }) => {
    await setupMockApi(page);

    // Log in first.
    await page.goto("/login");
    await page.getByLabel("Email").fill("admin@eaas.local");
    await page.getByLabel("Password").fill("admin");
    await page.getByRole("button", { name: "Sign In" }).click();
    await page.waitForURL(/\/overview/, { timeout: 10000 });

    // Hit the logout endpoint directly to avoid coupling this regression test
    // to the (separately-tested) avatar dropdown markup.
    const logoutRes = await page.request.post("/api/auth/logout");
    expect(logoutRes.ok()).toBeTruthy();

    // Navigating to a protected route must now redirect to /login.
    await page.goto("/overview");
    await page.waitForURL(/\/login/, { timeout: 10000 });
  });

  test("log back in after logout succeeds (cookie re-issued + accepted)", async ({
    page,
  }) => {
    await setupMockApi(page);

    // First login.
    await page.goto("/login");
    await page.getByLabel("Email").fill("admin@eaas.local");
    await page.getByLabel("Password").fill("admin");
    await page.getByRole("button", { name: "Sign In" }).click();
    await page.waitForURL(/\/overview/, { timeout: 10000 });

    // Logout.
    const logoutRes = await page.request.post("/api/auth/logout");
    expect(logoutRes.ok()).toBeTruthy();

    // Second login — should again land on /overview. If the middleware HMAC
    // drift were still present this would loop back to /login.
    await page.goto("/login");
    await page.getByLabel("Email").fill("admin@eaas.local");
    await page.getByLabel("Password").fill("admin");
    await page.getByRole("button", { name: "Sign In" }).click();
    await page.waitForURL(/\/overview/, { timeout: 10000 });
    await expect(page).not.toHaveURL(/\/login/);
  });
});
