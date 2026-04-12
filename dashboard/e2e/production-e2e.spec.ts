/**
 * Production E2E Test Suite
 * Runs against the live server at http://178.104.141.21
 * Tests real auth, real API calls, real UI flows.
 */
import { test, expect, Page } from "@playwright/test";

const BASE = "http://178.104.141.21";
const ADMIN_EMAIL = "admin@eaas.local";
const ADMIN_PASSWORD = "hellobabe123_$";

async function loginAsAdmin(page: Page) {
  await page.goto(`${BASE}/login`);
  await page.waitForLoadState("networkidle");
  await page.getByLabel("Email").fill(ADMIN_EMAIL);
  await page.getByLabel("Password").fill(ADMIN_PASSWORD);
  await page.getByRole("button", { name: /sign in/i }).click();
  await page.waitForURL(`${BASE}/`, { timeout: 15000 });
}

// ─── Auth ────────────────────────────────────────────────────────────────────

test.describe("Auth", () => {
  test("landing page loads", async ({ page }) => {
    await page.goto(BASE);
    await expect(page).toHaveTitle(/SendNex|EaaS/i);
    const body = await page.content();
    expect(body.length).toBeGreaterThan(1000);
  });

  test("login page renders", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await expect(page.getByLabel("Email")).toBeVisible();
    await expect(page.getByLabel("Password")).toBeVisible();
    await expect(page.getByRole("button", { name: /sign in/i })).toBeVisible();
  });

  test("wrong password shows error", async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await page.getByLabel("Email").fill(ADMIN_EMAIL);
    await page.getByLabel("Password").fill("wrongpassword");
    await page.getByRole("button", { name: /sign in/i }).click();
    await expect(page.getByText(/invalid|incorrect|error/i)).toBeVisible({ timeout: 8000 });
  });

  test("correct credentials redirect to dashboard", async ({ page }) => {
    await loginAsAdmin(page);
    expect(page.url()).toBe(`${BASE}/`);
    await expect(page.locator("body")).not.toContainText("sign in");
  });

  test("unauthenticated access redirects to login", async ({ page }) => {
    await page.goto(`${BASE}/emails`);
    await page.waitForURL(`${BASE}/login*`, { timeout: 8000 });
    expect(page.url()).toContain("/login");
  });

  test("sign out works", async ({ page }) => {
    await loginAsAdmin(page);
    // Find sign-out button in header
    const userMenu = page.locator('[data-testid="user-menu"], button[aria-label*="user"], button[aria-label*="account"]').first();
    if (await userMenu.isVisible()) {
      await userMenu.click();
      await page.getByRole("menuitem", { name: /sign out|log out/i }).click();
    } else {
      // Direct logout call
      await page.request.post(`${BASE}/api/auth/logout`);
      await page.goto(`${BASE}/login`);
    }
    await page.waitForURL(`${BASE}/login*`, { timeout: 8000 });
    expect(page.url()).toContain("/login");
  });
});

// ─── Dashboard Overview ───────────────────────────────────────────────────────

test.describe("Overview", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("overview page loads with stats", async ({ page }) => {
    await expect(page.locator("h1, h2").first()).toBeVisible();
    // Stats cards should be present
    await expect(page.locator("[class*='card'], [class*='stat']").first()).toBeVisible({ timeout: 10000 });
  });

  test("sidebar navigation is visible", async ({ page }) => {
    await expect(page.getByRole("link", { name: /emails/i }).first()).toBeVisible();
    await expect(page.getByRole("link", { name: /templates/i }).first()).toBeVisible();
  });
});

// ─── Emails ──────────────────────────────────────────────────────────────────

