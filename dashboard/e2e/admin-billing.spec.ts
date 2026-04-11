import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Admin Billing Plans Page", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/admin/billing");
  });

  test("should display billing plans heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Billing Plans" })
    ).toBeVisible({ timeout: 10000 });
  });

  test("should show list of plans", async ({ page }) => {
    await expect(page.getByText("Free", { exact: true })).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Pro", { exact: true })).toBeVisible();
    await expect(page.getByText("Enterprise", { exact: true })).toBeVisible();
  });

  test("should show plan prices", async ({ page }) => {
    await expect(page.getByText("$0.00")).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("$29.99")).toBeVisible();
  });

  test("should open create plan dialog", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Billing Plans" })
    ).toBeVisible({ timeout: 10000 });
    await page.getByRole("button", { name: "Create Plan" }).click();
    await expect(page.getByText("Create Plan").nth(1)).toBeVisible();
  });
});
