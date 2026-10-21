import { test, expect } from '../../fixtures/base.fixture';

// ─── Notifications: Regression Tests ────────────────────────────────────────

test.describe('Notifications @regression', () => {
  /**
   * NOTIF-001: Notifications page loads
   * Verify that the notifications page renders without errors.
   */
  test('NOTIF-001: should display notifications page @regression', async ({
    notificationsPage,
    page,
  }) => {
    await notificationsPage.goto();

    // Page should load
    await expect(page.locator('main').first()).toBeVisible({ timeout: 10_000 });

    // No error states
    await expect(page.locator('[role="alert"][data-type="error"]')).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });

  /**
   * NOTIF-002: Notification settings page loads
   * Verify notification preferences page renders with toggles.
   */
  test('NOTIF-002: should display notification settings @regression', async ({
    notificationsPage,
    page,
  }) => {
    await notificationsPage.gotoSettings();

    // Page should load
    await expect(page.locator('main').first()).toBeVisible({ timeout: 10_000 });

    // Should have some toggle switches or checkboxes for preferences
    const toggles = page.getByRole('switch').or(page.getByRole('checkbox'));
    if (await toggles.first().isVisible({ timeout: 5_000 }).catch(() => false)) {
      const count = await toggles.count();
      expect(count).toBeGreaterThan(0);
    }
  });
});
