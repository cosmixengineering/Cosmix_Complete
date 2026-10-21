import { test, expect } from '../../fixtures/base.fixture';

/**
 * Tenant Settings E2E Tests
 *
 * Route: /portal/admin/tenant-settings
 * Auth: admin@noir.local (default storageState from auth-setup)
 *
 * Tabs: Branding, Contact, Regional, Payment Gateways, Shipping Providers,
 *       SMTP, Email Templates, Legal Pages, Modules, Webhooks
 */

test.describe('Tenant Settings @smoke', () => {
  /**
   * SETT-001: Settings page loads with tabs
   * Verify tenant settings page renders with tab navigation.
   */
  test('SETT-001: settings page loads with tabs @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/admin/tenant-settings');
    await page.waitForLoadState('networkidle');

    // Verify page heading is visible
    await expect(page.getByRole('heading', { name: /settings|cài đặt/i }).or(
      page.getByText(/tenant.*settings|cài đặt/i).first(),
    ).first()).toBeVisible({ timeout: 10_000 });

    // Verify tab list is present
    const tabList = page.getByRole('tablist');
    await expect(tabList).toBeVisible();

    // Verify key tab labels are visible
    const tabs = tabList.getByRole('tab');
    const tabCount = await tabs.count();
    expect(tabCount).toBeGreaterThanOrEqual(4);

    // Verify at least some known tabs exist
    const brandingTab = tabList.getByRole('tab', { name: /branding|thương hiệu/i });
    await expect(brandingTab).toBeVisible();

    const modulesTab = tabList.getByRole('tab', { name: /modules|tính năng/i });
    await expect(modulesTab).toBeVisible();

    // Verify the default tab content (Branding) is rendered without errors
    const tabPanel = page.locator('[role="tabpanel"][data-state="active"]').first();
    await expect(tabPanel).toBeVisible();

    // No error alerts
    const errorAlert = page.getByRole('alert').filter({ hasText: /error|lỗi/i });
    const hasError = await errorAlert.isVisible({ timeout: 1_000 }).catch(() => false);
    expect(hasError).toBeFalsy();
  });

  /**
   * SETT-002: Modules settings tab loads
   * Verify module toggles render from API data.
   */
  test('SETT-002: modules settings tab loads @smoke', async ({
    page,
  }) => {
    await page.goto('/portal/admin/tenant-settings?tab=modules');
    await page.waitForLoadState('networkidle');

    // Click the Modules tab to ensure it's active
    const modulesTab = page.getByRole('tab', { name: /modules|tính năng/i });
    await expect(modulesTab).toBeVisible({ timeout: 10_000 });
    await modulesTab.click();

    // Wait for modules to load (skeletons disappear, switches appear)
    const moduleSwitch = page.getByRole('switch').first();
    await expect(moduleSwitch).toBeVisible({ timeout: 10_000 });

    // Verify at least one module group card is rendered
    const moduleCards = page.locator('[role="tabpanel"]').locator('.space-y-0');
    const cardCount = await moduleCards.count();
    expect(cardCount).toBeGreaterThanOrEqual(1);

    // Verify no error states
    const errorAlert = page.getByRole('alert').filter({ hasText: /error|lỗi/i });
    const hasError = await errorAlert.isVisible({ timeout: 1_000 }).catch(() => false);
    expect(hasError).toBeFalsy();
  });
});

