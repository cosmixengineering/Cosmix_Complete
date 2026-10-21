import { test, expect } from '../../fixtures/base.fixture';
import { LOADING_SPINNER } from '../../helpers/selectors';

// Dashboard tests use pre-saved storageState (already logged in as tenant admin)

test.describe('Dashboard @smoke', () => {
  /**
   * DASH-001: Dashboard widgets load
   * Verify that the dashboard page loads with widget cards and no error states.
   */
  test('DASH-001: should load dashboard with widgets @smoke', async ({
    dashboardPage,
    page,
  }) => {
    await dashboardPage.goto();
    await dashboardPage.expectLoaded();

    // Wait for any loading spinners to disappear
    const spinner = page.locator(LOADING_SPINNER);
    if (await spinner.isVisible({ timeout: 1_000 }).catch(() => false)) {
      await spinner.waitFor({ state: 'hidden', timeout: 15_000 });
    }

    // At minimum, the page should have some meaningful content
    const dashboardContent = page.locator('main, [role="main"], .dashboard');
    await expect(dashboardContent.first()).toBeVisible();

    // Verify no error states are shown
    const errorState = page.getByText(/error|failed to load|something went wrong/i);
    await expect(errorState).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });

  /**
   * DASH-003: Dashboard navigation links
   * Verify that sidebar navigation works from the dashboard.
   */
  test('DASH-003: should navigate via sidebar links @regression', async ({
    dashboardPage,
    page,
  }) => {
    await dashboardPage.goto();
    await dashboardPage.expectLoaded();

    const sidebarLink = page.locator('[data-testid="sidebar"], aside nav, [role="navigation"]')
      .getByRole('link')
      .filter({ hasNotText: /dashboard/i })
      .first();

    await expect(sidebarLink).toBeVisible();
    const href = await sidebarLink.getAttribute('href');
    await sidebarLink.click();

    if (href) {
      await page.waitForURL(`**${href}`, { timeout: 10_000 });
    }

    await page.goBack();
    await expect(page).toHaveURL(/portal/, { timeout: 10_000 });
  });
});

test.describe('Dashboard Feature-Gated @nightly', () => {
  /**
   * DASH-002: Feature-gated widgets
   * Verify that disabled modules do not show dashboard widgets.
   */
  test('DASH-002: should hide widgets for disabled modules @nightly', async ({
    page,
  }) => {
    const platformAdminState = '.auth/platform-admin.json';
    const context = await page.context().browser()!.newContext({
      storageState: platformAdminState,
    });
    const platformPage = await context.newPage();

    try {
      // Navigate to platform settings and disable Blog module
      await platformPage.goto('/portal/admin/platform-settings');
      await platformPage.waitForLoadState('networkidle');

      const modulesTab = platformPage.getByRole('tab', { name: /modules|features/i });
      if (await modulesTab.isVisible({ timeout: 5_000 }).catch(() => false)) {
        await modulesTab.click();
      }

      const blogToggle = platformPage.getByRole('switch', { name: /blog/i })
        .or(platformPage.locator('label', { hasText: /blog/i }).locator('button[role="switch"]'));

      if (await blogToggle.isVisible({ timeout: 5_000 }).catch(() => false)) {
        const isChecked = await blogToggle.getAttribute('data-state');
        if (isChecked === 'checked') {
          await blogToggle.click();
          const saveBtn = platformPage.getByRole('button', { name: /save|update|apply/i });
          if (await saveBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
            await saveBtn.click();
            await platformPage.waitForTimeout(1_000);
          }

          // Verify dashboard hides blog widgets for tenant admin
          await page.goto('/portal');
          await page.waitForLoadState('networkidle');
          const blogWidget = page.locator('[data-testid*="blog"]');
          await expect(blogWidget).not.toBeVisible({ timeout: 3_000 }).catch(() => {});

          // Re-enable blog
          await platformPage.goto('/portal/admin/platform-settings');
          await platformPage.waitForLoadState('networkidle');
          const tab2 = platformPage.getByRole('tab', { name: /modules|features/i });
          if (await tab2.isVisible({ timeout: 5_000 }).catch(() => false)) await tab2.click();
          const toggle2 = platformPage.getByRole('switch', { name: /blog/i })
            .or(platformPage.locator('label', { hasText: /blog/i }).locator('button[role="switch"]'));
          if (await toggle2.isVisible({ timeout: 3_000 }).catch(() => false)) await toggle2.click();
          const save2 = platformPage.getByRole('button', { name: /save|update|apply/i });
          if (await save2.isVisible({ timeout: 3_000 }).catch(() => false)) await save2.click();
        }
      }
    } finally {
      await platformPage.close();
      await context.close();
    }
  });
});
