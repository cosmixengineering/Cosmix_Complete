import { type Page, type Locator, expect } from '@playwright/test';

export class RolesPage {
  readonly createButton: Locator;
  readonly roleTable: Locator;
  readonly roleRows: Locator;
  readonly searchInput: Locator;

  constructor(private page: Page) {
    this.createButton = page.getByRole('button', { name: /create|add|new/i });
    this.roleTable = page.getByRole('table');
    this.roleRows = page.getByRole('table').getByRole('row');
    this.searchInput = page.getByPlaceholder(/search/i);
  }

  async goto() {
    await this.page.goto('/portal/admin/roles');
    await this.page.waitForLoadState('networkidle');
  }

  async createRole(data: { name: string; description?: string }) {
    await this.createButton.click();
    await this.page.getByLabel(/name/i).first().fill(data.name);
    if (data.description) {
      const descLabel = this.page.getByLabel(/description/i);
      if (await descLabel.isVisible({ timeout: 2_000 }).catch(() => false)) {
        await descLabel.fill(data.description);
      }
    }
    // The dialog footer is outside the scrollable area — use JS dispatch to click
    const submitBtn = this.page.getByRole('button', { name: /save|create|submit/i });
    await submitBtn.dispatchEvent('click');
    // Wait for the roles API POST to complete
    await this.page.waitForResponse(
      resp => resp.url().includes('/api/roles') && resp.request().method() === 'POST',
      { timeout: 10_000 },
    ).catch(() => this.page.waitForTimeout(2_000));
  }

  async editRole(name: string) {
    // Wait for the row to be stable (table may be re-fetching after previous action)
    const row = this.page.getByRole('row', { name: new RegExp(name, 'i') });
    await expect(row).toBeVisible({ timeout: 10_000 });
    // The table uses a dropdown menu (ellipsis button) for row actions, not inline buttons
    const triggerBtn = row.getByRole('button').first();
    await expect(triggerBtn).toBeVisible({ timeout: 5_000 });
    await triggerBtn.click();
    await this.page.getByRole('menuitem', { name: /edit/i }).click();
    // Wait for the edit dialog to open before returning
    await this.page.locator('[role="dialog"]').waitFor({ state: 'visible', timeout: 5_000 });
  }

  async assignPermissions(roleName: string) {
    // Wait for the row to be stable before clicking
    const row = this.page.getByRole('row', { name: new RegExp(roleName, 'i') });
    await expect(row).toBeVisible({ timeout: 10_000 });
    const triggerBtn = row.getByRole('button').first();
    await expect(triggerBtn).toBeVisible({ timeout: 5_000 });
    // Open the dropdown menu and click "Manage Permissions"
    await triggerBtn.click();
    await this.page.getByRole('menuitem', { name: /manage permissions|permissions/i }).click();

    // Wait for the PermissionsDialog to open and permissions to load
    await this.page.locator('[role="dialog"]').waitFor({ state: 'visible', timeout: 5_000 });
    await this.page.waitForTimeout(500); // Wait for permissions to load

    // Use "Select All" to assign all permissions (simpler than finding specific checkboxes)
    const selectAllBtn = this.page.getByRole('button', { name: /select all/i });
    if (await selectAllBtn.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await selectAllBtn.click();
    }

    // Resize viewport to ensure dialog footer is visible
    const originalSize = await this.page.evaluate(() => ({ width: window.innerWidth, height: window.innerHeight }));
    await this.page.setViewportSize({ width: originalSize.width, height: 1200 });
    await this.page.waitForTimeout(300); // Let dialog re-layout

    const saveBtn = this.page.getByRole('button', { name: /save/i }).last();
    await saveBtn.scrollIntoViewIfNeeded();
    await saveBtn.click();

    // Restore original viewport
    await this.page.setViewportSize({ width: originalSize.width, height: originalSize.height });

    // Wait for the permissions update API call
    await this.page.waitForResponse(
      resp => resp.url().includes('/api/roles') && (resp.request().method() === 'PUT' || resp.request().method() === 'POST'),
      { timeout: 10_000 },
    ).catch(() => {});
    // Wait for dialog to close
    await this.page.locator('[role="dialog"]').waitFor({ state: 'detached', timeout: 5_000 }).catch(() => {});
    // Wait for list to refresh
    await this.page.waitForTimeout(1_000);
  }

  async expectRoleInList(name: string) {
    await expect(this.page.getByRole('row', { name: new RegExp(name, 'i') })).toBeVisible({ timeout: 10_000 });
  }
}
