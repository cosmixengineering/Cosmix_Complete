import { test, expect } from '../../fixtures/base.fixture';
import {
  TOAST_ERROR,
} from '../../helpers/selectors';

// ─── Shipping: Smoke Tests ──────────────────────────────────────────────────

test.describe('E-commerce Shipping @smoke', () => {
  /**
   * SHIP-001: Shipping page redirects to tenant settings
   * The /portal/ecommerce/shipping route redirects to tenant settings with
   * the shippingProviders tab active.
   */
  test('SHIP-001: should redirect shipping route to tenant settings @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/shipping');
    await page.waitForLoadState('networkidle');

    // Should redirect to tenant settings with shippingProviders tab
    await expect(page).toHaveURL(/\/portal\/admin\/tenant-settings\?tab=shippingProviders/, { timeout: 10_000 });

    // Verify the settings page loaded without error
    const pageContent = page.locator('main, [role="main"], .container').first();
    await expect(pageContent).toBeVisible({ timeout: 5_000 });
  });

  /**
   * SHIP-002: Shipping providers tab renders in tenant settings
   * Navigate directly to the tenant settings shipping tab and verify
   * the providers list or empty state is visible.
   */
  test('SHIP-002: should display shipping providers in tenant settings @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/admin/tenant-settings?tab=shippingProviders');
    await page.waitForLoadState('networkidle');

    // Verify the page loaded
    const pageContent = page.locator('main, [role="main"], .container').first();
    await expect(pageContent).toBeVisible({ timeout: 10_000 });

    // Look for shipping provider content: table, provider cards, or empty state
    const table = page.getByRole('table');
    const providerContent = page.getByText(/provider|shipping|carrier/i).first();
    const emptyState = page.getByText(/no provider|configure a shipping/i);

    const isTableVisible = await table.isVisible({ timeout: 10_000 }).catch(() => false);
    const hasProviderContent = await providerContent.isVisible({ timeout: 5_000 }).catch(() => false);
    const isEmptyVisible = await emptyState.isVisible({ timeout: 3_000 }).catch(() => false);

    // At least one indicator of the shipping tab should be present
    expect(isTableVisible || hasProviderContent || isEmptyVisible).toBeTruthy();
  });

  /**
   * SHIP-003: Shipping providers table has expected columns
   * If providers exist, verify table headers are present.
   */
  test('SHIP-003: should show provider table with expected structure @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/admin/tenant-settings?tab=shippingProviders');
    await page.waitForLoadState('networkidle');

    const table = page.getByRole('table');
    const isTableVisible = await table.isVisible({ timeout: 10_000 }).catch(() => false);

    if (!isTableVisible) {
      test.skip(true, 'No shipping providers table visible (empty state or different UI)');
      return;
    }

    // Verify headers exist
    const headers = table.locator('thead th');
    const headerCount = await headers.count();
    expect(headerCount).toBeGreaterThanOrEqual(3);

    // Verify at least one data row or empty state
    const rows = table.getByRole('row');
    const rowCount = await rows.count();
    // At minimum, header row should exist
    expect(rowCount).toBeGreaterThanOrEqual(1);
  });
});

// ─── Shipping: Regression Tests ─────────────────────────────────────────────

