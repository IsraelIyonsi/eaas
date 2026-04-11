import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Admin System Health Page", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/admin/health");
  });

  test("should display system health page", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "System Health" })
    ).toBeVisible({ timeout: 10000 });

    // Service names from mock data should be visible
    await expect(page.getByText("API Server")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("PostgreSQL")).toBeVisible();
    await expect(page.getByText("Redis")).toBeVisible();
    await expect(page.getByText("RabbitMQ")).toBeVisible();
  });
});
