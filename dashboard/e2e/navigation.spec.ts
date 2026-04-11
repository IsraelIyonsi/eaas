import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Navigation", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("should navigate to Sent Emails from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Sent Emails" }).click();
    await page.waitForURL("/emails");
    await expect(
      page.getByRole("heading", { name: "Email Logs" })
    ).toBeVisible();
  });

  test("should navigate to Received Emails from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Received" }).click();
    await page.waitForURL("/inbound/emails");
    await expect(
      page.getByRole("heading", { name: "Received Emails" })
    ).toBeVisible();
  });

  test("should navigate to Templates from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Templates" }).click();
    await page.waitForURL("/templates");
    await expect(
      page.getByRole("heading", { name: "Templates" })
    ).toBeVisible();
  });

  test("should navigate to Routing Rules from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Routing Rules" }).click();
    await page.waitForURL("/inbound/rules");
    await expect(
      page.getByRole("heading", { name: "Inbound Rules" })
    ).toBeVisible();
  });

  test("should navigate to Domains from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Domains" }).click();
    await page.waitForURL("/domains");
    await expect(
      page.getByRole("heading", { name: "Domains" })
    ).toBeVisible();
  });

  test("should navigate to API Keys from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "API Keys" }).click();
    await page.waitForURL("/api-keys");
    await expect(
      page.getByRole("heading", { name: "API Keys" })
    ).toBeVisible();
  });

  test("should navigate to Webhooks from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Webhooks" }).click();
    await page.waitForURL("/webhooks");
    await expect(
      page.getByRole("heading", { name: "Webhooks" })
    ).toBeVisible();
  });

  test("should navigate to Outbound Analytics from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Outbound" }).click();
    await page.waitForURL("/analytics");
    await expect(
      page.getByRole("heading", { name: "Analytics" })
    ).toBeVisible();
  });

  test("should navigate to Inbound Analytics from sidebar", async ({ page }) => {
    // The link text includes the "NEW" badge, use the analytics inbound link
    await page.locator("a[href='/analytics/inbound']").click();
    await page.waitForURL("/analytics/inbound");
    await expect(
      page.getByRole("heading", { name: "Inbound Analytics" })
    ).toBeVisible();
  });

  test("should navigate to Suppressions from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Suppressions" }).click();
    await page.waitForURL("/suppressions");
    await expect(
      page.getByRole("heading", { name: "Suppression List" })
    ).toBeVisible();
  });

  test("should navigate to Notifications from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Notifications" }).click();
    await page.waitForURL("/notifications");
    await expect(
      page.getByRole("heading", { name: "Notification Preferences" })
    ).toBeVisible();
  });

  test("should navigate to Settings from sidebar", async ({ page }) => {
    await page.getByRole("link", { name: "Settings" }).click();
    await page.waitForURL("/settings");
    await expect(
      page.getByRole("heading", { name: "Settings" })
    ).toBeVisible();
  });

  test("should navigate back to Overview from sidebar", async ({ page }) => {
    // Navigate away first
    await page.getByRole("link", { name: "Sent Emails" }).click();
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
    await page.getByRole("link", { name: "Sent Emails" }).click();
    await page.waitForURL("/emails");

    // The Sent Emails link should have the active styling class
    const emailLink = page.getByRole("link", { name: "Sent Emails" });
    await expect(emailLink).toHaveClass(/text-\[#60a5fa\]/);
  });

  test("should navigate to Admin Panel from sidebar", async ({ page }) => {
    await page.locator("a[href='/admin']").click();
    await page.waitForURL("/admin");
    await expect(
      page.getByRole("heading", { name: "Admin Overview" })
    ).toBeVisible();
  });

  test("should toggle sidebar collapse and expand", async ({ page }) => {
    const sidebar = page.locator("aside").first();
    await expect(sidebar).toBeVisible();

    // The sidebar should initially have the expanded width
    await expect(sidebar).toHaveClass(/w-\[240px\]/);

    // The toggle button is in the header bar
    const collapseBtn = page.getByLabel("Collapse sidebar");
    await expect(collapseBtn).toBeVisible();
    await collapseBtn.click();

    // Sidebar should now be collapsed
    await expect(sidebar).toHaveClass(/w-16/);

    // Click expand to restore
    const expandBtn = page.getByLabel("Expand sidebar");
    await expandBtn.click();
    await expect(sidebar).toHaveClass(/w-\[240px\]/);
  });
});
