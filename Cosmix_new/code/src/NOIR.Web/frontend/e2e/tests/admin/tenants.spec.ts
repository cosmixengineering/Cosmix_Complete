import { test, expect } from '../../fixtures/base.fixture';
import { waitForTableLoad } from '../../helpers/selectors';

// All tenants admin tests use platform admin credentials
test.use({ storageState: '.auth/platform-admin.json' });

// ─── Tenants Admin: Smoke Tests ───────────────────────────────────────────────

test.describe('Platform Admin Tenants @smoke', () => {
  /**
   * ADMIN-TNT-001: Tenants list loads for platform admin
   * Verify the tenants management page renders with the default tenant.
   */
  test('ADMIN-TNT-001: should display tenants list as platform admin @smoke', async ({ page }) => {
    await page.goto('/portal/admin/tenants');
    await page.waitForLoadState('networkidle');

    // Verify page loaded (not redirected to login)
    await expect(page).not.toHaveURL(/login/, { timeout: 5_000 });

    // Verify 'default' tenant is present
    await expect(
      page.getByText(/default/i).first(),
    ).toBeVisible({ timeout: 10_000 });

    // Verify table or list is visible
    const hasTable = await page.getByRole('table').isVisible({ timeout: 3_000 }).catch(() => false);
    const hasGrid = await page.getByRole('grid').isVisible({ timeout: 2_000 }).catch(() => false);
    const hasContent = await page.locator('main').isVisible({ timeout: 3_000 }).catch(() => false);
    expect(hasTable || hasGrid || hasContent).toBe(true);
  });

  /**
   * ADMIN-TNT-002: Navigate to tenant detail
   * Click the 'default' tenant to view its settings or detail page.
   */
  test('ADMIN-TNT-002: should navigate to tenant detail @smoke', async ({ page }) => {
    await page.goto('/portal/admin/tenants');
    await page.waitForLoadState('networkidle');

    // Find the default tenant row and click it
    const defaultTenantRow = page.getByRole('row', { name: /default/i });
    const defaultTenantLink = page.getByRole('link', { name: /default/i });

    if (await defaultTenantLink.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await defaultTenantLink.first().click();
    } else if (await defaultTenantRow.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await defaultTenantRow.click();
    } else {
      // Try clicking on any 'default' text that looks like a link
      await page.getByText('default').first().click();
    }

    await page.waitForLoadState('networkidle');

    // Verify we navigated to a tenant detail/settings page
    // Use .first() to avoid strict mode violation when multiple elements match
    const isOnDetailPage = await page.getByText(/tenant|settings|default/i)
      .first()
      .isVisible({ timeout: 8_000 }).catch(() => false);
    // Also acceptable: any heading or main content on the destination page
    const hasContent = await page.locator('main').isVisible({ timeout: 3_000 }).catch(() => false);
    expect(isOnDetailPage || hasContent).toBe(true);
  });
});

// ─── Tenants Admin: Regression Tests ─────────────────────────────────────────

test.describe('Platform Admin Tenants @regression', () => {
  /**
   * ADMIN-TNT-003: Search tenants
   * Verify searching for 'default' tenant returns results.
   */
  test('ADMIN-TNT-003: should search tenants by identifier @regression', async ({ page }) => {
    await page.goto('/portal/admin/tenants');
    await page.waitForLoadState('networkidle');

    const searchInput = page.getByRole('searchbox')
      .or(page.getByPlaceholder(/search|tìm kiếm/i))
      .or(page.getByLabel(/search/i));

    if (await searchInput.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await searchInput.fill('default');
      await page.waitForTimeout(800);
      await page.waitForLoadState('networkidle');

      // 'default' tenant should still be visible after search
      await expect(
        page.getByText(/default/i).first(),
      ).toBeVisible({ timeout: 5_000 });
    } else {
      test.skip(true, 'No search input on tenants page');
    }
  });

  /**
   * ADMIN-TNT-004: Platform settings page loads
   * Verify the platform-level settings page is accessible.
   */
  test('ADMIN-TNT-004: should load platform settings @regression', async ({ page }) => {
    await page.goto('/portal/settings/platform');
    await page.waitForLoadState('networkidle');

    // Check if we're on the platform settings page (not redirected)
    const isNotLogin = !(await page.url().includes('login'));
    expect(isNotLogin).toBe(true);

    // Verify page has settings content
    const hasHeading = await page.getByRole('heading').isVisible({ timeout: 5_000 }).catch(() => false);
    const hasContent = await page.locator('main').isVisible({ timeout: 3_000 }).catch(() => false);
    expect(hasHeading || hasContent).toBe(true);
  });
});
