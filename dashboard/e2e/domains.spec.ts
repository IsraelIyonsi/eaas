import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

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

  test("should show DNS records section for existing domains", async ({
    page,
  }) => {
    // If there are domains in the mock data, check for DNS records
    const domainItem = page.getByText("example.com").first();
    const isVisible = await domainItem.isVisible().catch(() => false);

    if (isVisible) {
      // Click to expand the accordion
      await domainItem.click();
      await expect(page.getByText("DNS Records")).toBeVisible({
        timeout: 5000,
      });
    }
  });
});
