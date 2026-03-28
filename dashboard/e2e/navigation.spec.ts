import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Navigation", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("should navigate to Email Logs from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Email Logs" }).click();
    await page.waitForURL("/emails");
    await expect(
      page.getByRole("heading", { name: "Email Logs" })
    ).toBeVisible();
  });

  test("should navigate to Templates from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Templates" }).click();
    await page.waitForURL("/templates");
    await expect(
      page.getByRole("heading", { name: "Templates" })
    ).toBeVisible();
  });

  test("should navigate to Domains from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Domains" }).click();
    await page.waitForURL("/domains");
    await expect(
      page.getByRole("heading", { name: "Domains" })
    ).toBeVisible();
  });

  test("should navigate to Analytics from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Analytics" }).click();
    await page.waitForURL("/analytics");
    await expect(
      page.getByRole("heading", { name: "Analytics" })
    ).toBeVisible();
  });

  test("should navigate to Suppressions from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Suppressions" }).click();
    await page.waitForURL("/suppressions");
    await expect(
      page.getByRole("heading", { name: "Suppression List" })
    ).toBeVisible();
  });

  test("should navigate back to Overview from sidebar", async ({ page }) => {
    // Navigate away first
    await page.getByRole("link", { name: "Email Logs" }).click();
    await page.waitForURL("/emails");

    // Navigate back to Overview
    await page.getByRole("link", { name: "Overview" }).click();
    await page.waitForURL("/");
    await expect(
      page.getByRole("heading", { name: "Overview" })
    ).toBeVisible();
  });

  test("should highlight active sidebar item", async ({ page }) => {
    // Navigate to Emails
    await page.getByRole("link", { name: "Email Logs" }).click();
    await page.waitForURL("/emails");

    // The Email Logs link should have the active styling class
    const emailLink = page.getByRole("link", { name: "Email Logs" });
    await expect(emailLink).toHaveClass(/text-\[#7C4DFF\]/);
  });

  test("should toggle sidebar collapse and expand", async ({ page }) => {
    const sidebar = page.locator("aside");
    await expect(sidebar).toBeVisible();

    // The sidebar should initially have the expanded width (w-60)
    await expect(sidebar).toHaveClass(/w-60/);

    // The toggle button is the last button in the sidebar (in the border-t section)
    const toggleButton = sidebar.locator("div.border-t button");
    await toggleButton.click({ force: true });

    // Sidebar should now be collapsed (w-16)
    await expect(sidebar).toHaveClass(/w-16/);

    // Click again to expand (force: true to bypass Next.js dev overlay)
    await toggleButton.click({ force: true });
    await expect(sidebar).toHaveClass(/w-60/);
  });
});
