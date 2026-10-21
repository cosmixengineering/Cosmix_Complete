import { type Page, type Locator, expect } from '@playwright/test';

export class OrdersPage {
  readonly orderTable: Locator;
  readonly statusFilter: Locator;
  readonly orderRows: Locator;
  readonly searchInput: Locator;

  constructor(private page: Page) {
    this.orderTable = page.getByRole('table');
    this.orderRows = page.getByRole('table').getByRole('row');
    this.statusFilter = page.getByRole('combobox', { name: /status/i }).or(
      page.getByRole('button', { name: /status|filter/i }),
    );
    this.searchInput = page.getByPlaceholder(/search/i);
  }

  async goto() {
    await this.page.goto('/portal/ecommerce/orders');
    await this.page.waitForLoadState('networkidle');
  }

  async clickOrder(id: string) {
    await this.page.getByRole('row', { name: new RegExp(id, 'i') })
      .getByRole('link').first()
      .click();
    await this.page.waitForURL(`**/orders/${id}`);
  }

  async filterByStatus(status: string) {
    await this.statusFilter.click();
    await this.page.getByRole('option', { name: new RegExp(status, 'i') }).click();
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/orders') && resp.status() === 200,
    );
  }

  async expectOrderInList(id: string) {
    await expect(this.page.getByRole('row', { name: new RegExp(id, 'i') })).toBeVisible();
  }

  async getOrderCount(): Promise<number> {
    const rows = await this.orderRows.count();
    return Math.max(0, rows - 1); // subtract header row
  }
}
