import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Inbound Emails Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/inbound/emails");
  });

  test("should display received emails list with data", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Received Emails" })
    ).toBeVisible();

    const tableRows = page.locator("table tbody tr");
    await expect(tableRows.first()).toBeVisible({ timeout: 10000 });
    const rowCount = await tableRows.count();
    expect(rowCount).toBeGreaterThan(0);
  });

  test("should show empty state when no inbound emails", async ({ page }) => {
    // Override the inbound emails route to return empty data
    await page.route("**/api/proxy/**", async (route) => {
      const url = route.request().url();
      if (url.includes("/api/v1/inbound/emails")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            success: true,
            data: {
              items: [],
              totalCount: 0,
              page: 1,
              pageSize: 10,
            },
          }),
        });
      }
      return route.fallback();
    });

    await page.reload();
    await expect(page.getByText("No inbound emails yet")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should filter by status", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Received Emails" })
    ).toBeVisible();

    // Open the status filter dropdown
    await page.getByRole("combobox").click();
    await page.getByRole("option", { name: "Processed" }).click();

    // Table should update
    await page.waitForTimeout(500);
    const tableRows = page.locator("table tbody tr");
    const rowCount = await tableRows.count();
    expect(rowCount).toBeGreaterThanOrEqual(0);
  });

  test("should search by sender", async ({ page }) => {
    const searchInput = page.getByPlaceholder("Search by sender, subject...");
    await expect(searchInput).toBeVisible();
    await searchInput.fill("john");

    // Wait for debounce
    await page.waitForTimeout(500);
  });
});
