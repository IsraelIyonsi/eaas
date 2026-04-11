import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";
import { setupEmptyMockApi } from "./helpers/mock-api";

test.describe("Webhooks Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/webhooks");
  });

  test("should display webhooks list", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Webhooks" })
    ).toBeVisible();

    // Webhook cards should show the URLs
    await expect(
      page.getByText("https://myapp.com/webhooks/email")
    ).toBeVisible({ timeout: 10000 });
    await expect(
      page.getByText("https://myapp.com/webhooks/tracking")
    ).toBeVisible();
  });

  test("should open create webhook sheet", async ({ page }) => {
    await page
      .getByRole("button", { name: "Create Webhook" })
      .first()
      .click();

    // The sheet/form should be visible
    await expect(page.getByText("Create Webhook").first()).toBeVisible();
  });

  test("should show empty state when no webhooks", async ({ page }) => {
    await setupEmptyMockApi(page);
    await page.goto("/webhooks");

    await expect(
      page.getByRole("heading", { name: "Webhooks" })
    ).toBeVisible({ timeout: 10000 });

    await expect(page.getByText("No webhooks configured")).toBeVisible({ timeout: 10000 });
  });

  test("should test a webhook", async ({ page }) => {
    // Wait for webhooks to load
    await expect(
      page.getByText("https://myapp.com/webhooks/email")
    ).toBeVisible({ timeout: 10000 });

    // Click the Test button on the first webhook
    const testButton = page.getByRole("button", { name: "Test" }).first();
    await testButton.click();

    // Should show a success banner or toast
    await expect(
      page.getByText("Webhook test successful")
    ).toBeVisible({ timeout: 5000 });
  });
});
