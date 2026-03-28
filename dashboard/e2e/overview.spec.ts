import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Overview Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("should display the overview heading", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Overview" })).toBeVisible();
    await expect(
      page.getByText("System health and email sending activity at a glance.")
    ).toBeVisible();
  });

  test("should render 6 stat cards with numbers", async ({ page }) => {
    // Wait for stat cards to load (they replace skeletons)
    await expect(page.getByText("Sent Today")).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Delivery Rate")).toBeVisible();
    await expect(page.getByText("Bounce Rate")).toBeVisible();
    await expect(page.getByText("Open Rate")).toBeVisible();
    await expect(page.getByText("Click Rate")).toBeVisible();
    await expect(page.getByText("Complaints")).toBeVisible();
  });

  test("should display system health section with 5 services", async ({
    page,
  }) => {
    // Health status section should show services
    await expect(page.getByText("API")).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Worker")).toBeVisible();
    await expect(page.getByText("RabbitMQ")).toBeVisible();
    await expect(page.getByText("PostgreSQL")).toBeVisible();
    await expect(page.getByText("Redis")).toBeVisible();
  });

  test("should display recent emails table", async ({ page }) => {
    await expect(page.getByText("Recent Emails")).toBeVisible({
      timeout: 10000,
    });
    // The email table should have at least one row with content
    const tableRows = page.locator("table tbody tr");
    await expect(tableRows.first()).toBeVisible({ timeout: 10000 });
  });

  test("should render the email volume chart", async ({ page }) => {
    // The chart is a Recharts SVG. Look for the card/container.
    // SendVolumeChart renders inside a Card
    await expect(page.getByText("Email Volume")).toBeVisible({ timeout: 10000 });
  });
});
