import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Admin Users Page", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/admin/users");
  });

  test("should display admin users list", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Admin Users" })
    ).toBeVisible({ timeout: 10000 });

    // Users from mock data should be visible
    await expect(page.getByText("superadmin@eaas.io")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("admin@eaas.io", { exact: true })).toBeVisible();
    await expect(page.getByText("readonly@eaas.io")).toBeVisible();
  });

  test("should open create user dialog", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Admin Users" })
    ).toBeVisible({ timeout: 10000 });

    await page.getByRole("button", { name: "Create User" }).click();

    // The dialog should be visible with form fields
    await expect(page.getByText("Create User").first()).toBeVisible();
  });
});
