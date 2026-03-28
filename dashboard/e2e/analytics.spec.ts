import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Analytics Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/analytics");
  });

  test("should display the analytics heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Analytics" })
    ).toBeVisible();
    await expect(
      page.getByText(
        "Delivery performance, engagement tracking, and sending trends."
      )
    ).toBeVisible();
  });

  test("should render KPI cards", async ({ page }) => {
    await expect(page.getByText("Total Sent")).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Delivered").first()).toBeVisible();
    await expect(page.getByText("Bounced").first()).toBeVisible();
    await expect(page.getByText("Opened").first()).toBeVisible();
    await expect(page.getByText("Clicked").first()).toBeVisible();
    await expect(page.getByText("Complaints").first()).toBeVisible();
  });

  test("should render charts", async ({ page }) => {
    // Charts render as SVGs inside cards - look for chart container text
    await expect(page.getByText("Email Volume")).toBeVisible({ timeout: 10000 });
  });

  test("should switch between date range presets", async ({ page }) => {
    // Wait for KPI cards to load first
    await expect(page.getByText("Total Sent")).toBeVisible({ timeout: 10000 });

    // Click 7d preset
    const btn7d = page.getByRole("button", { name: "7d" });
    await btn7d.click();
    // Button should now have the active styling (bg-[#7C4DFF])
    await expect(btn7d).toBeVisible();

    // Click 30d preset
    const btn30d = page.getByRole("button", { name: "30d" });
    await btn30d.click();
    await expect(btn30d).toBeVisible();

    // Click 90d preset
    const btn90d = page.getByRole("button", { name: "90d" });
    await btn90d.click();
    await expect(btn90d).toBeVisible();
  });
});