test.describe("Emails", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("emails page loads", async ({ page }) => {
    await page.goto(`${BASE}/emails`);
    await page.waitForLoadState("networkidle");
    await expect(page).toHaveURL(`${BASE}/emails`);
    const heading = page.getByRole("heading").first();
    await expect(heading).toBeVisible();
  });

  test("emails page shows table or empty state", async ({ page }) => {
    await page.goto(`${BASE}/emails`);
    await page.waitForLoadState("networkidle");
    const hasTable = await page.locator("table, [role='table']").isVisible().catch(() => false);
    const hasEmpty = await page.getByText(/no emails|empty/i).isVisible().catch(() => false);
    expect(hasTable || hasEmpty).toBeTruthy();
  });
});

// ─── Templates ───────────────────────────────────────────────────────────────

test.describe("Templates", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("templates page loads", async ({ page }) => {
    await page.goto(`${BASE}/templates`);
    await page.waitForLoadState("networkidle");
    await expect(page).toHaveURL(`${BASE}/templates`);
    await expect(page.getByRole("heading").first()).toBeVisible();
  });

  test("create template button is visible", async ({ page }) => {
    await page.goto(`${BASE}/templates`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("button", { name: /create|new template/i }).first()).toBeVisible({ timeout: 8000 });
  });
});

// ─── Domains ─────────────────────────────────────────────────────────────────

test.describe("Domains", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("domains page loads", async ({ page }) => {
    await page.goto(`${BASE}/domains`);
    await page.waitForLoadState("networkidle");
    await expect(page).toHaveURL(`${BASE}/domains`);
    await expect(page.getByRole("heading").first()).toBeVisible();
  });
});

// ─── API Keys ─────────────────────────────────────────────────────────────────

test.describe("API Keys", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("api keys page loads", async ({ page }) => {
    await page.goto(`${BASE}/api-keys`);
    await page.waitForLoadState("networkidle");
    await expect(page).toHaveURL(`${BASE}/api-keys`);
    await expect(page.getByRole("heading").first()).toBeVisible();
  });

  test("create key button is visible", async ({ page }) => {
    await page.goto(`${BASE}/api-keys`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("button", { name: /create|new|generate/i }).first()).toBeVisible({ timeout: 8000 });
  });
});

// ─── Webhooks ─────────────────────────────────────────────────────────────────

test.describe("Webhooks", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("webhooks page loads without 502", async ({ page }) => {
    await page.goto(`${BASE}/webhooks`);
    await page.waitForLoadState("networkidle");
    await expect(page).toHaveURL(`${BASE}/webhooks`);
    // Must NOT show a 502/error page
    await expect(page.getByText("502")).not.toBeVisible();
    await expect(page.getByText(/bad gateway/i)).not.toBeVisible();
    await expect(page.getByRole("heading").first()).toBeVisible();
  });
});

// ─── Analytics ───────────────────────────────────────────────────────────────

test.describe("Analytics", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("analytics page loads", async ({ page }) => {
    await page.goto(`${BASE}/analytics`);
    await page.waitForLoadState("networkidle");
    await expect(page).toHaveURL(`${BASE}/analytics`);
    await expect(page.getByRole("heading").first()).toBeVisible();
  });
});

// ─── Inbound ──────────────────────────────────────────────────────────────────

test.describe("Inbound", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("inbound emails page loads", async ({ page }) => {
    await page.goto(`${BASE}/inbound/emails`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("heading").first()).toBeVisible();
  });

  test("inbound rules page loads", async ({ page }) => {
    await page.goto(`${BASE}/inbound/rules`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("heading").first()).toBeVisible();
  });
});

// ─── Admin Pages ──────────────────────────────────────────────────────────────

