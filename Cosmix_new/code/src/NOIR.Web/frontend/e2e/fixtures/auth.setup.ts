import { test as setup, expect } from '@playwright/test';
import fs from 'fs';
import path from 'path';

const AUTH_DIR = path.join(__dirname, '..', '.auth');
const ADMIN_AUTH_FILE = path.join(AUTH_DIR, 'admin.json');
const PLATFORM_ADMIN_AUTH_FILE = path.join(AUTH_DIR, 'platform-admin.json');

// Ensure .auth directory exists
if (!fs.existsSync(AUTH_DIR)) {
  fs.mkdirSync(AUTH_DIR, { recursive: true });
}

/**
 * Auth setup — runs ONCE before all tests.
 * Saves cookies + localStorage (JWT tokens) to .auth/ files.
 * All test projects reuse via storageState → no repeated logins.
 */

setup('authenticate as tenant admin', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill('admin@noir.local');
  await page.getByLabel('Password', { exact: true }).fill('123qwe');
  await page.getByRole('button', { name: /sign in|login|đăng nhập/i }).click();

  // After login, app redirects to /portal (the portal home/dashboard)
  await page.waitForURL('**/portal**', { timeout: 15_000 });
  await expect(page).toHaveURL(/portal/);

  await page.context().storageState({ path: ADMIN_AUTH_FILE });
});

setup('authenticate as platform admin', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill('platform@noir.local');
  await page.getByLabel('Password', { exact: true }).fill('123qwe');
  await page.getByRole('button', { name: /sign in|login|đăng nhập/i }).click();

  // After login, app redirects to /portal (the portal home/dashboard)
  await page.waitForURL('**/portal**', { timeout: 15_000 });
  await expect(page).toHaveURL(/portal/);

  await page.context().storageState({ path: PLATFORM_ADMIN_AUTH_FILE });
});
