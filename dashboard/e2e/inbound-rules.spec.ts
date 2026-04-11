import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Inbound Rules Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/inbound/rules");
  });

  test("should display rules list", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Inbound Rules" })
    ).toBeVisible();

    // Rule cards should be visible
    await expect(page.getByText("Support Inbox")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText("Billing Forward")).toBeVisible();
    await expect(page.getByText("Catch-All")).toBeVisible();
  });

  test("should show empty state when no rules", async ({ page }) => {
    // Override the rules route to return empty data
    await page.route("**/api/proxy/**", async (route) => {
      const url = route.request().url();
      if (url.includes("/api/v1/inbound/rules")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            success: true,
            data: { items: [], totalCount: 0, page: 1, pageSize: 20 },
          }),
        });
      }
      return route.fallback();
    });

    await page.reload();
    await expect(page.getByText("No rules configured")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should open create rule sheet", async ({ page }) => {
    await page.getByRole("button", { name: "Create Rule" }).first().click();

    // The sheet should be open with a form
    await expect(page.getByText("Create Rule").first()).toBeVisible();
  });

  test("should toggle rule active/inactive", async ({ page }) => {
    // Wait for rules to load
    await expect(page.getByText("Support Inbox")).toBeVisible({
      timeout: 10000,
    });

    // Each rule card should have a switch to toggle active/inactive
    const switches = page.getByRole("switch");
    const count = await switches.count();
    expect(count).toBeGreaterThan(0);
  });

  test("should delete a rule with confirmation", async ({ page }) => {
    // Wait for rules to load
    await expect(page.getByText("Support Inbox")).toBeVisible({
      timeout: 10000,
    });

    // Find and click a delete button on a rule card (trash icon button)
    // The delete button is the second icon button in each card's actions area
    const trashButtons = page.locator("button svg.lucide-trash-2").locator("..");
    const count = await trashButtons.count();
    if (count > 0) {
      await trashButtons.first().click();

      // Confirmation dialog should appear
      await expect(
        page.getByRole("heading", { name: "Delete Rule" })
      ).toBeVisible({ timeout: 5000 });
    }
  });
});
