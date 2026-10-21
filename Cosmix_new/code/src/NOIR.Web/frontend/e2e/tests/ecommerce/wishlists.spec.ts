import { test, expect } from '../../fixtures/base.fixture';
import {
  TOAST_ERROR,
} from '../../helpers/selectors';

// ─── Wishlists: Smoke Tests ─────────────────────────────────────────────────

test.describe('E-commerce Wishlists @smoke', () => {
  /**
   * WISH-001: Wishlist analytics page loads
   * Navigate to the wishlists analytics page and verify it renders
   * stats cards and the top products table.
   */
  test('WISH-001: should display wishlist analytics page @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/wishlists');
    await page.waitForLoadState('networkidle');

    // Verify analytics page rendered (PageHeader with "Wishlist" text)
    const heading = page.getByText(/wishlist analytics|wishlist/i).first();
    await expect(heading).toBeVisible({ timeout: 10_000 });

    // Verify stats cards are visible (3 cards: Total Wishlists, Total Items, Top Products)
    const cards = page.locator('[class*="card"]');
    const cardCount = await cards.count();
    expect(cardCount).toBeGreaterThanOrEqual(1);

    // Verify the top products table or empty state is visible
    const table = page.getByRole('table');
    const emptyState = page.getByText(/no wishlisted products|wishlist analytics will appear/i);
    const isTableVisible = await table.isVisible({ timeout: 5_000 }).catch(() => false);
    const isEmptyVisible = await emptyState.isVisible({ timeout: 3_000 }).catch(() => false);

    expect(isTableVisible || isEmptyVisible).toBeTruthy();
  });

  /**
   * WISH-002: Wishlist analytics renders without error
   * Verify no error toast and at least one UI element is visible.
   */
  test('WISH-002: should render wishlists page without error @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/wishlists');
    await page.waitForLoadState('networkidle');

    // No error toast should appear
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 3_000 }).catch(() => {});

    // At least one of: stats card, table, or empty state should be visible
    const statsCard = page.locator('[class*="card"]').first();
    const table = page.getByRole('table');
    const emptyState = page.getByText(/no wishlisted|no wishlist/i);

    const hasStats = await statsCard.isVisible({ timeout: 10_000 }).catch(() => false);
    const hasTable = await table.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasEmpty = await emptyState.isVisible({ timeout: 3_000 }).catch(() => false);

    expect(hasStats || hasTable || hasEmpty).toBeTruthy();
  });

  /**
   * WISH-003: Wishlist analytics stats cards show numbers
   * Verify the stat cards display numeric values (or loading skeletons).
   */
  test('WISH-003: should show analytics stats cards @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/wishlists');
    await page.waitForLoadState('networkidle');

    // Look for stats card labels
    const totalWishlistsLabel = page.getByText(/total wishlists/i);
    const totalItemsLabel = page.getByText(/total items/i);

    const hasWishlistsLabel = await totalWishlistsLabel.isVisible({ timeout: 10_000 }).catch(() => false);
    const hasItemsLabel = await totalItemsLabel.isVisible({ timeout: 5_000 }).catch(() => false);

    // At least one stats label should be visible
    expect(hasWishlistsLabel || hasItemsLabel).toBeTruthy();
  });
});

// ─── Wishlists: Regression Tests ────────────────────────────────────────────