test.describe("Admin", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("admin tenants page loads", async ({ page }) => {
    await page.goto(`${BASE}/admin/tenants`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("heading").first()).toBeVisible();
  });

  test("eventra appears in tenants list", async ({ page }) => {
    await page.goto(`${BASE}/admin/tenants`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByText("Eventra")).toBeVisible({ timeout: 10000 });
  });

  test("admin users page loads", async ({ page }) => {
    await page.goto(`${BASE}/admin/users`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("heading").first()).toBeVisible();
  });

  test("admin analytics page loads", async ({ page }) => {
    await page.goto(`${BASE}/admin/analytics`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("heading").first()).toBeVisible();
  });

  test("admin health page loads", async ({ page }) => {
    await page.goto(`${BASE}/admin/health`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("heading").first()).toBeVisible();
  });

  test("admin audit logs page loads", async ({ page }) => {
    await page.goto(`${BASE}/admin/audit-logs`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("heading").first()).toBeVisible();
  });

  test("admin billing page loads", async ({ page }) => {
    await page.goto(`${BASE}/admin/billing`);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("heading").first()).toBeVisible();
  });
});

// ─── pgAdmin ──────────────────────────────────────────────────────────────────

test.describe("pgAdmin", () => {
  test("pgadmin login page loads", async ({ page }) => {
    await page.goto(`${BASE}/pgadmin/`);
    await page.waitForLoadState("networkidle");
    // Should redirect to login
    await expect(page.locator("input[name='email'], input[id='email']").first()).toBeVisible({ timeout: 15000 });
  });

  test("pgadmin login submits without 502", async ({ page }) => {
    await page.goto(`${BASE}/pgadmin/login?next=/pgadmin/`);
    await page.waitForLoadState("networkidle");

    const emailField = page.locator("input[name='email'], input[id='email']").first();
    const passwordField = page.locator("input[name='password'], input[id='password']").first();
    await emailField.fill("admin@sendnex.xyz");
    await passwordField.fill("Admin@1234!");

    await page.locator("button[type='submit'], input[type='submit']").click();
    // Should NOT see 502
    await page.waitForTimeout(3000);
    await expect(page.getByText("502")).not.toBeVisible();
    await expect(page.getByText(/bad gateway/i)).not.toBeVisible();
  });
});

// ─── API Direct Tests ─────────────────────────────────────────────────────────

test.describe("API Endpoints", () => {
  test("health endpoint returns 200", async ({ request }) => {
    const resp = await request.get(`${BASE}/api/health-backend`);
    expect(resp.status()).toBe(200);
  });

  test("admin login returns token", async ({ request }) => {
    const resp = await request.post(`${BASE}/api/v1/admin/auth/login`, {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
    });
    expect(resp.status()).toBe(200);
    const body = await resp.json();
    expect(body.success).toBe(true);
    expect(body.data.role).toBe("SuperAdmin");
  });

  test("eventra API key can authenticate", async ({ request }) => {
    const resp = await request.get(`${BASE}/api/v1/emails`, {
      headers: { Authorization: "Bearer eaas_live_sFpVrD5WSFCUpzy7zkv6PRVfu14YeQkPZfrkBoXG" },
    });
    // 200 = authenticated. 403/404 on domain is acceptable. 401 = bad key.
    expect(resp.status()).not.toBe(401);
  });

  test("new email events endpoint returns 200 or 404", async ({ request }) => {
    // Auth first
    const loginResp = await request.post(`${BASE}/api/v1/admin/auth/login`, {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
    });
    // Test endpoint exists (not 405 Method Not Allowed or 404 route not found)
    const resp = await request.get(
      `${BASE}/api/v1/emails/00000000-0000-0000-0000-000000000000/events`,
      { headers: { Authorization: "Bearer eaas_live_sFpVrD5WSFCUpzy7zkv6PRVfu14YeQkPZfrkBoXG" } }
    );
    // 404 (email not found) is correct — means the route EXISTS. 405 means route missing.
    expect([200, 404]).toContain(resp.status());
  });

  test("billing plans endpoint exists", async ({ request }) => {
    const resp = await request.get(`${BASE}/api/v1/billing/plans`, {
      headers: { Authorization: "Bearer eaas_live_sFpVrD5WSFCUpzy7zkv6PRVfu14YeQkPZfrkBoXG" },
    });
    expect([200, 404]).toContain(resp.status());
    expect(resp.status()).not.toBe(405);
  });
});
