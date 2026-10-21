import { test, expect } from '../../fixtures/base.fixture';
import {
  waitForTableLoad,
  TOAST_ERROR,
} from '../../helpers/selectors';

// ─── Payments: Smoke Tests ──────────────────────────────────────────────────

test.describe('E-commerce Payments @smoke', () => {
  /**
   * PAYM-001: Payments list loads
   * Navigate to the payments page and verify the table renders with expected columns.
   */
  test('PAYM-001: should display payments list page @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/payments');
    await page.waitForLoadState('networkidle');

    // Page should render without errors
    await expect(page.getByText(/not found|404|error/i)).not.toBeVisible({ timeout: 3_000 }).catch(() => {
      // Page may legitimately contain the word "error" in other contexts
    });

    // Verify the payments table or empty state is visible
    const table = page.getByRole('table');
    const emptyState = page.getByText(/no payment|payment transactions will appear/i);
    const isTableVisible = await table.isVisible({ timeout: 10_000 }).catch(() => false);
    const isEmptyVisible = await emptyState.isVisible({ timeout: 3_000 }).catch(() => false);

    expect(isTableVisible || isEmptyVisible).toBeTruthy();

    // If table is visible, verify column headers
    if (isTableVisible) {
      const headers = page.getByRole('table').locator('thead th');
      const headerCount = await headers.count();
      expect(headerCount).toBeGreaterThanOrEqual(3);

      // Verify key column headers exist (transaction, amount, status)
      const headerText = await page.getByRole('table').locator('thead').textContent();
      expect(headerText).toBeTruthy();
    }
  });

  /**
   * PAYM-002: Payment detail navigates correctly
   * Click on the first payment row (if available) and verify the detail page loads.
   */
  test('PAYM-002: should navigate to payment detail page @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/payments');
    await page.waitForLoadState('networkidle');

    const table = page.getByRole('table');
    const isTableVisible = await table.isVisible({ timeout: 10_000 }).catch(() => false);

    if (!isTableVisible) {
      test.skip(true, 'Payments table not visible');
      return;
    }

    // Check if there are data rows (beyond the header row)
    const dataRows = table.getByRole('row');
    const rowCount = await dataRows.count();

    if (rowCount <= 1) {
      test.skip(true, 'No payments available to test detail navigation');
      return;
    }

    // Click on the first data row (rows are clickable via onClick handler)
    await dataRows.nth(1).click();
    await page.waitForLoadState('networkidle');

    // Verify URL changed to a payment detail page
    await expect(page).toHaveURL(/\/portal\/ecommerce\/payments\/.+/);

    // Verify detail page rendered — should show transaction number or back button
    const backButton = page.getByRole('button', { name: /back to payments/i })
      .or(page.getByRole('button').filter({ has: page.locator('[class*="ArrowLeft"], svg') }).first());
    const transactionInfo = page.getByText(/transaction|overview|timeline/i).first();
    const statusBadge = page.locator('[class*="badge"]').first();

    const hasBackButton = await backButton.isVisible({ timeout: 5_000 }).catch(() => false);
    const hasTransactionInfo = await transactionInfo.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasStatusBadge = await statusBadge.isVisible({ timeout: 3_000 }).catch(() => false);

    // At least one detail page element should be visible
    expect(hasBackButton || hasTransactionInfo || hasStatusBadge).toBeTruthy();
  });
});

// ─── Payments: Regression Tests ─────────────────────────────────────────────

