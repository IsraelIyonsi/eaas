import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Admin Tenants Page", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/admin/tenants");
  });

  test("should display tenant list", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Tenants" })
    ).toBeVisible({ timeout: 10000 });

    // Tenant names from mock data should be visible
    await expect(page.getByText("Acme Corporation")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("Beta Industries")).toBeVisible();
    await expect(page.getByText("Epsilon Tech")).toBeVisible();
  });

  test("should filter tenants by status", async ({ page }) => {
    await expect(page.getByText("Acme Corporation")).toBeVisible({
      timeout: 10000,
    });

    // Open the status filter dropdown and select "Suspended"
    const statusFilter = page.getByRole("combobox").first();
    await statusFilter.click();
    await page.getByRole("option", { name: "Suspended" }).click();

    // Only suspended tenants should be visible
    await expect(page.getByText("Gamma Solutions")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should navigate to tenant detail", async ({ page }) => {
    await expect(page.getByText("Acme Corporation")).toBeVisible({
      timeout: 10000,
    });

    // Click on the first tenant row
    const row = page.locator("table tbody tr").first();
    await row.click();

    // Should navigate to tenant detail page
    await page.waitForURL(/\/admin\/tenants\/tenant-/);
  });
});
