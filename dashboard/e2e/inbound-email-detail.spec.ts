import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("Inbound Email Detail Page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/inbound/emails/inbound-001");
  });

  test("should display inbound email detail", async ({ page }) => {
    // The page title is the email subject
    await expect(
      page.getByRole("heading", { name: "Re: Order #12345" })
    ).toBeVisible({ timeout: 10000 });
  });

  test("should show sender and subject", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Re: Order #12345" })
    ).toBeVisible({ timeout: 10000 });

    // From field
    await expect(page.getByText("John Doe")).toBeVisible();
    await expect(page.getByText("john@example.com")).toBeVisible();

    // Subject info row
    await expect(page.getByText("Subject").first()).toBeVisible();
  });

  test("should show security verdicts (SPF/DKIM/DMARC)", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Re: Order #12345" })
    ).toBeVisible({ timeout: 10000 });

    await expect(page.getByText("Security Verdicts")).toBeVisible();
    await expect(page.getByText("SPF")).toBeVisible();
    await expect(page.getByText("DKIM")).toBeVisible();
    await expect(page.getByText("DMARC")).toBeVisible();
  });

  test("should show action buttons", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Re: Order #12345" })
    ).toBeVisible({ timeout: 10000 });

    await expect(
      page.getByRole("button", { name: "Raw MIME" })
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Retry Webhook" })
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Delete" })
    ).toBeVisible();
  });

  test("should show body tabs", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Re: Order #12345" })
    ).toBeVisible({ timeout: 10000 });

    await expect(page.getByRole("tab", { name: "HTML Preview" })).toBeVisible();
    await expect(page.getByRole("tab", { name: "Plain Text" })).toBeVisible();
    await expect(page.getByRole("tab", { name: "Headers" })).toBeVisible();
    await expect(page.getByRole("tab", { name: "JSON" })).toBeVisible();
  });
});
