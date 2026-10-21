import { test, expect } from '../../fixtures/base.fixture';
import { testBlogCategory, uniqueId } from '../../helpers/test-data';
import {
  waitForTableLoad,
  confirmDelete,
  TOAST_SUCCESS,
} from '../../helpers/selectors';

// ─── Blog Categories: Smoke Tests ─────────────────────────────────────────────

test.describe('Blog Categories @smoke', () => {
  /**
   * BLOG-CAT-001: Categories list loads
   * Seed one category via API, navigate to the page, verify table/list is visible.
   */
  test('BLOG-CAT-001: should display blog categories list @smoke', async ({
    api,
    trackCleanup,
    page,
  }) => {
    // Seed a category to ensure the list is non-empty
    const catData = testBlogCategory();
    const created = await api.createBlogCategory(catData);
    trackCleanup(async () => { await api.deleteBlogCategory(created.id ?? created.Id).catch(() => {}); });

    // Navigate to the page (URL search param is NOT read by React state, so skip it)
    await page.goto('/portal/blog/categories');
    await page.waitForLoadState('networkidle');

    // Page content should be visible
    await expect(page.locator('main').first()).toBeVisible({ timeout: 10_000 });

    // Page should load without error states
    await expect(page.locator('[role="alert"][data-type="error"]')).not.toBeVisible();

    // Switch to table view if available (default is tree view — only table has rows)
    const tableViewBtn = page.getByLabel(/table view/i)
      .or(page.getByRole('button', { name: /table view/i }))
      .first();
    if (await tableViewBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await tableViewBtn.click();
      await page.waitForTimeout(300);
    }

    // Check if category is already visible (no search needed for small datasets)
    const isVisibleWithoutSearch = await page.getByText(catData.name).first()
      .isVisible({ timeout: 3_000 }).catch(() => false);

    if (!isVisibleWithoutSearch) {
      // Search for the category — scope to main to avoid matching sidebar "Search menu..." input
      const searchInput = page.locator('main').getByPlaceholder(/search/i).first();
      if (await searchInput.isVisible({ timeout: 2_000 }).catch(() => false)) {
        await searchInput.fill(catData.name);
        await page.waitForLoadState('networkidle');
      }
    }

    // The seeded category should be visible somewhere on the page
    await expect(page.getByText(catData.name).first()).toBeVisible({ timeout: 10_000 });
  });

  /**
   * BLOG-CAT-002: Create category via UI
   * Click create button, fill name in dialog, save, verify success toast and list.
   */
  test('BLOG-CAT-002: should create blog category via UI @smoke', async ({
    api,
    trackCleanup,
    page,
  }) => {
    const catData = testBlogCategory();

    await page.goto('/portal/blog/categories');
    await page.waitForLoadState('networkidle');

    // Click the create/add/new button
    await page.getByRole('button', { name: /create|add|new/i }).first().click();

    // Wait for dialog to appear
    await page.locator('[role="dialog"]').waitFor({ state: 'visible', timeout: 5_000 });

    // Fill the name field
    await page.getByLabel(/name/i).first().fill(catData.name);

    // Click create/save via evaluate() — CredenzaFooter button in Radix portal
    await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('[role="dialog"] button'));
      const createBtn = btns.find(b => /^(create|save|submit)$/i.test(b.textContent?.trim() ?? ''));
      if (createBtn) (createBtn as HTMLButtonElement).click();
    });

    // Expect success toast
    await expect(page.locator(TOAST_SUCCESS).first()).toBeVisible({ timeout: 10_000 });

    // Wait for dialog to close and list to refresh
    await page.waitForTimeout(500);

    // Verify category appears in the list
    await expect(page.getByText(catData.name).first()).toBeVisible({ timeout: 5_000 });

    // Cleanup: find and delete the created category via API
    trackCleanup(async () => {
      const searchRes = await api['request'].get(
        `${process.env.API_URL ?? 'http://localhost:4000'}/api/blog/categories`,
      );
      const searchData = await searchRes.json();
      const items = searchData?.items ?? searchData?.data ?? searchData;
      if (Array.isArray(items)) {
        for (const item of items) {
          if (item.name === catData.name) {
            await api.deleteBlogCategory(item.id ?? item.Id).catch(() => {});
          }
        }
      }
    });
  });
});

// ─── Blog Categories: Regression Tests ────────────────────────────────────────

