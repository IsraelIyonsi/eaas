import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Admin Tenant Detail Page", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/admin/tenants/tenant-001");
  });

  test("should display tenant detail page", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Acme Corporation" })
    ).toBeVisible({ timeout: 10000 });
  });

  test("should show tenant name and status", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Acme Corporation" })
    ).toBeVisible({ timeout: 10000 });

    // Status badge should be visible
    await expect(page.getByText("Active").first()).toBeVisible();
  });

  test("should show email/domain/apikey counts", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Acme Corporation" })
    ).toBeVisible({ timeout: 10000 });

    // Stats cards from TenantStatsCards component
    await expect(page.getByText("API Keys")).toBeVisible();
    await expect(page.getByText("Domains")).toBeVisible();
    await expect(page.getByText("Emails Sent")).toBeVisible();
  });

  test("should show tenant info fields", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Acme Corporation" })
    ).toBeVisible({ timeout: 10000 });

    await expect(page.getByText("Company")).toBeVisible();
    await expect(page.getByText("Email", { exact: true })).toBeVisible();
    await expect(page.getByText("Created")).toBeVisible();
  });

  test("should show action buttons", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Acme Corporation" })
    ).toBeVisible({ timeout: 10000 });

    await expect(
      page.getByRole("button", { name: "Suspend" })
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Edit" })
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Delete" })
    ).toBeVisible();
  });
});