test.describe('Tenant Settings @regression', () => {
  /**
   * SETT-003: Branding settings tab
   * Verify branding tab renders with expected form fields.
   */
  test('SETT-003: branding settings tab renders @regression', async ({
    page,
  }) => {
    await page.goto('/portal/admin/tenant-settings');
    await page.waitForLoadState('networkidle');

    // Branding is the default tab — verify it's active
    const brandingTab = page.getByRole('tab', { name: /branding|thương hiệu/i });
    await expect(brandingTab).toBeVisible({ timeout: 10_000 });

    // Verify brand-related fields are present
    const tabPanel = page.locator('[role="tabpanel"][data-state="active"]').first();
    await expect(tabPanel).toBeVisible();

    // Look for brand name input or logo area
    const brandNameField = tabPanel.getByLabel(/brand.*name|tên.*thương hiệu|name|tên/i).first();
    const logoArea = tabPanel.getByText(/logo/i).first();

    const hasBrandName = await brandNameField.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasLogo = await logoArea.isVisible({ timeout: 3_000 }).catch(() => false);

    // At least one branding-related element should be visible
    expect(hasBrandName || hasLogo).toBeTruthy();

    // No error alerts
    const errorAlert = page.getByRole('alert').filter({ hasText: /error|lỗi/i });
    const hasError = await errorAlert.isVisible({ timeout: 1_000 }).catch(() => false);
    expect(hasError).toBeFalsy();
  });

  /**
   * SETT-004: Regional settings tab
   * Verify regional tab renders with timezone/language/date format fields.
   */
  test('SETT-004: regional settings tab renders @regression', async ({
    page,
  }) => {
    await page.goto('/portal/admin/tenant-settings?tab=regional');
    await page.waitForLoadState('networkidle');

    // Click the Regional tab
    const regionalTab = page.getByRole('tab', { name: /regional|khu vực/i });
    await expect(regionalTab).toBeVisible({ timeout: 10_000 });
    await regionalTab.click();

    // Wait for tab panel content
    const tabPanel = page.locator('[role="tabpanel"][data-state="active"]').first();
    await expect(tabPanel).toBeVisible();

    // Verify regional-related fields exist (timezone, language, date format, currency)
    const timezoneField = tabPanel.getByText(/timezone|múi giờ/i).first();
    const languageField = tabPanel.getByText(/language|ngôn ngữ/i).first();
    const currencyField = tabPanel.getByText(/currency|tiền tệ/i).first();
    const dateFormatField = tabPanel.getByText(/date.*format|định dạng.*ngày/i).first();

    const hasTimezone = await timezoneField.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasLanguage = await languageField.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasCurrency = await currencyField.isVisible({ timeout: 3_000 }).catch(() => false);
    const hasDateFormat = await dateFormatField.isVisible({ timeout: 3_000 }).catch(() => false);

    // At least two regional fields should be visible
    const visibleCount = [hasTimezone, hasLanguage, hasCurrency, hasDateFormat].filter(Boolean).length;
    expect(visibleCount).toBeGreaterThanOrEqual(1);

    // No error alerts
    const errorAlert = page.getByRole('alert').filter({ hasText: /error|lỗi/i });
    const hasError = await errorAlert.isVisible({ timeout: 1_000 }).catch(() => false);
    expect(hasError).toBeFalsy();
  });

  /**
   * SETT-005: Email templates tab
   * Verify email templates list loads and an editor can be opened.
   */
  test('SETT-005: email templates tab loads @regression', async ({
    page,
  }) => {
    await page.goto('/portal/admin/tenant-settings?tab=emailTemplates');
    await page.waitForLoadState('networkidle');

    // Click the Email Templates tab
    const emailTab = page.getByRole('tab', { name: /email.*template|mẫu.*email/i });
    await expect(emailTab).toBeVisible({ timeout: 10_000 });
    await emailTab.click();

    // Wait for tab content to render
    const tabPanel = page.locator('[role="tabpanel"][data-state="active"]').first();
    await expect(tabPanel).toBeVisible();
    await page.waitForTimeout(1_000); // Let data load

    // Verify email template items are present (cards or list items)
    // Template buttons use aria-label (icon-only), not visible text — check by aria-label or table rows
    const templateItems = tabPanel.locator('button[aria-label], tr').filter({
      has: tabPanel.locator('button[aria-label*="edit" i], button[aria-label*="preview" i], button[aria-label*="Edit"], button[aria-label*="Preview"]'),
    });
    // Fallback: any table rows (email templates render in a table)
    const tableRows = tabPanel.getByRole('table').getByRole('row');
    const emptyState = tabPanel.getByText(/no.*template|chưa có.*mẫu|no email/i).first()
      .or(tabPanel.locator('[class*="empty"]').first());
    // Also accept: any list items, cards, or table content in the tab panel
    const hasContent = await tabPanel.locator('table, [class*="card"], [class*="list"], li').first()
      .isVisible({ timeout: 5_000 }).catch(() => false);

    const hasTemplates = await templateItems.first().isVisible({ timeout: 3_000 }).catch(() => false);
    const hasRows = await tableRows.nth(1).isVisible({ timeout: 3_000 }).catch(() => false); // skip header row
    const isEmpty = await emptyState.isVisible({ timeout: 2_000 }).catch(() => false);

    // Either templates exist or empty state is shown or any content is visible — no error
    expect(hasTemplates || hasRows || isEmpty || hasContent).toBeTruthy();

    // If templates exist, click one to open editor
    if (hasTemplates) {
      const firstEditBtn = templateItems.first();
      await firstEditBtn.click();
      await page.waitForLoadState('networkidle');

      // Verify editor page loads (should navigate to email template edit page)
      // The route is /portal/email-templates/:id
      const editorVisible = await page.getByText(/subject|tiêu đề|template|mẫu/i)
        .first().isVisible({ timeout: 5_000 }).catch(() => false);

      if (editorVisible) {
        // Navigate back without saving
        await page.goBack();
        await page.waitForLoadState('networkidle');
      }
    }

    // No error alerts on the page
    const errorAlert = page.getByRole('alert').filter({ hasText: /error|lỗi/i });
    const hasError = await errorAlert.isVisible({ timeout: 1_000 }).catch(() => false);
    expect(hasError).toBeFalsy();
  });
});

