import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Settings Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/settings");
  });

  test("should display settings page with tabs", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Settings" })
    ).toBeVisible();

    // Tab triggers should be visible
    await expect(page.getByRole("tab", { name: "General" })).toBeVisible();
    await expect(page.getByRole("tab", { name: "Team" })).toBeVisible();
    await expect(page.getByRole("tab", { name: "Billing" })).toBeVisible();
    await expect(page.getByRole("tab", { name: "API" })).toBeVisible();
  });

  test("should show General tab with profile form", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Settings" })
    ).toBeVisible();

    // General tab is active by default
    await expect(page.getByRole("tab", { name: "General" })).toBeVisible();

    // Danger Zone section should be visible on General tab
    await expect(page.getByText("Danger Zone")).toBeVisible({ timeout: 10000 });
    await expect(
      page.getByRole("button", { name: "Delete Account" })
    ).toBeVisible();
  });

  test("should show Billing tab with pricing cards", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Settings" })
    ).toBeVisible();

    await page.getByRole("tab", { name: "Billing" }).click();

    // Pricing cards
    await expect(page.getByText("Free")).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Pro")).toBeVisible();
    await expect(page.getByText("Enterprise")).toBeVisible();
  });

  test("should show Free plan as current plan", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Settings" })
    ).toBeVisible();

    await page.getByRole("tab", { name: "Billing" }).click();

    await expect(page.getByText("Current Plan")).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("$0")).toBeVisible();
    await expect(page.getByText("100 emails/day")).toBeVisible();
  });

  test("should switch to API tab and show configuration", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Settings" })
    ).toBeVisible();

    await page.getByRole("tab", { name: "API" }).click();
    await expect(page.getByText("API Access")).toBeVisible();
    await expect(page.getByText("Base URL")).toBeVisible();
    await expect(page.getByText("Authentication")).toBeVisible();
  });

  test("should switch to Team tab and show coming soon", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Settings" })
    ).toBeVisible();

    await page.getByRole("tab", { name: "Team" }).click();
    await expect(page.getByText("Coming Soon").first()).toBeVisible();
  });
});
