import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Logout", () => {
  test("should show Sign Out option in user dropdown", async ({ page }) => {
    await login(page);

    // Open user avatar dropdown -- the trigger contains an avatar with "U"
    const trigger = page.locator("button, [role='button']").filter({
      has: page.locator("text=U"),
    }).last();
    await trigger.click();

    await expect(page.getByText("Sign Out")).toBeVisible();
  });

  test("should redirect to login page after clicking Sign Out", async ({
    page,
  }) => {
    await login(page);

    // Open user avatar dropdown
    const trigger = page.locator("button, [role='button']").filter({
      has: page.locator("text=U"),
    }).last();
    await trigger.click();

    // Click Sign Out
    await page.getByText("Sign Out").click();

    // Should redirect to login
    await page.waitForURL(/\/login/, { timeout: 10000 });
    await expect(page.getByText("Sign in to EaaS")).toBeVisible();
  });
});
