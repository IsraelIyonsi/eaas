import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Docs Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/docs");
  });

  test("should display documentation hub page", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Documentation" })
    ).toBeVisible();

    await expect(
      page.getByText("Guides, API reference, and integration tutorials for EaaS.")
    ).toBeVisible();
  });

  test("should show API reference link", async ({ page }) => {
    await expect(page.getByText("API Reference", { exact: true })).toBeVisible({ timeout: 10000 });
    await expect(
      page.getByText("Complete REST API documentation with request/response examples")
    ).toBeVisible();
  });

  test("should show Getting Started section", async ({ page }) => {
    await expect(page.getByText("Getting Started")).toBeVisible({ timeout: 10000 });
    await expect(
      page.getByText("Quick start guide to send your first email in under 5 minutes.")
    ).toBeVisible();
  });

  test("should show Webhook Integration section", async ({ page }) => {
    await expect(page.getByText("Webhook Integration")).toBeVisible({ timeout: 10000 });
  });

  test("should show Inbound Email Setup section", async ({ page }) => {
    await expect(page.getByText("Inbound Email Setup")).toBeVisible({ timeout: 10000 });
  });

  test("should show API Sandbox section", async ({ page }) => {
    await expect(page.getByText("API Sandbox")).toBeVisible({ timeout: 10000 });
  });
});
