import { Page } from "@playwright/test";
import { setupMockApi } from "./mock-api";

/**
 * Logs in to the EaaS dashboard with admin/admin credentials.
 * Also sets up mock API routes so tests work without a running backend.
 */
export async function login(page: Page) {
  await setupMockApi(page);
  await page.goto("/login");
  await page.getByLabel("Username").fill("admin");
  await page.getByLabel("Password").fill("admin");
  await page.getByRole("button", { name: "Sign In" }).click();
  // Wait for redirect to overview
  await page.waitForURL("/", { timeout: 10000 });
}
