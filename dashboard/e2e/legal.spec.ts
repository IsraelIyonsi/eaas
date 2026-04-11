import { test, expect } from "@playwright/test";
import { setupMockApi } from "./helpers/mock-api";

test.describe("Legal Pages", () => {
  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  test("should display privacy policy page", async ({ page }) => {
    await page.goto("/privacy");
    await expect(
      page.getByRole("heading", { name: "Privacy Policy", level: 1 }),
    ).toBeVisible();
    await expect(page.getByText("Last updated: April 2026")).toBeVisible();
    await expect(page.getByText("Data We Collect")).toBeVisible();
    await expect(page.getByText("privacy@eaas.dev").first()).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Back to home" }),
    ).toBeVisible();
  });

  test("should display terms of service page", async ({ page }) => {
    await page.goto("/terms");
    await expect(
      page.getByRole("heading", { name: "Terms of Service", level: 1 }),
    ).toBeVisible();
    await expect(page.getByText("Last updated: April 2026")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Acceptable Use" })).toBeVisible();
    await expect(page.getByText("legal@eaas.dev")).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Back to home" }),
    ).toBeVisible();
  });

  test("should display cookie policy page", async ({ page }) => {
    await page.goto("/cookies");
    await expect(
      page.getByRole("heading", { name: "Cookie Policy", level: 1 }),
    ).toBeVisible();
    await expect(page.getByText("Last updated: April 2026")).toBeVisible();
    await expect(page.getByText("eaas_session")).toBeVisible();
    await expect(page.getByText("No Tracking Cookies")).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Back to home" }),
    ).toBeVisible();
  });

  test("should have legal links on login page", async ({ page }) => {
    await page.goto("/login");
    await expect(
      page.getByRole("link", { name: "Privacy Policy" }),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Terms of Service" }),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Cookie Policy" }),
    ).toBeVisible();
  });

  test("should display data processing agreement page", async ({ page }) => {
    await page.goto("/dpa");
    await expect(
      page.getByRole("heading", { name: "Data Processing Agreement", level: 1 }),
    ).toBeVisible();
    await expect(page.getByText("Last updated: April 2026")).toBeVisible();
    await expect(page.getByText("Definitions")).toBeVisible();
    await expect(page.getByText("Processor's Obligations")).toBeVisible();
    await expect(page.getByText("Data Breach Notification")).toBeVisible();
    await expect(page.getByText("privacy@eaas.dev").first()).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Back to home" }),
    ).toBeVisible();
  });

  test("should display sub-processor list page", async ({ page }) => {
    await page.goto("/sub-processors");
    await expect(
      page.getByRole("heading", { name: "Sub-Processor List", level: 1 }),
    ).toBeVisible();
    await expect(page.getByText("Last updated: April 2026")).toBeVisible();
    await expect(page.getByText("Amazon Web Services (AWS)")).toBeVisible();
    await expect(page.getByText("PayStack", { exact: true })).toBeVisible();
    await expect(page.getByText("Stripe", { exact: true })).toBeVisible();
    await expect(page.getByText("Flutterwave", { exact: true })).toBeVisible();
    await expect(page.getByText("PayPal", { exact: true })).toBeVisible();
    await expect(page.getByText("privacy@eaas.dev").first()).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Back to home" }),
    ).toBeVisible();
  });

  test("should display acceptable use policy page", async ({ page }) => {
    await page.goto("/acceptable-use");
    await expect(
      page.getByRole("heading", { name: "Acceptable Use Policy", level: 1 }),
    ).toBeVisible();
    await expect(page.getByText("Last updated: April 2026")).toBeVisible();
    await expect(page.getByText("Prohibited Content")).toBeVisible();
    await expect(page.getByText("Prohibited Activities")).toBeVisible();
    await expect(page.getByText("Reporting Abuse")).toBeVisible();
    await expect(page.getByText("abuse@eaas.dev")).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Back to home" }),
    ).toBeVisible();
  });

  test("should require consent checkbox on signup page", async ({ page }) => {
    await page.goto("/signup");

    // Verify the checkbox exists
    const checkbox = page.locator("#agreeToTerms");
    await expect(checkbox).toBeVisible();

    // Verify the submit button is disabled when checkbox is unchecked
    const submitButton = page.getByRole("button", { name: "Create Account" });
    await expect(submitButton).toBeDisabled();

    // Check the checkbox
    await checkbox.check();
    await expect(submitButton).toBeEnabled();

    // Uncheck and verify disabled again
    await checkbox.uncheck();
    await expect(submitButton).toBeDisabled();
  });
});
