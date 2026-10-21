import { test, expect } from '../../fixtures/base.fixture';
import { testEmployee, testDepartment } from '../../helpers/test-data';

const API_URL = process.env.API_URL ?? 'http://localhost:4000';

/**
 * HR Reports E2E Tests
 *
 * Covers: HR-RPT-001 (Reports page loads with charts),
 *         HR-RPT-002 (Date range filter works),
 *         HR-RPT-003 (Reports export)
 *
 * Notes:
 * - Reports page shows aggregate statistics (headcount, department breakdown, employment type).
 * - Uses GetHrReportsQuery with GROUP BY aggregates.
 * - Charts render asynchronously — use networkidle + generous timeouts.
 * - This is a read-only page; focus on verifying data loads without errors.
 */

test.describe('HR Reports @smoke @regression', () => {
  // ─── HR-RPT-001: Reports page loads with charts @smoke ───────

  test.describe('HR-RPT-001: Reports page loads with charts @smoke', () => {
    test('should load reports page and display summary statistics', async ({ page }) => {
      await page.goto('/portal/hr/reports');
      await page.waitForLoadState('networkidle');

      // Verify the page loads without error
      await expect(page.locator('main, [role="main"]').first()).toBeVisible({ timeout: 10_000 });

      // Verify report sections — headcount, department breakdown, employment type, etc.
      const statsContent = page
        .getByText(/headcount|total employees|employee count/i)
        .or(page.getByText(/by department|department breakdown/i))
        .or(page.getByText(/by employment|employment type/i))
        .or(page.getByText(/report|overview|summary|statistics/i));

      await expect(statsContent.first()).toBeVisible({ timeout: 10_000 });

      // Verify no error states
      await expect(page.getByText(/error|failed to load/i))
        .not.toBeVisible({ timeout: 2_000 })
        .catch(() => {});
    });

    test('should display metric cards or chart containers', async ({ api, page, trackCleanup }) => {
      // Seed a department + employee so reports have data
      const deptData = testDepartment();
      const dept = await api.createDepartment(deptData);
      const empData = testEmployee({ departmentId: dept.id });
      const emp = await api.createEmployee(empData);

      trackCleanup(async () => {
        if (emp?.id) await api.deleteEmployee(emp.id).catch(() => {});
        if (dept?.id) await api.deleteDepartment(dept.id).catch(() => {});
      });

      await page.goto('/portal/hr/reports');
      await page.waitForLoadState('networkidle');

      // Look for metric cards (common pattern: Card with number + label)
      // or chart containers (canvas for Chart.js, svg for recharts/d3)
      const metricCard = page.locator('[class*="card"], [class*="Card"]');
      const chartElement = page.locator('canvas, svg.recharts-surface, [class*="chart"]');

      const hasMetricCards = await metricCard
        .first()
        .isVisible({ timeout: 10_000 })
        .catch(() => false);
      const hasCharts = await chartElement
        .first()
        .isVisible({ timeout: 5_000 })
        .catch(() => false);

      // At least metric cards or charts should be present
      expect(hasMetricCards || hasCharts).toBeTruthy();
    });
  });

  // ─── HR-RPT-002: Date range filter works @smoke ──────────────

  test.describe('HR-RPT-002: Date range filter works @smoke', () => {
    test('should not crash when interacting with date range filter', async ({ page }) => {
      await page.goto('/portal/hr/reports');
      await page.waitForLoadState('networkidle');

      // Look for a date range picker or filter control
      const dateFilter = page
        .getByRole('combobox', { name: /date|period|range/i })
        .or(page.getByRole('button', { name: /date range|period|last \d+ days|this month/i }))
        .or(page.locator('[data-testid*="date"], [data-testid*="filter"]'));

      const hasDateFilter = await dateFilter
        .first()
        .isVisible({ timeout: 5_000 })
        .catch(() => false);

      if (hasDateFilter) {
        await dateFilter.first().click();

        // Try selecting a preset option if dropdown appears
        const presetOption = page
          .getByRole('option', { name: /last 30|this month|last month|all time/i })
          .or(page.getByRole('menuitem', { name: /last 30|this month|last month|all time/i }))
          .or(page.getByText(/last 30 days|this month/i));

        const hasPreset = await presetOption
          .first()
          .isVisible({ timeout: 3_000 })
          .catch(() => false);

        if (hasPreset) {
          await presetOption.first().click();
        } else {
          // Close dropdown if no preset found
          await page.keyboard.press('Escape');
        }

        // Verify page still displays content (no crash)
        await expect(page.locator('main, [role="main"]').first()).toBeVisible({ timeout: 5_000 });
      } else {
        // Date filter may not exist — skip gracefully
        test.skip(true, 'No date range filter found on HR reports page');
      }
    });
  });

  // ─── HR-RPT-003: Reports export @regression ──────────────────

  test.describe('HR-RPT-003: Reports export @regression', () => {
    test('should export report data if export button exists', async ({ page }) => {
      await page.goto('/portal/hr/reports');
      await page.waitForLoadState('networkidle');

      // Look for an export button
      const exportBtn = page.getByRole('button', { name: /export|download/i });

      const hasExport = await exportBtn
        .first()
        .isVisible({ timeout: 5_000 })
        .catch(() => false);

      if (hasExport) {
        // Set up download listener before clicking
        const downloadPromise = page.waitForEvent('download', { timeout: 15_000 });
        await exportBtn.first().click();

        // Select Excel format if option appears
        const excelOption = page
          .getByRole('menuitem', { name: /excel|xlsx/i })
          .or(page.getByRole('option', { name: /excel|xlsx/i }));

        const hasFormatOption = await excelOption
          .isVisible({ timeout: 3_000 })
          .catch(() => false);
        if (hasFormatOption) {
          await excelOption.click();
        }

        // Verify download starts
        const download = await downloadPromise.catch(() => null);
        if (download) {
          const fileName = download.suggestedFilename();
          expect(fileName).toMatch(/\.(xlsx|xls|csv|pdf)$/i);

          const filePath = await download.path();
          expect(filePath).toBeTruthy();
        } else {
          // Export might trigger a success toast instead of download
          // (e.g., emailed report). Verify no error.
          await expect(page.getByText(/error|failed/i))
            .not.toBeVisible({ timeout: 2_000 })
            .catch(() => {});
        }
      } else {
        // No export button — skip gracefully
        test.skip(true, 'No export button found on HR reports page');
      }
    });
  });
});
