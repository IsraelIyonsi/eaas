import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Admin Analytics Page", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/admin/analytics");
  });

  test("should display analytics page", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Cross-Tenant Analytics" })
    ).toBeVisible({ timeout: 10000 });

    // Summary stat cards should be visible
    await expect(page.getByText("Total Tenants")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("Total Emails")).toBeVisible();
  });
});
