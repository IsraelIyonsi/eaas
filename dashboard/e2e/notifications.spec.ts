import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Notifications Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/notifications");
  });

  test("should display notification preferences page", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Notification Preferences" })
    ).toBeVisible();

    // Alert cards should be visible
    await expect(page.getByText("Volume Spike Alert")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("Processing Failure Alert")).toBeVisible();
    await expect(page.getByText("Spam Threshold Alert")).toBeVisible();
  });

  test("should toggle alert on/off", async ({ page }) => {
    await expect(page.getByText("Volume Spike Alert")).toBeVisible({
      timeout: 10000,
    });

    // Each alert card has a switch to toggle (base-ui renders as role="switch")
    const switches = page.getByRole("switch");
    const count = await switches.count();
    expect(count).toBeGreaterThan(0);

    // Toggle the first switch
    await switches.first().click();
  });
});
