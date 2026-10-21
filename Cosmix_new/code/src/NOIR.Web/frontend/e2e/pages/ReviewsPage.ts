import { type Page, type Locator, expect } from '@playwright/test';

export class ReviewsPage {
  readonly reviewTable: Locator;
  readonly reviewRows: Locator;
  readonly searchInput: Locator;

  constructor(private page: Page) {
    this.reviewTable = page.getByRole('table');
    this.reviewRows = page.getByRole('table').getByRole('row');
    this.searchInput = page.getByPlaceholder(/search/i);
  }

  async goto() {
    await this.page.goto('/portal/ecommerce/reviews');
    await this.page.waitForLoadState('networkidle');
  }

  async expectReviewInList(text: string) {
    await expect(this.page.getByRole('row', { name: new RegExp(text, 'i') })).toBeVisible();
  }

  async filterByStatus(status: string) {
    const statusFilter = this.page.getByRole('combobox', { name: /status/i })
      .or(this.page.getByLabel(/status/i));
    if (await statusFilter.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await statusFilter.click();
      await this.page.getByRole('option', { name: new RegExp(status, 'i') }).click();
    }
  }
}
