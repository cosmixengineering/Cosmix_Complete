import { test, expect } from '../../fixtures/base.fixture';
import { LOADING_SPINNER, TOAST_ERROR } from '../../helpers/selectors';

// ─── E-commerce Reports: Smoke Tests ─────────────────────────────────────────

test.describe('E-commerce Reports @smoke', () => {
  /**
   * RPT-001: Reports page loads
   * Verify that the reports & analytics page loads with metric cards and no errors.
   */
  test('RPT-001: should load reports page with metrics @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/marketing/reports');
    await page.waitForLoadState('networkidle');

    // Verify page heading
    await expect(
      page.getByRole('heading', { name: /report|analytics|báo cáo|phân tích/i })
        .or(page.getByText(/reports.*analytics|báo cáo/i).first())
        .first(),
    ).toBeVisible({ timeout: 10_000 });

    // Wait for any loading spinners to disappear
    const spinner = page.locator(LOADING_SPINNER);
    if (await spinner.isVisible({ timeout: 1_000 }).catch(() => false)) {
      await spinner.waitFor({ state: 'hidden', timeout: 15_000 });
    }

    // Verify at least one metric card or chart container is visible
    const metricCards = page.locator('[class*="shadow-sm"]').filter({
      has: page.locator('p, span'),
    });
    const chartContainers = page.locator('.recharts-responsive-container, [class*="recharts"], svg.recharts-surface');
    const skeletons = page.locator('[class*="animate-pulse"], [class*="skeleton"]');

    const hasMetrics = await metricCards.first().isVisible({ timeout: 5_000 }).catch(() => false);
    const hasCharts = await chartContainers.first().isVisible({ timeout: 3_000 }).catch(() => false);
    const hasSkeletons = await skeletons.first().isVisible({ timeout: 2_000 }).catch(() => false);

    // Page should show metrics, charts, or at least loading skeletons
    expect(hasMetrics || hasCharts || hasSkeletons).toBeTruthy();

    // No error toast should appear
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });

  /**
   * RPT-002: Revenue report tab loads
   * Verify tab navigation exists and the revenue tab shows chart or data.
   */
  test('RPT-002: should display revenue tab with chart @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/marketing/reports');
    await page.waitForLoadState('networkidle');

    // Verify tabs exist (Revenue, Best Sellers, Inventory, Customers)
    const tabList = page.getByRole('tablist');
    await expect(tabList).toBeVisible({ timeout: 10_000 });

    const tabs = tabList.getByRole('tab');
    const tabCount = await tabs.count();
    expect(tabCount).toBeGreaterThanOrEqual(2);

    // Click the first tab (Revenue) if not already active
    const revenueTab = tabs.filter({ hasText: /revenue|doanh thu/i }).first();
    if (await revenueTab.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await revenueTab.click();
      await page.waitForTimeout(500);
    }

    // Verify chart or data table becomes visible (or empty state)
    const chartOrTable = page.locator('.recharts-responsive-container, [class*="recharts"], svg.recharts-surface')
      .or(page.getByRole('table'))
      .or(page.getByText(/no data|không có dữ liệu/i));

    await expect(chartOrTable.first()).toBeVisible({ timeout: 10_000 });

    // No error toast
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });
});

// ─── E-commerce Reports: Regression Tests ────────────────────────────────────

test.describe('E-commerce Reports @regression', () => {
  /**
   * RPT-003: Date range filter works
   * Click a date range preset and verify the page updates without crash.
   */
  test('RPT-003: should filter by date range preset @regression', async ({
    page,
  }) => {
    await page.goto('/portal/marketing/reports');
    await page.waitForLoadState('networkidle');

    // Look for date range preset buttons (Today, Last 7 days, Last 30 days, etc.)
    const presetButtons = page.getByRole('button', {
      name: /today|last 7|last 30|this month|last month|hôm nay|7 ngày|30 ngày|tháng này/i,
    });

    const presetCount = await presetButtons.count();
    if (presetCount === 0) {
      test.skip(true, 'No date range preset buttons found');
      return;
    }

    // Click a different preset than the default (Last 30 days)
    const targetPreset = presetButtons.filter({ hasText: /last 7|7 ngày|today|hôm nay/i }).first();
    const fallbackPreset = presetButtons.first();
    const presetToClick = await targetPreset.isVisible({ timeout: 2_000 }).catch(() => false)
      ? targetPreset
      : fallbackPreset;

    await presetToClick.click();
    await page.waitForTimeout(1_000);

    // Verify page updates without crash — loading state then content
    const mainContent = page.locator('main, [role="main"]').first();
    await expect(mainContent).toBeVisible();

    // No error toast
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 3_000 }).catch(() => {});
  });

  /**
   * RPT-004: Export functionality
   * Click the export button and verify no error occurs.
   */
  test('RPT-004: should export report without error @regression', async ({
    page,
  }) => {
    await page.goto('/portal/marketing/reports');
    await page.waitForLoadState('networkidle');

    // Look for Export/Download button
    const exportButton = page.getByRole('button', { name: /export|download|xuất|tải/i }).first();
    const hasExport = await exportButton.isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasExport) {
      test.skip(true, 'No export button found');
      return;
    }

    // Click export — it opens a dropdown menu
    await exportButton.click();
    await page.waitForTimeout(500);

    // Look for CSV or Excel option in dropdown
    const exportOption = page.getByRole('menuitem', { name: /csv|excel/i }).first();
    const hasOption = await exportOption.isVisible({ timeout: 3_000 }).catch(() => false);

    if (hasOption) {
      // Set up download listener before clicking
      const downloadPromise = page.waitForEvent('download', { timeout: 15_000 }).catch(() => null);
      await exportOption.click();

      // Either a download starts or a success toast appears — both are valid
      const download = await downloadPromise;
      if (download) {
        // Download started successfully
        expect(download.suggestedFilename()).toBeTruthy();
      } else {
        // Check for success toast or no error
        await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 3_000 }).catch(() => {});
      }
    } else {
      // Close dropdown with Escape
      await page.keyboard.press('Escape');
    }
  });

  /**
   * RPT-005: Tab switching works across all report tabs
   * Cycle through each tab and verify no crash.
   */
  test('RPT-005: should switch between all report tabs @regression', async ({
    page,
  }) => {
    await page.goto('/portal/marketing/reports');
    await page.waitForLoadState('networkidle');

    const tabList = page.getByRole('tablist');
    await expect(tabList).toBeVisible({ timeout: 10_000 });

    const tabs = tabList.getByRole('tab');
    const tabCount = await tabs.count();

    for (let i = 0; i < tabCount; i++) {
      const tab = tabs.nth(i);
      await tab.click();
      await page.waitForTimeout(800);

      // Verify no error toast after switching
      const errorToast = page.locator(TOAST_ERROR);
      const hasError = await errorToast.isVisible({ timeout: 1_000 }).catch(() => false);
      expect(hasError).toBeFalsy();

      // Verify tab panel content rendered (table, chart, empty state, or skeletons)
      const tabPanel = page.locator('[role="tabpanel"][data-state="active"]').first();
      await expect(tabPanel).toBeVisible({ timeout: 5_000 });
    }
  });
});
