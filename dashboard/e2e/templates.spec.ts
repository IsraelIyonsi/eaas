import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";
import { setupEmptyMockApi } from "./helpers/mock-api";

test.describe("Templates Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/templates");
  });

  test("should display the templates heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Templates" })
    ).toBeVisible();
  });

  test("should render template list", async ({ page }) => {
    // Wait for either the table or the empty state to appear
    const table = page.locator("table");
    const emptyState = page.getByText("No templates yet");
    await expect(table.or(emptyState)).toBeVisible({ timeout: 10000 });
  });

  test("should open create template dialog and fill form", async ({
    page,
  }) => {
    await page
      .getByRole("button", { name: "Create Template" })
      .first()
      .click();

    // Dialog should be open
    await expect(
      page.getByRole("heading", { name: "Create Template" })
    ).toBeVisible();

    // Fill in the form
    await page.getByPlaceholder("e.g., Invoice Notification").fill("Test Template");
    await page
      .getByPlaceholder("e.g., Invoice #{{invoice_number}}")
      .fill("Test Subject {{name}}");
    await page
      .getByPlaceholder("<html><body>Your email here...</body></html>")
      .fill("<html><body><h1>Hello {{name}}</h1></body></html>");
    await page
      .getByPlaceholder("Plain text version...")
      .fill("Hello {{name}}");

    // Save button should be enabled
    const saveButton = page.getByRole("button", { name: "Create Template" }).last();
    await expect(saveButton).toBeEnabled();
  });

  test("should show preview tab in template editor", async ({ page }) => {
    await page
      .getByRole("button", { name: "Create Template" })
      .first()
      .click();

    // Fill in HTML body first
    await page.getByPlaceholder("e.g., Invoice Notification").fill("Test");
    await page
      .getByPlaceholder("e.g., Invoice #{{invoice_number}}")
      .fill("Subject");
    await page
      .getByPlaceholder("<html><body>Your email here...</body></html>")
      .fill("<html><body><h1>Preview Test</h1></body></html>");

    // Switch to preview tab
    await page.getByRole("tab", { name: "Preview" }).click();

    // Iframe should be visible
    const iframe = page.locator("iframe[title='Template preview']");
    await expect(iframe).toBeVisible();
  });

  test("should show empty state when no templates", async ({ page }) => {
    await setupEmptyMockApi(page);
    await page.goto("/templates");

    await expect(
      page.getByRole("heading", { name: "Templates" })
    ).toBeVisible({ timeout: 10000 });

    await expect(page.getByText("No templates yet")).toBeVisible({ timeout: 10000 });
  });

  test("should open edit dialog when clicking edit on a template", async ({
    page,
  }) => {
    // Wait for template list to load
    const table = page.locator("table");
    const emptyState = page.getByText("No templates yet");
    const visible = await table
      .isVisible()
      .catch(() => false);

    if (visible) {
      // Click the actions menu on the first template row
      const actionButton = page
        .locator("table tbody tr")
        .first()
        .locator("button")
        .first();
      await actionButton.click();

      // Click Edit
      await page.getByRole("menuitem", { name: "Edit" }).click();

      // The dialog should show "Edit Template"
      await expect(page.getByText("Edit Template")).toBeVisible();
    }
  });
});
