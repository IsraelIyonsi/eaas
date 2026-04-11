import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Domain Detail Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/domains/dom-001");
  });

  test("should display domain detail with DNS records", async ({ page }) => {
    // Wait for domain detail to load
    await expect(page.getByText("Back to Domains")).toBeVisible({
      timeout: 10000,
    });

    // Domain name should appear in the heading
    await expect(page.getByText("mail.example.com").first()).toBeVisible();

    // DNS Records section should be visible (use first() to avoid matching danger zone text)
    await expect(page.getByText("DNS Records", { exact: true }).first()).toBeVisible();

    // DNS record table should show the records
    await expect(page.getByText("TXT").first()).toBeVisible();
    await expect(page.getByText("CNAME").first()).toBeVisible();
  });

  test("should show copy buttons for DNS values", async ({ page }) => {
    await expect(page.getByText("DNS Records", { exact: true }).first()).toBeVisible({
      timeout: 10000,
    });

    // Copy buttons should be present in the DNS records table
    const copyButtons = page.getByTitle("Copy to clipboard");
    const count = await copyButtons.count();
    expect(count).toBeGreaterThan(0);
  });
});
