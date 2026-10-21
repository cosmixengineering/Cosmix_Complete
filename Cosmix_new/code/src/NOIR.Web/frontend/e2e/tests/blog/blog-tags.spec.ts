import { test, expect } from '../../fixtures/base.fixture';
import { testBlogTag, uniqueId } from '../../helpers/test-data';
import {
  waitForTableLoad,
  confirmDelete,
  TOAST_SUCCESS,
} from '../../helpers/selectors';

// ─── Blog Tags: Smoke Tests ───────────────────────────────────────────────────

test.describe('Blog Tags @smoke', () => {
  /**
   * BLOG-TAG-001: Tags list loads
   * Seed one tag via API, navigate to the page, verify table/list is visible.
   */
  test('BLOG-TAG-001: should display blog tags list @smoke', async ({
    api,
    trackCleanup,
    page,
  }) => {
    // Seed a tag to ensure the list is non-empty
    const tagData = testBlogTag();
    const created = await api.createBlogTag(tagData);
    trackCleanup(async () => { await api.deleteBlogTag(created.id ?? created.Id).catch(() => {}); });

    // Navigate without URL search param (React state is not URL-synced)
    await page.goto('/portal/blog/tags');
    await page.waitForLoadState('networkidle');

    // Page content should be visible
    await expect(page.locator('main').first()).toBeVisible({ timeout: 10_000 });

    // Page should load without error states
    await expect(page.locator('[role="alert"][data-type="error"]')).not.toBeVisible();

    // Check if tag is immediately visible (no search needed for small datasets)
    const isVisibleWithoutSearch = await page.getByText(tagData.name).first()
      .isVisible({ timeout: 3_000 }).catch(() => false);

    if (!isVisibleWithoutSearch) {
      // Search for the tag — scope to main to avoid matching sidebar "Search menu..." input
      const searchInput = page.locator('main').getByPlaceholder(/search/i).first();
      if (await searchInput.isVisible({ timeout: 2_000 }).catch(() => false)) {
        await searchInput.fill(tagData.name);
        await page.waitForLoadState('networkidle');
      }
    }

    // The seeded tag should be visible somewhere on the page
    await expect(page.getByText(tagData.name).first()).toBeVisible({ timeout: 10_000 });
  });

  /**
   * BLOG-TAG-002: Create tag via UI
   * Click create button, fill name in dialog, save, verify success toast and list.
   */
  test('BLOG-TAG-002: should create blog tag via UI @smoke', async ({
    api,
    trackCleanup,
    page,
  }) => {
    const tagData = testBlogTag();

    await page.goto('/portal/blog/tags');
    await page.waitForLoadState('networkidle');

    // Click the create/add/new button
    await page.getByRole('button', { name: /create|add|new/i }).first().click();

    // Wait for dialog to appear
    await page.locator('[role="dialog"]').waitFor({ state: 'visible', timeout: 5_000 });

    // Fill the name field
    await page.getByLabel(/name/i).first().fill(tagData.name);

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

    // Verify tag appears in the list
    await expect(page.getByText(tagData.name).first()).toBeVisible({ timeout: 5_000 });

    // Cleanup: find and delete the created tag via API
    trackCleanup(async () => {
      const searchRes = await api['request'].get(
        `${process.env.API_URL ?? 'http://localhost:4000'}/api/blog/tags`,
      );
      const searchData = await searchRes.json();
      const items = searchData?.items ?? searchData?.data ?? searchData;
      if (Array.isArray(items)) {
        for (const item of items) {
          if (item.name === tagData.name) {
            await api.deleteBlogTag(item.id ?? item.Id).catch(() => {});
          }
        }
      }
    });
  });
});

// ─── Blog Tags: Regression Tests ──────────────────────────────────────────────

test.describe('Blog Tags @regression', () => {
  /**
   * BLOG-TAG-003: Edit tag
   * Seed tag via API, find row, open actions dropdown, edit name, verify.
   */
  test('BLOG-TAG-003: should edit blog tag @regression', async ({
    api,
    trackCleanup,
    page,
  }) => {
    // Seed a tag via API
    const tagData = testBlogTag();
    const created = await api.createBlogTag(tagData);
    trackCleanup(async () => { await api.deleteBlogTag(created.id ?? created.Id).catch(() => {}); });

    // Navigate without URL search param (React state is not URL-synced)
    await page.goto('/portal/blog/tags');
    await page.waitForLoadState('networkidle');

    // Search for the tag — scope to main to avoid matching sidebar "Search menu..." input
    const searchInput = page.locator('main').getByPlaceholder(/search/i).first();
    if (await searchInput.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await searchInput.fill(tagData.name);
      await page.waitForLoadState('networkidle');
    }

    // Find the tag row
    const tagRow = page.getByRole('row', { name: new RegExp(tagData.name, 'i') });
    await expect(tagRow).toBeVisible({ timeout: 10_000 });

    // Click the actions dropdown button (first button in the row)
    const actionBtn = tagRow.getByRole('button').first();
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
    const updatedName = `BlogTag-Updated-${uniqueId('edit')}`;
    const nameInput = activeDialog.getByLabel(/name/i).first();
    await nameInput.clear();
    await nameInput.fill(updatedName);

    // Save changes
    await activeDialog.getByRole('button', { name: /save|update|submit/i }).click();
    await expect(page.locator(TOAST_SUCCESS).first()).toBeVisible({ timeout: 10_000 });

    // Wait for dialog to close
    await page.waitForTimeout(500);

    // Navigate fresh and search for updated name (avoids React Query stale cache issue)
    await page.goto('/portal/blog/tags');
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
   * BLOG-TAG-004: Delete tag with confirmation
   * Seed tag via API, delete via UI with confirmation, verify removed.
   */
  test('BLOG-TAG-004: should delete blog tag with confirmation @regression', async ({
    api,
    page,
  }) => {
    // Seed a tag via API
    const tagData = testBlogTag();
    const created = await api.createBlogTag(tagData);
    // No trackCleanup needed — we're deleting via UI

    // Navigate without URL search param (React state is not URL-synced)
    await page.goto('/portal/blog/tags');
    await page.waitForLoadState('networkidle');

    // Search for the tag — scope to main to avoid matching sidebar "Search menu..." input
    const searchInput = page.locator('main').getByPlaceholder(/search/i).first();
    if (await searchInput.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await searchInput.fill(tagData.name);
      await page.waitForLoadState('networkidle');
    }

    // Find the tag row
    const tagRow = page.getByRole('row', { name: new RegExp(tagData.name, 'i') });
    await expect(tagRow).toBeVisible({ timeout: 10_000 });

    // Click the actions dropdown button (first button in the row)
    const actionBtn = tagRow.getByRole('button').first();
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

    // Verify tag is removed from the list
    await page.waitForTimeout(1_000); // Allow list to refresh
    await expect(page.getByRole('row', { name: new RegExp(tagData.name, 'i') })).not.toBeVisible({ timeout: 5_000 });
  });
});
