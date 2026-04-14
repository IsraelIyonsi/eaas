import { test, expect } from "@playwright/test";

test.describe("Forgot Password Flow", () => {
  test("login page links to forgot-password (not mailto)", async ({ page }) => {
    await page.goto("/login");
    const link = page.getByRole("link", { name: /Forgot your password/i });
    await expect(link).toBeVisible();
    const href = await link.getAttribute("href");
    expect(href).toBe("/forgot-password");
    expect(href).not.toMatch(/^mailto:/);
  });

  test("forgot-password page renders the form", async ({ page }) => {
    await page.goto("/forgot-password");
    await expect(
      page.getByRole("heading", { name: /Reset your password/i }),
    ).toBeVisible();
    await expect(page.getByLabel("Email")).toBeVisible();
    await expect(
      page.getByRole("button", { name: /Send reset link/i }),
    ).toBeVisible();
  });

  test("forgot-password submit shows no-enumeration success message", async ({
    page,
  }) => {
    // Intercept the API call and return the generic success response the
    // backend always returns, to avoid depending on a running backend.
    await page.route("**/api/auth/forgot-password", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ success: true }),
      });
    });

    await page.goto("/forgot-password");
    await page.getByLabel("Email").fill("unknown@example.com");
    await page.getByRole("button", { name: /Send reset link/i }).click();

    await expect(
      page.getByText(/If an account exists for that email/i),
    ).toBeVisible();
  });

  test("reset-password page without token shows request-new-link prompt", async ({
    page,
  }) => {
    await page.goto("/reset-password");
    await expect(
      page.getByText(/reset link is missing or malformed/i),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: /Request a new reset link/i }),
    ).toBeVisible();
  });

  test("reset-password page with token renders the form", async ({ page }) => {
    await page.goto("/reset-password?token=fake-token-for-rendering");
    await expect(
      page.getByRole("heading", { name: /Choose a new password/i }),
    ).toBeVisible();
    await expect(page.getByLabel("New password")).toBeVisible();
    await expect(page.getByLabel("Confirm password")).toBeVisible();
  });

  test("reset-password validates mismatched passwords client-side", async ({
    page,
  }) => {
    await page.goto("/reset-password?token=fake-token");
    await page.getByLabel("New password").fill("Password123");
    await page.getByLabel("Confirm password").fill("Different123");
    await page.getByRole("button", { name: /Reset password/i }).click();
    await expect(page.getByText(/Passwords do not match/i)).toBeVisible();
  });

  test("reset-password surfaces backend error on invalid token", async ({
    page,
  }) => {
    await page.route("**/api/auth/reset-password", async (route) => {
      await route.fulfill({
        status: 401,
        contentType: "application/json",
        body: JSON.stringify({
          error: "This reset link is invalid or has expired.",
        }),
      });
    });

    await page.goto("/reset-password?token=expired");
    await page.getByLabel("New password").fill("Password123");
    await page.getByLabel("Confirm password").fill("Password123");
    await page.getByRole("button", { name: /Reset password/i }).click();

    await expect(
      page.getByText(/invalid or has expired/i),
    ).toBeVisible();
  });
});
