import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";
import { setupEmptyMockApi } from "./helpers/mock-api";

test.describe("Suppressions Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/suppressions");
  });

  test("should display the suppressions heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Suppression List" })
    ).toBeVisible();
  });

  test("should render suppression list or empty state", async ({ page }) => {
    const table = page.locator("table");
    const emptyState = page.getByText("No suppressed addresses");
    await expect(table.or(emptyState)).toBeVisible({ timeout: 10000 });
  });

  test("should show empty state when no suppressions", async ({ page }) => {
    await setupEmptyMockApi(page);
    await page.goto("/suppressions");

    await expect(
      page.getByRole("heading", { name: "Suppression List" })
    ).toBeVisible({ timeout: 10000 });

    await expect(page.getByText("No suppressed addresses")).toBeVisible({ timeout: 10000 });
  });

  test("should open add suppression dialog", async ({ page }) => {
    await page
      .getByRole("button", { name: "Add Suppression" })
      .first()
      .click();

    await expect(
      page.getByRole("heading", { name: "Add Suppression" })
    ).toBeVisible();
    await expect(page.getByPlaceholder("user@example.com")).toBeVisible();
  });

  test("should fill in add suppression form", async ({ page }) => {
    await page
      .getByRole("button", { name: "Add Suppression" })
      .first()
      .click();

    // Fill email
    await page
      .getByPlaceholder("user@example.com")
      .fill("test@blocked.com");

    // The add button in the dialog should be enabled
    const addButton = page
      .getByRole("button", { name: "Add Suppression" })
      .last();
    await expect(addButton).toBeEnabled();
  });

  test("should show remove button on suppression rows", async ({ page }) => {
    const table = page.locator("table");
    const isVisible = await table.isVisible().catch(() => false);

    if (isVisible) {
      // Each row should have a delete/remove button (trash icon)
      const deleteButtons = page.locator("table tbody tr button");
      const count = await deleteButtons.count();
      expect(count).toBeGreaterThan(0);
    }
  });

  test("should open remove confirmation dialog", async ({ page }) => {
    const table = page.locator("table");
    const isVisible = await table.isVisible().catch(() => false);

    if (isVisible) {
      // Click the first delete button
      const deleteButton = page.locator("table tbody tr button").first();
      await deleteButton.click();

      // Confirmation dialog should appear
      await expect(
        page.getByRole("heading", { name: "Remove Suppression" })
      ).toBeVisible({ timeout: 5000 });
      await expect(
        page.getByText("Are you sure you want to remove this suppression?")
      ).toBeVisible();
    }
  });
});
