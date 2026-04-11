import { Page } from "@playwright/test";
import { setupMockApi } from "./mock-api";

/**
 * Logs in to the EaaS dashboard with admin/admin credentials.
 * Also sets up mock API routes so tests work without a running backend.
 */
export async function login(page: Page) {
  await setupMockApi(page);
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@eaas.local");
  await page.getByLabel("Password").fill("admin");
  await page.getByRole("button", { name: "Sign In" }).click();
  // Wait for redirect to overview
  await page.waitForURL("/", { timeout: 10000 });
}

/**
 * Logs in as an admin (superadmin role) and sets up mock API routes.
 * Uses the same login flow since the dashboard uses a single auth mechanism.
 */
export async function loginAsAdmin(page: Page) {
  await setupMockApi(page);
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@eaas.local");
  await page.getByLabel("Password").fill("admin");
  await page.getByRole("button", { name: "Sign In" }).click();
  await page.waitForURL("/", { timeout: 10000 });
}
