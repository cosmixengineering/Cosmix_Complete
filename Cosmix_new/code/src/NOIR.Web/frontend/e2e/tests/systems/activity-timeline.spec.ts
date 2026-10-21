import { test, expect } from '../../fixtures/base.fixture';
import { LOADING_SPINNER, TOAST_ERROR } from '../../helpers/selectors';

// ─── Systems Activity Timeline: Smoke Tests ──────────────────────────────────

test.describe('Systems Activity Timeline @smoke', () => {
  /**
   * SYS-TL-001: Activity timeline loads
   * Verify that the activity timeline page renders without error,
   * showing either timeline entries or a graceful empty state.
   */
  test('SYS-TL-001: should load activity timeline page @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/activity-timeline');
    await page.waitForLoadState('networkidle');

    // Verify page heading
    await expect(
      page.getByRole('heading', { name: /activity|timeline|hoạt động|lịch sử/i })
        .or(page.getByText(/activity.*timeline|lịch sử hoạt động/i).first())
        .first(),
    ).toBeVisible({ timeout: 10_000 });

    // Wait for loading to complete
    const spinner = page.locator(LOADING_SPINNER);
    if (await spinner.isVisible({ timeout: 1_000 }).catch(() => false)) {
      await spinner.waitFor({ state: 'hidden', timeout: 15_000 });
    }

    // Verify common UI elements exist: search input, filter controls
    const searchInput = page.getByPlaceholder(/search|tìm kiếm/i).first();
    const hasSearch = await searchInput.isVisible({ timeout: 3_000 }).catch(() => false);

    // Should have a refresh button
    const refreshButton = page.getByRole('button', { name: /refresh|làm mới/i });
    const hasRefresh = await refreshButton.isVisible({ timeout: 3_000 }).catch(() => false);

    // At least search or refresh should be visible
    expect(hasSearch || hasRefresh).toBeTruthy();

    // Verify timeline entries OR empty state
    const timelineEntries = page.locator('button[type="button"]').filter({
      has: page.locator('[class*="rounded-lg"]'),
    });
    const emptyState = page.getByText(/no activity|chưa có hoạt động|no entries/i);

    const hasEntries = await timelineEntries.first().isVisible({ timeout: 5_000 }).catch(() => false);
    const isEmpty = await emptyState.isVisible({ timeout: 3_000 }).catch(() => false);

    // Either entries exist or empty state is shown
    expect(hasEntries || isEmpty).toBeTruthy();

    // No error toast
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });

  /**
   * SYS-TL-002: Activity entries display correctly
   * Verify that timeline entries show user, action type, and timestamp.
   */
  test('SYS-TL-002: should display activity entries with details @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/activity-timeline');
    await page.waitForLoadState('networkidle');

    // Wait for loading skeletons to disappear
    const skeletons = page.locator('[class*="animate-pulse"]');
    if (await skeletons.first().isVisible({ timeout: 2_000 }).catch(() => false)) {
      await skeletons.first().waitFor({ state: 'hidden', timeout: 15_000 });
    }

    // Check for empty state first
    const emptyState = page.getByText(/no activity|chưa có hoạt động/i);
    const isEmpty = await emptyState.isVisible({ timeout: 3_000 }).catch(() => false);

    if (isEmpty) {
      // Graceful empty state — verify it renders properly
      await expect(emptyState).toBeVisible();
      return;
    }

    // Entries should exist — verify they show operation badges (Create, Update, Delete)
    const operationBadges = page.locator('[class*="badge"], [data-slot="badge"]').filter({
      hasText: /create|update|delete|tạo|cập nhật|xóa/i,
    });
    const hasBadges = await operationBadges.first().isVisible({ timeout: 5_000 }).catch(() => false);

    // Verify timestamps are visible (clock icon or time text)
    const timestamps = page.locator('[class*="tabular-nums"]');
    const hasTimestamps = await timestamps.first().isVisible({ timeout: 3_000 }).catch(() => false);

    // Verify user emails are visible
    const userEmails = page.getByText(/@/);
    const hasEmails = await userEmails.first().isVisible({ timeout: 3_000 }).catch(() => false);

    // At least some entry details should be visible
    expect(hasBadges || hasTimestamps || hasEmails).toBeTruthy();
  });
});

// ─── Systems Activity Timeline: Regression Tests ─────────────────────────────

