import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Admin Audit Logs Page", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/admin/audit-logs");
  });

  test("should display audit logs", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Audit Logs" })
    ).toBeVisible({ timeout: 10000 });

    // Audit log entries from mock data should be visible
    await expect(page.getByText("superadmin@eaas.io").first()).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("tenant.create").first()).toBeVisible();
  });

  test("should filter by action type", async ({ page }) => {
    await expect(page.getByText("superadmin@eaas.io").first()).toBeVisible({
      timeout: 10000,
    });

    // Open the action filter dropdown
    const actionFilter = page.getByRole("combobox").first();
    await actionFilter.click();
    await page.getByRole("option", { name: "User Create" }).click();

    // Only user.create logs should be visible in the table
    await expect(page.getByRole("table").getByText("user.create")).toBeVisible({
      timeout: 10000,
    });
  });
});