test.describe('Blog Categories @regression', () => {
  /**
   * BLOG-CAT-003: Edit category
   * Seed category via API, find row, open actions dropdown, edit name, verify.
   */
  test('BLOG-CAT-003: should edit blog category @regression', async ({
    api,
    trackCleanup,
    page,
  }) => {
    // Seed a category via API
    const catData = testBlogCategory();
    const created = await api.createBlogCategory(catData);
    trackCleanup(async () => { await api.deleteBlogCategory(created.id ?? created.Id).catch(() => {}); });

    // Navigate without URL search param (React state is not URL-synced)
    await page.goto('/portal/blog/categories');
    await page.waitForLoadState('networkidle');

    // Switch to table/list view (default is tree view — getByRole('row') only works in table)
    const tableViewBtn = page.getByLabel(/table view/i)
      .or(page.getByRole('button', { name: /table view/i }))
      .first();
    if (await tableViewBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await tableViewBtn.click();
      await page.waitForTimeout(300);
    }

    // Search for the category — scope to main to avoid matching sidebar "Search menu..." input
    const searchInput = page.locator('main').getByPlaceholder(/search/i).first();
    if (await searchInput.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await searchInput.fill(catData.name);
      await page.waitForLoadState('networkidle');
    }

    // Find the category row
    const catRow = page.getByRole('row', { name: new RegExp(catData.name, 'i') });
    await expect(catRow).toBeVisible({ timeout: 10_000 });

    // Click the actions dropdown button (first button in the row)
    const actionBtn = catRow.getByRole('button').first();
    await expect(actionBtn).toBeVisible({ timeout: 5_000 });
    await actionBtn.click();

    // Click Edit menuitem from the dropdown
    const editMenuItem = page.getByRole('menuitem', { name: /edit/i });
    await editMenuItem.waitFor({ state: 'visible', timeout: 3_000 });
    await editMenuItem.click();

    // Wait for the edit dialog to appear
    const activeDialog = page.locator('[role="dialog"]:not([aria-hidden="true"])').first();
    await expect(activeDialog).toBeVisible({ timeout: 5_000 });

    // Update the name
    const updatedName = `BlogCat-Updated-${uniqueId('edit')}`;
    const nameInput = activeDialog.getByLabel(/name/i).first();
    await nameInput.clear();
    await nameInput.fill(updatedName);

    // Save changes
    await activeDialog.getByRole('button', { name: /save|update|submit/i }).click();
    await expect(page.locator(TOAST_SUCCESS).first()).toBeVisible({ timeout: 10_000 });

    // Wait for dialog to close
    await page.waitForTimeout(500);

    // Navigate fresh and search for updated name (avoids React Query stale cache issue)
    await page.goto('/portal/blog/categories');
    await page.waitForLoadState('networkidle');
    const searchAfterEdit = page.locator('main').getByPlaceholder(/search/i).first();
    if (await searchAfterEdit.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await searchAfterEdit.fill(updatedName);
      await page.waitForLoadState('networkidle');
    }

    // Verify updated name appears in the list
    await expect(page.getByText(updatedName).first()).toBeVisible({ timeout: 5_000 });
  });

  /**
   * BLOG-CAT-004: Delete category with confirmation
   * Seed category via API, delete via UI with confirmation, verify removed.
   */
  test('BLOG-CAT-004: should delete blog category with confirmation @regression', async ({
    api,
    page,
  }) => {
    // Seed a category via API
    const catData = testBlogCategory();
    const created = await api.createBlogCategory(catData);
    // No trackCleanup needed — we're deleting via UI

    // Navigate without URL search param (React state is not URL-synced)
    await page.goto('/portal/blog/categories');
    await page.waitForLoadState('networkidle');

    // Switch to table/list view (default is tree view — getByRole('row') only works in table)
    const tableViewBtn = page.getByLabel(/table view/i)
      .or(page.getByRole('button', { name: /table view/i }))
      .first();
    if (await tableViewBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await tableViewBtn.click();
      await page.waitForTimeout(300);
    }

    // Search for the category — scope to main to avoid matching sidebar "Search menu..." input
    const searchInput = page.locator('main').getByPlaceholder(/search/i).first();
    if (await searchInput.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await searchInput.fill(catData.name);
      await page.waitForLoadState('networkidle');
    }

    // Find the category row
    const catRow = page.getByRole('row', { name: new RegExp(catData.name, 'i') });
    await expect(catRow).toBeVisible({ timeout: 10_000 });

    // Click the actions dropdown button (first button in the row)
    const actionBtn = catRow.getByRole('button').first();
    await expect(actionBtn).toBeVisible({ timeout: 5_000 });
    await actionBtn.click();

    // Click Delete menuitem from the dropdown
    const deleteMenuItem = page.getByRole('menuitem', { name: /^delete$/i });
    await deleteMenuItem.waitFor({ state: 'visible', timeout: 3_000 });
    await deleteMenuItem.click();

    // Wait for delete confirmation dialog and confirm via evaluate()
    await page.waitForTimeout(300);
    await page.evaluate(() => {
      const dialogs = Array.from(document.querySelectorAll('[role="dialog"]'));
      for (const dialog of dialogs) {
        const btns = Array.from(dialog.querySelectorAll('button'));
        const deleteBtn = btns.find(b => /^delete$/i.test(b.textContent?.trim() ?? ''));
        if (deleteBtn && !(deleteBtn as HTMLButtonElement).disabled) {
          (deleteBtn as HTMLButtonElement).click();
          return;
        }
      }
    });

    // Expect success toast
    await expect(page.locator(TOAST_SUCCESS).first()).toBeVisible({ timeout: 10_000 });

    // Verify category is removed from the list
    await page.waitForTimeout(1_000); // Allow list to refresh
    await expect(page.getByRole('row', { name: new RegExp(catData.name, 'i') })).not.toBeVisible({ timeout: 5_000 });
  });
});
