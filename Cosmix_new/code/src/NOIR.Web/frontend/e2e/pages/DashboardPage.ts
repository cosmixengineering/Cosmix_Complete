import { type Page, type Locator, expect } from '@playwright/test';

export class DashboardPage {
  readonly heading: Locator;
  readonly sidebar: Locator;

  constructor(private page: Page) {
    this.heading = page.getByRole('heading', { level: 1 });
    this.sidebar = page.locator('[data-testid="sidebar"], aside nav, [role="navigation"]');
  }

  async goto() {
    await this.page.goto('/portal');
  }

  async expectLoaded() {
    await expect(this.page).toHaveURL(/portal/);
    await this.page.waitForLoadState('networkidle');
    await expect(this.sidebar.first()).toBeVisible();
  }

  async navigateViaSidebar(menuText: string | RegExp) {
    await this.sidebar.getByRole('link', { name: menuText }).click();
  }

  async expectWidgetVisible(widgetText: string | RegExp) {
    await expect(this.page.getByText(widgetText)).toBeVisible();
  }

  async useGlobalSearch(query: string) {
    await this.page.keyboard.press('Meta+k');
    await this.page.getByPlaceholder(/search|tìm/i).fill(query);
  }
}
