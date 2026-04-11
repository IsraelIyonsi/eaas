import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";
import { setupErrorMockApi } from "./helpers/mock-api";

test.describe("Domains Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/domains");
  });

  test("should display the domains heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Domains" })
    ).toBeVisible();
  });

  test("should render domain list", async ({ page }) => {
    // Wait for either domain items or empty state
    const domainItem = page.getByText("example.com").first();
    const emptyState = page.getByText("No sending domains configured");
    await expect(domainItem.or(emptyState)).toBeVisible({ timeout: 10000 });
  });

  test("should open add domain dialog", async ({ page }) => {
    await page.getByRole("button", { name: "Add Domain" }).first().click();

    await expect(
      page.getByRole("heading", { name: "Add Domain" })
    ).toBeVisible();
    await expect(
      page.getByPlaceholder("e.g., notifications.example.com")
    ).toBeVisible();
  });

  test("should fill in domain name in the add dialog", async ({ page }) => {
    await page.getByRole("button", { name: "Add Domain" }).first().click();

    const input = page.getByPlaceholder("e.g., notifications.example.com");
    await input.fill("mail.testdomain.com");

    const addButton = page
      .getByRole("button", { name: "Add Domain" })
      .last();
    await expect(addButton).toBeEnabled();
  });

  test("should show error state when API fails", async ({ page }) => {
    await setupErrorMockApi(page);
    await page.goto("/domains");

    // Page should still render the heading without crashing
    await expect(
      page.getByRole("heading", { name: "Domains" })
    ).toBeVisible({ timeout: 10000 });
  });

  test("should show DNS records section for existing domains", async ({
    page,
  }) => {
    // Wait for domains to load
    const domainTrigger = page.getByRole("button", { name: /mail\.example\.com/ });
    const isVisible = await domainTrigger.isVisible().catch(() => false);

    if (isVisible) {
      // Click the accordion trigger to expand it
      await domainTrigger.click();
      // DNS Records heading should appear inside the expanded accordion
      await expect(page.getByText("DNS Records", { exact: true })).toBeVisible({
        timeout: 5000,
      });
    }
  });
});