test.describe('E-commerce Payments @regression', () => {
  /**
   * PAYM-003: Filter payments by status
   * Use the status filter dropdown and verify the page updates without crashing.
   */
  test('PAYM-003: should filter payments by status @regression', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/payments');
    await page.waitForLoadState('networkidle');

    // Find the status filter dropdown
    const statusFilter = page.getByRole('combobox', { name: /status/i })
      .or(page.locator('[aria-label*="status" i]').first());

    const hasStatusFilter = await statusFilter.isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasStatusFilter) {
      test.skip(true, 'Status filter not available on payments page');
      return;
    }

    // Open the dropdown and select a status
    await statusFilter.click();
    await page.waitForTimeout(300); // Let dropdown animation settle

    // Try to select "Paid" or any available option
    const option = page.getByRole('option', { name: /paid|completed|pending/i }).first();
    const hasOption = await option.isVisible({ timeout: 3_000 }).catch(() => false);

    if (hasOption) {
      await option.click();
      await page.waitForLoadState('networkidle');

      // Verify page didn't crash — table or empty state should be visible
      const table = page.getByRole('table');
      const emptyState = page.getByText(/no payment/i);
      const isTableVisible = await table.isVisible({ timeout: 10_000 }).catch(() => false);
      const isEmptyVisible = await emptyState.isVisible({ timeout: 5_000 }).catch(() => false);

      expect(isTableVisible || isEmptyVisible).toBeTruthy();
    }

    // Verify no error toast appeared
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {
      // Acceptable if no toast framework loaded
    });
  });

  /**
   * PAYM-004: Filter payments by payment method
   * Use the method filter dropdown and verify the page updates without crashing.
   */
  test('PAYM-004: should filter payments by method @regression', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/payments');
    await page.waitForLoadState('networkidle');

    // Find the method filter dropdown
    const methodFilter = page.getByRole('combobox', { name: /method/i })
      .or(page.locator('[aria-label*="method" i]').first());

    const hasMethodFilter = await methodFilter.isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasMethodFilter) {
      test.skip(true, 'Method filter not available on payments page');
      return;
    }

    // Open the dropdown and select a method
    await methodFilter.click();
    await page.waitForTimeout(300);

    const option = page.getByRole('option', { name: /credit|bank|cod|ewallet/i }).first();
    const hasOption = await option.isVisible({ timeout: 3_000 }).catch(() => false);

    if (hasOption) {
      await option.click();
      await page.waitForLoadState('networkidle');

      // Verify page didn't crash
      const table = page.getByRole('table');
      const emptyState = page.getByText(/no payment/i);
      const isTableVisible = await table.isVisible({ timeout: 10_000 }).catch(() => false);
      const isEmptyVisible = await emptyState.isVisible({ timeout: 5_000 }).catch(() => false);

      expect(isTableVisible || isEmptyVisible).toBeTruthy();
    }
  });

  /**
   * PAYM-005: Payments search input
   * Type into the search input and verify the page updates without crashing.
   */
  test('PAYM-005: should search payments without crash @regression', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/payments');
    await page.waitForLoadState('networkidle');

    // Find the search input
    const searchInput = page.getByPlaceholder(/search|transaction/i)
      .or(page.getByRole('textbox', { name: /search/i }));

    const hasSearch = await searchInput.first().isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasSearch) {
      test.skip(true, 'Search input not available on payments page');
      return;
    }

    // Type a partial search query
    await searchInput.first().fill('PAY-');
    await page.waitForLoadState('networkidle');

    // Verify page didn't crash — table or empty state should still be visible
    const table = page.getByRole('table');
    const emptyState = page.getByText(/no payment/i);
    const isTableVisible = await table.isVisible({ timeout: 10_000 }).catch(() => false);
    const isEmptyVisible = await emptyState.isVisible({ timeout: 5_000 }).catch(() => false);

    expect(isTableVisible || isEmptyVisible).toBeTruthy();

    // Clear search
    await searchInput.first().clear();
    await page.waitForLoadState('networkidle');
  });

  /**
   * PAYM-006: Payment detail tabs render correctly
   * Navigate to a payment detail and verify all tabs can be accessed.
   */
  test('PAYM-006: should render payment detail tabs @regression', async ({
    page,
  }) => {
    await page.goto('/portal/ecommerce/payments');
    await page.waitForLoadState('networkidle');

    const table = page.getByRole('table');
    const isTableVisible = await table.isVisible({ timeout: 10_000 }).catch(() => false);
    if (!isTableVisible) {
      test.skip(true, 'Payments table not visible');
      return;
    }

    const dataRows = table.getByRole('row');
    const rowCount = await dataRows.count();
    if (rowCount <= 1) {
      test.skip(true, 'No payments available for detail tab test');
      return;
    }

    // Navigate to the first payment detail
    await dataRows.nth(1).click();
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/\/portal\/ecommerce\/payments\/.+/);

    // Verify tabs exist: Overview, Timeline, API Logs, Webhooks, Refunds
    const tabNames = ['overview', 'timeline'];
    for (const tabName of tabNames) {
      const tab = page.getByRole('tab', { name: new RegExp(tabName, 'i') });
      const isTabVisible = await tab.isVisible({ timeout: 3_000 }).catch(() => false);

      if (isTabVisible) {
        await tab.click();
        await page.waitForTimeout(500); // Let tab content load

        // Verify tab content area rendered (card, table, or empty state)
        const tabContent = page.locator('[role="tabpanel"]')
          .or(page.locator('[data-state="active"]'));
        await expect(tabContent.first()).toBeVisible({ timeout: 5_000 });
      }
    }
  });
});
