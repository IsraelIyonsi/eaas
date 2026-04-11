import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Inbound Analytics Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/analytics/inbound");
  });

  test("should display inbound analytics page", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Inbound Analytics" })
    ).toBeVisible();
    await expect(
      page.getByText(
        "Monitor inbound email volume, processing rates, and top senders."
      )
    ).toBeVisible();
  });

  test("should show metric cards", async ({ page }) => {
    await expect(page.getByText("Total Received")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("Processed").first()).toBeVisible();
    await expect(page.getByText("Failed").first()).toBeVisible();
    await expect(page.getByText("Avg Processing")).toBeVisible();
  });
});
