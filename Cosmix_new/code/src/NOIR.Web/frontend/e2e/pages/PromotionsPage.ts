import { type Page, type Locator, expect } from '@playwright/test';

export class PromotionsPage {
  readonly createButton: Locator;
  readonly promotionTable: Locator;
  readonly promotionRows: Locator;
  readonly searchInput: Locator;

  constructor(private page: Page) {
    this.createButton = page.getByRole('button', { name: /create|add|new/i });
    this.promotionTable = page.getByRole('table');
    this.promotionRows = page.getByRole('table').getByRole('row');
    this.searchInput = page.getByPlaceholder(/search/i);
  }

  async goto() {
    await this.page.goto('/portal/marketing/promotions');
    await this.page.waitForLoadState('networkidle');
  }

  async gotoDetail(promotionId: string) {
    await this.page.goto(`/portal/marketing/promotions/${promotionId}`);
    await this.page.waitForLoadState('networkidle');
  }

  async expectPromotionInList(name: string) {
    await expect(this.page.getByRole('row', { name: new RegExp(name, 'i') })).toBeVisible();
  }
}