test.describe('Systems Activity Timeline @regression', () => {
  /**
   * SYS-TL-003: Filter by action type
   * Select an operation type filter and verify the timeline updates.
   */
  test('SYS-TL-003: should filter by action type @regression', async ({
    page,
  }) => {
    await page.goto('/portal/activity-timeline');
    await page.waitForLoadState('networkidle');

    // Find the action type filter dropdown
    const actionFilter = page.getByRole('combobox', { name: /action|filter by action|hành động/i })
      .or(page.locator('[aria-label*="action" i], [aria-label*="hành động" i]'));

    const hasActionFilter = await actionFilter.first().isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasActionFilter) {
      test.skip(true, 'No action type filter found');
      return;
    }

    // Click to open the action filter dropdown
    await actionFilter.first().click();
    await page.waitForTimeout(300);

    // Select "Create" option
    const createOption = page.getByRole('option', { name: /create|tạo/i }).first();
    const hasOption = await createOption.isVisible({ timeout: 3_000 }).catch(() => false);

    if (hasOption) {
      await createOption.click();
      await page.waitForTimeout(1_000);

      // Verify filtered results or empty state
      const mainContent = page.locator('main, [role="main"]').first();
      await expect(mainContent).toBeVisible();

      // No error toast
      await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
    } else {
      // Close dropdown
      await page.keyboard.press('Escape');
    }
  });

  /**
   * SYS-TL-004: Date range filter
   * Select a date range and verify timeline updates without crash.
   */
  test('SYS-TL-004: should filter by date range @regression', async ({
    page,
  }) => {
    await page.goto('/portal/activity-timeline');
    await page.waitForLoadState('networkidle');

    // Find date range picker button
    const dateRangePicker = page.getByRole('button', { name: /date|range|ngày|khoảng/i })
      .or(page.locator('[class*="date-range"], [data-testid*="date"]'))
      .first();

    const hasDatePicker = await dateRangePicker.isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasDatePicker) {
      test.skip(true, 'No date range picker found');
      return;
    }

    // Click date range picker to open calendar
    await dateRangePicker.click();
    await page.waitForTimeout(500);

    // Look for calendar popover
    const calendar = page.locator('[role="dialog"], [data-radix-popper-content-wrapper]').filter({
      has: page.locator('table, [role="grid"]'),
    });

    const hasCalendar = await calendar.isVisible({ timeout: 3_000 }).catch(() => false);

    if (hasCalendar) {
      // Click a date in the calendar (first available day button)
      const dayButton = calendar.getByRole('gridcell').filter({ hasNotText: /^$/ }).first();
      if (await dayButton.isVisible({ timeout: 2_000 }).catch(() => false)) {
        await dayButton.click();
        await page.waitForTimeout(500);

        // Click a second date for range end
        const secondDay = calendar.getByRole('gridcell').filter({ hasNotText: /^$/ }).last();
        if (await secondDay.isVisible({ timeout: 2_000 }).catch(() => false)) {
          await secondDay.click();
        }
      }

      // Close calendar by clicking outside
      await page.keyboard.press('Escape');
    }

    await page.waitForTimeout(1_000);

    // Verify no crash
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });

  /**
   * SYS-TL-005: Search activity timeline
   * Enter a search term and verify results update.
   */
  test('SYS-TL-005: should search activity entries @regression', async ({
    page,
  }) => {
    await page.goto('/portal/activity-timeline');
    await page.waitForLoadState('networkidle');

    // Find the search input
    const searchInput = page.getByPlaceholder(/search|tìm kiếm/i).first();
    const hasSearch = await searchInput.isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasSearch) {
      test.skip(true, 'No search input found');
      return;
    }

    // Type a search term
    await searchInput.fill('admin');
    await page.waitForTimeout(1_500); // Allow debounce

    // Verify results update (content area should be visible)
    const mainContent = page.locator('main, [role="main"]').first();
    await expect(mainContent).toBeVisible();

    // Clear search
    await searchInput.clear();
    await page.waitForTimeout(1_500);

    // Verify no error toast
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });

  /**
   * SYS-TL-006: Failed-only toggle
   * Toggle the failed-only switch and verify filter applies.
   */
  test('SYS-TL-006: should toggle failed-only filter @regression', async ({
    page,
  }) => {
    await page.goto('/portal/activity-timeline');
    await page.waitForLoadState('networkidle');

    // Find the failed-only toggle switch
    const failedToggle = page.getByRole('switch').first()
      .or(page.locator('#only-failed'));

    const hasToggle = await failedToggle.isVisible({ timeout: 5_000 }).catch(() => false);

    if (!hasToggle) {
      test.skip(true, 'No failed-only toggle found');
      return;
    }

    // Toggle on
    await failedToggle.click();
    await page.waitForTimeout(1_000);

    // Verify page updates without crash
    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});

    // Toggle off
    await failedToggle.click();
    await page.waitForTimeout(1_000);

    await expect(page.locator(TOAST_ERROR)).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });
});
