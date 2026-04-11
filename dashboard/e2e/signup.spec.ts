import { test, expect } from "@playwright/test";
import { setupMockApi } from "./helpers/mock-api";

test.describe("Signup Page", () => {
  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  test("should display the signup form with all fields", async ({ page }) => {
    await page.goto("/signup");
    await expect(page.getByText("Create your EaaS account")).toBeVisible();
    await expect(page.getByLabel("Name", { exact: true })).toBeVisible();
    await expect(page.getByLabel("Email")).toBeVisible();
    await expect(page.getByLabel("Password", { exact: true })).toBeVisible();
    await expect(page.getByLabel("Confirm Password")).toBeVisible();
    await expect(page.getByLabel(/Company Name/)).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Create Account" }),
    ).toBeVisible();
  });

  test("should show validation error for mismatched passwords", async ({
    page,
  }) => {
    await page.goto("/signup");
    await page.getByLabel("Name", { exact: true }).fill("Test User");
    await page.getByLabel("Email").fill("test@example.com");
    await page.getByLabel("Password", { exact: true }).fill("password123");
    await page.getByLabel("Confirm Password").fill("different");
    await page.locator("#agreeToTerms").check();
    await page.getByRole("button", { name: "Create Account" }).click();

    await expect(page.getByText("Passwords do not match")).toBeVisible();
  });

  test("should show validation error for short password", async ({ page }) => {
    await page.goto("/signup");
    await page.getByLabel("Name", { exact: true }).fill("Test User");
    await page.getByLabel("Email").fill("test@example.com");
    await page.getByLabel("Password", { exact: true }).fill("short");
    await page.getByLabel("Confirm Password").fill("short");
    await page.locator("#agreeToTerms").check();
    await page.getByRole("button", { name: "Create Account" }).click();

    await expect(
      page.getByText("Password must be at least 8 characters"),
    ).toBeVisible();
  });

  test("should show 'Already have an account?' link to login", async ({
    page,
  }) => {
    await page.goto("/signup");
    const link = page.getByRole("link", { name: "Sign in" });
    await expect(link).toBeVisible();
    await expect(link).toHaveAttribute("href", "/login");
  });

  test("should show API key dialog on successful signup", async ({ page }) => {
    // Mock the Next.js API route (browser-level request) to return success with API key
    await page.route("**/api/auth/register", async (route) => {
      if (route.request().method() === "POST") {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          headers: {
            "Set-Cookie":
              "eaas_session=mock_session_token.0000000000000000000000000000000000000000000000000000000000000000; Path=/; HttpOnly; SameSite=Lax",
          },
          body: JSON.stringify({
            success: true,
            data: {
              apiKey: "eaas_sk_live_mock_signup_key_123456",
              tenantId: "tenant-new-123456",
            },
          }),
        });
      }
      return route.fallback();
    });

    await page.goto("/signup");
    await page.getByLabel("Name", { exact: true }).fill("Test User");
    await page.getByLabel("Email").fill("test@example.com");
    await page.getByLabel("Password", { exact: true }).fill("password123");
    await page.getByLabel("Confirm Password").fill("password123");
    await page.locator("#agreeToTerms").check();
    await page.getByRole("button", { name: "Create Account" }).click();

    await expect(page.getByText("Your API Key")).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Save this API key now")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Continue to Dashboard" }),
    ).toBeVisible();
  });
});
