import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Billing Page (Settings > Billing Tab)", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/settings");
    // Click the Billing tab
    await page.getByRole("tab", { name: "Billing" }).click();
  });

  test("should display current plan on billing page", async ({ page }) => {
    // Current subscription is "Free" plan from mock data
    await expect(page.getByText("Current Plan")).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Free").first()).toBeVisible();
  });

  test("should show plan comparison cards", async ({ page }) => {
    // All 5 plans from the mock API
    await expect(page.getByRole("heading", { name: "Free" })).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole("heading", { name: "Starter" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Pro" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Business" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Enterprise" })).toBeVisible();
  });

  test("should show current plan badge on active plan", async ({ page }) => {
    // The Free plan card should have a "Current Plan" badge
    await expect(page.getByText("Current Plan")).toBeVisible({ timeout: 10000 });
  });

  test("should show upgrade button on higher plans", async ({ page }) => {
    // Plans above free should have an Upgrade button
    await expect(page.getByText("Current Plan")).toBeVisible({ timeout: 10000 });
    const upgradeButtons = page.getByRole("button", { name: "Upgrade" });
    const count = await upgradeButtons.count();
    expect(count).toBeGreaterThanOrEqual(4); // Starter, Pro, Business, Enterprise
  });

  test("should show invoice history section", async ({ page }) => {
    await expect(page.getByText("Invoice History")).toBeVisible({ timeout: 10000 });
  });

  test("should show empty state when no invoices", async ({ page }) => {
    // Free plan mock returns empty invoices
    await expect(page.getByText("No invoices yet")).toBeVisible({ timeout: 10000 });
  });
});
