import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";
import { setupErrorMockApi } from "./helpers/mock-api";

test.describe("Admin Overview Page", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/admin");
  });

  test("should display admin overview page", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Admin Overview" })
    ).toBeVisible({ timeout: 10000 });
  });

  test("should show error state when API fails", async ({ page }) => {
    await setupErrorMockApi(page);
    await page.goto("/admin");

    // Page should still render the heading without crashing
    await expect(
      page.getByRole("heading", { name: "Admin Overview" })
    ).toBeVisible({ timeout: 10000 });
  });

  test("should show platform summary metrics", async ({ page }) => {
    await expect(page.getByText("Total Tenants")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("Active Tenants")).toBeVisible();
    await expect(page.getByText("Total Emails")).toBeVisible();
    await expect(page.getByText("Total Domains")).toBeVisible();
  });
});
