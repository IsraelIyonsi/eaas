import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Email Detail Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/emails/email-001");
  });

  test("should display sent email detail", async ({ page }) => {
    // Wait for email detail to load - the page shows the subject as heading
    await expect(page.getByText("Back to Emails")).toBeVisible({
      timeout: 10000,
    });

    // Email info card should show metadata labels
    await expect(page.getByText("Status").first()).toBeVisible();
    await expect(page.getByText("From").first()).toBeVisible();
    await expect(page.getByText("To", { exact: true }).first()).toBeVisible();
    await expect(page.getByText("Message ID").first()).toBeVisible();
  });

  test("should show event timeline", async ({ page }) => {
    await expect(page.getByText("Event Timeline")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should show body tabs", async ({ page }) => {
    await expect(page.getByText("Email Body")).toBeVisible({
      timeout: 10000,
    });

    // Tab triggers should be visible
    await expect(
      page.getByRole("tab", { name: "HTML Preview" })
    ).toBeVisible();
    await expect(
      page.getByRole("tab", { name: "Plain Text" })
    ).toBeVisible();
    await expect(
      page.getByRole("tab", { name: "Raw Headers" })
    ).toBeVisible();

    // Switch to Plain Text tab
    await page.getByRole("tab", { name: "Plain Text" }).click();
    // Switch to Raw Headers tab
    await page.getByRole("tab", { name: "Raw Headers" }).click();
  });
});