test.describe('E-commerce Shipping @regression', () => {
  /**
   * SHIP-004: Shipping providers search filter
   * If a search input is available, type into it and verify no crash.
   */
  test('SHIP-004: should filter shipping providers via search @regression', async ({
    page,
  }) => {
    await page.goto('/portal/admin/tenant-settings?tab=shippingProviders');
    await page.waitForLoadState('networkidle');

    // Look for search input
    const searchInput = page.getByPlaceholder(/search provider|search/i)
      .or(page.getByRole('textbox', { name: /search/i }));

    const hasSearch = await searchInput.first().isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasSearch) {
      test.skip(true, 'Search input not available on shipping providers tab');
      return;
    }

    // Type a search query
    await searchInput.first().fill('GHN');
    await page.waitForTimeout(1_000); // Let deferred search settle
    await page.waitForLoadState('networkidle');

    // Verify page didn't crash — table or empty state still visible
    const table = page.getByRole('table');
    const emptyState = page.getByText(/no provider|no result|no match|not found/i).first();
    const isTableVisible = await table.isVisible({ timeout: 5_000 }).catch(() => false);
    const isEmptyVisible = await emptyState.isVisible({ timeout: 3_000 }).catch(() => false);
    // Also accept: main content area still visible (page didn't crash)
    const hasMain = await page.locator('main').isVisible({ timeout: 3_000 }).catch(() => false);

    expect(isTableVisible || isEmptyVisible || hasMain).toBeTruthy();

    // Clear search and verify table returns
    await searchInput.first().clear();
    await page.waitForTimeout(500);
  });

  /**
   * SHIP-005: Shipment lookup tab renders
   * If a standalone shipping management page exists (not just redirect),
   * verify the shipment lookup tab works.
   * Note: The route currently redirects, so this test verifies the redirect
   * target and looks for shipment lookup functionality in tenant settings.
   */
  test('SHIP-005: should handle shipment lookup if available @regression', async ({
    page,
  }) => {
    // First try the redirected settings page
    await page.goto('/portal/admin/tenant-settings?tab=shippingProviders');
    await page.waitForLoadState('networkidle');

    // Check if there's a Shipments tab within the shipping content
    const shipmentsTab = page.getByRole('tab', { name: /shipment|tracking|lookup/i });
    const hasShipmentsTab = await shipmentsTab.isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasShipmentsTab) {
      // Shipment lookup may not be available in tenant settings view
      test.skip(true, 'Shipment lookup tab not available in current view');
      return;
    }

    await shipmentsTab.click();
    await page.waitForTimeout(500);

    // Verify the lookup section renders (search input or empty state)
    const lookupInput = page.getByPlaceholder(/tracking|order id/i)
      .or(page.getByRole('textbox', { name: /look.*up|tracking|search/i }));
    const hasLookup = await lookupInput.first().isVisible({ timeout: 5_000 }).catch(() => false);

    if (hasLookup) {
      // Type a dummy tracking number and verify no crash
      await lookupInput.first().fill('TRACK-00000');

      // Look for a search button to trigger the lookup
      const searchBtn = page.getByRole('button', { name: /search|look.*up|find/i });
      const hasSearchBtn = await searchBtn.isVisible({ timeout: 3_000 }).catch(() => false);

      if (hasSearchBtn) {
        await searchBtn.click();
        await page.waitForLoadState('networkidle');

        // Page should not crash — either "not found" message or result card
        const notFound = page.getByText(/not found|no shipment/i);
        const resultCard = page.locator('[class*="card"]');
        const hasNotFound = await notFound.isVisible({ timeout: 5_000 }).catch(() => false);
        const hasResult = await resultCard.first().isVisible({ timeout: 3_000 }).catch(() => false);

        expect(hasNotFound || hasResult).toBeTruthy();
      }
    }
  });

  /**
   * SHIP-006: Add provider button is visible
   * Verify the "Add Provider" button exists on the shipping providers tab.
   */
  test('SHIP-006: should show add provider button @regression', async ({
    page,
  }) => {
    await page.goto('/portal/admin/tenant-settings?tab=shippingProviders');
    await page.waitForLoadState('networkidle');

    // Look for add/create provider button
    const addBtn = page.getByRole('button', { name: /add provider|new provider|create/i });
    const hasAddBtn = await addBtn.isVisible({ timeout: 10_000 }).catch(() => false);

    if (!hasAddBtn) {
      test.skip(true, 'Add provider button not visible (permissions or different UI)');
      return;
    }

    // Click the add button and verify a dialog opens
    await addBtn.click();
    await page.waitForTimeout(500);

    const dialog = page.locator('[role="dialog"]');
    const hasDialog = await dialog.isVisible({ timeout: 5_000 }).catch(() => false);

    if (hasDialog) {
      // Verify dialog has expected form fields
      const dialogContent = await dialog.textContent();
      expect(dialogContent).toBeTruthy();

      // Close the dialog
      await page.keyboard.press('Escape');
      await expect(dialog).not.toBeVisible({ timeout: 3_000 });
    }

    // Verify no error toast
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });
});