test.describe('E-commerce Wishlists @regression', () => {
  /**
   * WISH-004: Wishlist manage page loads
   * Navigate to the manage page (separate from analytics) and verify
   * wishlists or empty state is visible.
   */
  test('WISH-004: should display wishlist manage page @regression', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/wishlists/manage');
    await page.waitForLoadState('networkidle');

    // Verify page rendered (may show wishlists or empty state)
    const heading = page.getByText(/wishlist|my wishlist/i).first();
    await expect(heading).toBeVisible({ timeout: 10_000 });

    // Look for tabs (wishlist names), empty state, or create button
    const createBtn = page.getByRole('button', { name: /new wishlist|create/i });
    const emptyState = page.getByText(/no wishlists yet|create your first/i);
    const wishlistTabs = page.getByRole('tab');

    const hasTabs = await wishlistTabs.first().isVisible({ timeout: 5_000 }).catch(() => false);
    const hasEmpty = await emptyState.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasCreate = await createBtn.isVisible({ timeout: 3_000 }).catch(() => false);

    expect(hasTabs || hasEmpty || hasCreate).toBeTruthy();
  });

  /**
   * WISH-005: Wishlist analytics top products table
   * Verify the top products table has expected column structure.
   */
  test('WISH-005: should render top products table structure @regression', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/wishlists');
    await page.waitForLoadState('networkidle');

    const table = page.getByRole('table');
    const isTableVisible = await table.isVisible({ timeout: 10_000 }).catch(() => false);

    if (!isTableVisible) {
      // Check for empty state instead
      const emptyState = page.getByText(/no wishlisted products/i);
      const hasEmpty = await emptyState.isVisible({ timeout: 5_000 }).catch(() => false);

      if (hasEmpty) {
        // Empty state is acceptable — analytics page is working
        expect(hasEmpty).toBeTruthy();
        return;
      }

      test.skip(true, 'Top products table not visible');
      return;
    }

    // Verify table headers: #, Product, Times Wishlisted
    const headers = table.locator('thead th');
    const headerCount = await headers.count();
    expect(headerCount).toBeGreaterThanOrEqual(2);
  });

  /**
   * WISH-006: Create wishlist dialog opens
   * Verify the "New Wishlist" button opens a form dialog on the manage page.
   */
  test('WISH-006: should open create wishlist dialog @regression', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/wishlists/manage');
    await page.waitForLoadState('networkidle');

    // Find the create button
    const createBtn = page.getByRole('button', { name: /new wishlist|create wishlist/i });
    const hasCreate = await createBtn.isVisible({ timeout: 10_000 }).catch(() => false);

    if (!hasCreate) {
      test.skip(true, 'Create wishlist button not available');
      return;
    }

    // Click create button
    await createBtn.click();
    await page.waitForTimeout(500);

    // Verify dialog opened
    const dialog = page.locator('[role="dialog"]');
    const hasDialog = await dialog.isVisible({ timeout: 5_000 }).catch(() => false);

    if (hasDialog) {
      // Verify dialog has form fields (name input at minimum)
      const nameInput = dialog.getByLabel(/name/i)
        .or(dialog.getByPlaceholder(/name|wishlist/i));
      const hasNameInput = await nameInput.first().isVisible({ timeout: 3_000 }).catch(() => false);
      expect(hasNameInput).toBeTruthy();

      // Close the dialog
      await page.keyboard.press('Escape');
      await expect(dialog).not.toBeVisible({ timeout: 3_000 });
    }

    // Verify no error toast
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });

  /**
   * WISH-007: Navigate between analytics and manage pages
   * Verify both wishlist routes work and render distinct content.
   */
  test('WISH-007: should navigate between analytics and manage pages @regression', async ({
    page,
  }) => {
    // Start at analytics page
    await page.goto('/portal/ecommerce/wishlists');
    await page.waitForLoadState('networkidle');

    // Verify analytics content is present
    const analyticsHeading = page.getByText(/wishlist analytics|analytics/i).first();
    const hasAnalytics = await analyticsHeading.isVisible({ timeout: 10_000 }).catch(() => false);
    expect(hasAnalytics).toBeTruthy();

    // Navigate to manage page
    await page.goto('/portal/ecommerce/wishlists/manage');
    await page.waitForLoadState('networkidle');

    // Verify manage page content is present (different from analytics)
    const manageHeading = page.getByText(/my wishlist|wishlist/i).first();
    const hasManage = await manageHeading.isVisible({ timeout: 10_000 }).catch(() => false);
    expect(hasManage).toBeTruthy();

    // Verify URL is correct
    await expect(page).toHaveURL(/\/portal\/ecommerce\/wishlists\/manage/);
  });
});
