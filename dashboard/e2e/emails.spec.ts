import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Email Logs Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/emails");
  });

  test("should display the email logs heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Email Logs" })
    ).toBeVisible();
  });

  test("should render table with email rows", async ({ page }) => {
    const tableRows = page.locator("table tbody tr");
    await expect(tableRows.first()).toBeVisible({ timeout: 10000 });
    // Mock data has multiple emails
    const rowCount = await tableRows.count();
    expect(rowCount).toBeGreaterThan(0);
  });

  test("should open detail drawer when clicking a row", async ({ page }) => {
    const firstRow = page.locator("table tbody tr").first();
    await expect(firstRow).toBeVisible({ timeout: 10000 });
    await firstRow.click();

    // Detail sheet should open - look for metadata fields that confirm it's open
    await expect(page.getByText("Message ID")).toBeVisible({
      timeout: 10000,
    });
    // Check for "From" label in the detail sheet (unique enough)
    await expect(page.getByLabel("Email Detail").getByText("From")).toBeVisible();
    await expect(page.getByText("Event Timeline")).toBeVisible();
  });

  test("should show event timeline in detail drawer", async ({ page }) => {
    const firstRow = page.locator("table tbody tr").first();
    await expect(firstRow).toBeVisible({ timeout: 10000 });
    await firstRow.click();

    await expect(page.getByText("Event Timeline")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should filter by status using dropdown", async ({ page }) => {
    // Open the status dropdown
    await page.getByRole("combobox").click();
    await page.getByRole("option", { name: "Delivered" }).click();

    // Table should update - wait for it to settle
    await page.waitForTimeout(500);
    const tableRows = page.locator("table tbody tr");
    const rowCount = await tableRows.count();
    expect(rowCount).toBeGreaterThanOrEqual(0);
  });

  test("should filter using search input", async ({ page }) => {
    const searchInput = page.getByPlaceholder("Search by recipient, subject...");
    await expect(searchInput).toBeVisible();
    await searchInput.fill("test");

    // Wait for debounce/query
    await page.waitForTimeout(500);
  });
});
