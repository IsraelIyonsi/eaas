import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";
import { setupErrorMockApi } from "./helpers/mock-api";

test.describe("API Keys Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/api-keys");
  });

  test("should display API keys list", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "API Keys" })
    ).toBeVisible();

    // Table should show mock keys
    await expect(page.getByText("Production Key")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("Staging Key")).toBeVisible();
    await expect(page.getByText("Old Key")).toBeVisible();
  });

  test("should show create key dialog", async ({ page }) => {
    await page
      .getByRole("button", { name: "Create API Key" })
      .first()
      .click();

    // Dialog should be visible
    await expect(
      page.getByRole("heading", { name: "Create API Key" })
    ).toBeVisible({ timeout: 5000 });
  });

  test("should show error state when API fails", async ({ page }) => {
    await setupErrorMockApi(page);
    await page.goto("/api-keys");

    // Page should still render the heading without crashing
    await expect(
      page.getByRole("heading", { name: "API Keys" })
    ).toBeVisible({ timeout: 10000 });
  });

  test("should show revoke confirmation dialog", async ({ page }) => {
    // Wait for keys to load
    await expect(page.getByText("Production Key")).toBeVisible({
      timeout: 10000,
    });

    // Click the Revoke button on an active key
    const revokeButton = page.getByRole("button", { name: "Revoke" }).first();
    await revokeButton.click();

    // Confirmation dialog should appear
    await expect(
      page.getByRole("heading", { name: "Revoke API Key" })
    ).toBeVisible({ timeout: 5000 });
  });
});
