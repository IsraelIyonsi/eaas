import { test, expect } from "@playwright/test";

test.describe("Login Page", () => {
  test("should display the login form", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByText("Sign in to EaaS")).toBeVisible();
    await expect(page.getByLabel("Email")).toBeVisible();
    await expect(page.getByLabel("Password")).toBeVisible();
    await expect(page.getByRole("button", { name: "Sign In" })).toBeVisible();
  });

  test("should login with admin/admin and redirect to overview", async ({
    page,
  }) => {
    await page.goto("/login");
    await page.getByLabel("Email").fill("admin@eaas.local");
    await page.getByLabel("Password").fill("admin");
    await page.getByRole("button", { name: "Sign In" }).click();

    // Should redirect to overview
    await page.waitForURL("/", { timeout: 10000 });

    // Verify overview page content
    await expect(page.getByRole("heading", { name: "Overview" })).toBeVisible();
  });

  test("should show error for invalid credentials", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel("Email").fill("wrong@example.com");
    await page.getByLabel("Password").fill("wrong");
    await page.getByRole("button", { name: "Sign In" }).click();

    await expect(page.getByText(/Invalid email or password|Authentication service unavailable/)).toBeVisible();
  });

  test("should redirect unauthenticated users to login", async ({ page }) => {
    await page.goto("/");
    await page.waitForURL(/\/login/, { timeout: 10000 });
    await expect(page.getByText("Sign in to EaaS")).toBeVisible();
  });
});
