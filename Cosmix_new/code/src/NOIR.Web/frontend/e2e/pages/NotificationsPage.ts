import { type Page, type Locator, expect } from '@playwright/test';

export class NotificationsPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/portal/notifications');
    await this.page.waitForLoadState('networkidle');
  }

  async gotoSettings() {
    await this.page.goto('/portal/settings/notifications');
    await this.page.waitForLoadState('networkidle');
  }
}