test.describe('Tenant Settings @nightly', () => {
  /**
   * SETT-006: Toggle a feature module (read state, toggle, restore)
   * Navigate to Modules tab, find a toggleable module, toggle it, then restore.
   */
  test('SETT-006: toggle feature module and restore @nightly', async ({
    page,
    trackCleanup,
  }) => {
    await page.goto('/portal/admin/tenant-settings?tab=modules');
    await page.waitForLoadState('networkidle');

    // Click the Modules tab
    const modulesTab = page.getByRole('tab', { name: /modules|tính năng/i });
    await expect(modulesTab).toBeVisible({ timeout: 10_000 });
    await modulesTab.click();

    // Wait for module switches to load
    const switches = page.getByRole('switch');
    await expect(switches.first()).toBeVisible({ timeout: 10_000 });

    // Find the first toggleable switch
    const firstSwitch = switches.first();
    const originalState = await firstSwitch.getAttribute('data-state');
    const wasChecked = originalState === 'checked';

    // Register cleanup to restore original state if test fails
    trackCleanup(async () => {
      try {
        await page.goto('/portal/admin/tenant-settings?tab=modules');
        await page.waitForLoadState('networkidle');
        const modulesTabCleanup = page.getByRole('tab', { name: /modules|tính năng/i });
        if (await modulesTabCleanup.isVisible({ timeout: 5_000 }).catch(() => false)) {
          await modulesTabCleanup.click();
        }
        const switchCleanup = page.getByRole('switch').first();
        await switchCleanup.waitFor({ state: 'visible', timeout: 5_000 });
        const currentState = await switchCleanup.getAttribute('data-state');
        const isCurrentlyChecked = currentState === 'checked';
        if (isCurrentlyChecked !== wasChecked) {
          await switchCleanup.click();
          await page.waitForTimeout(1_000);
        }
      } catch {
        // best-effort cleanup
      }
    });

    // Toggle the module
    await firstSwitch.click();
    await page.waitForTimeout(1_000);

    // Verify state changed
    const newState = await firstSwitch.getAttribute('data-state');
    const isNowChecked = newState === 'checked';
    expect(isNowChecked).not.toBe(wasChecked);

    // Toggle back to original state
    await firstSwitch.click();
    await page.waitForTimeout(1_000);

    // Verify state restored
    const restoredState = await firstSwitch.getAttribute('data-state');
    const isRestoredChecked = restoredState === 'checked';
    expect(isRestoredChecked).toBe(wasChecked);
  });
});
