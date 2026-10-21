import { type Page, type Locator, expect } from '@playwright/test';

export class OrderDetailPage {
  readonly orderStatus: Locator;
  readonly timeline: Locator;
  readonly items: Locator;
  readonly customerInfo: Locator;
  readonly actionButtons: Locator;

  constructor(private page: Page) {
    this.orderStatus = page.getByTestId('order-status').or(
      page.locator('[data-testid="order-status"], [class*="badge"]').first(),
    );
    this.timeline = page.getByTestId('order-timeline').or(
      page.getByRole('list', { name: /timeline|history/i }),
    );
    this.items = page.getByRole('table').or(
      page.getByTestId('order-items'),
    );
    this.customerInfo = page.getByTestId('customer-info').or(
      page.getByText(/customer/i).locator('..'),
    );
    this.actionButtons = page.getByRole('group').or(
      page.locator('[data-testid="order-actions"]'),
    );
  }

  async goto(orderId: string) {
    await this.page.goto(`/portal/ecommerce/orders/${orderId}`);
    await this.page.waitForLoadState('networkidle');
  }

  async confirmOrder() {
    await this.page.getByRole('button', { name: /confirm/i }).click();
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/orders') && resp.request().method() === 'PUT',
    );
  }

  async processOrder() {
    await this.page.getByRole('button', { name: /process/i }).click();
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/orders') && resp.request().method() === 'PUT',
    );
  }

  async shipOrder() {
    await this.page.getByRole('button', { name: /ship/i }).click();
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/orders') && resp.request().method() === 'PUT',
    );
  }

  async deliverOrder() {
    await this.page.getByRole('button', { name: /deliver/i }).click();
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/orders') && resp.request().method() === 'PUT',
    );
  }

  async completeOrder() {
    await this.page.getByRole('button', { name: /complete/i }).click();
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/orders') && resp.request().method() === 'PUT',
    );
  }

  async cancelOrder(reason: string) {
    await this.page.getByRole('button', { name: /cancel/i }).click();
    // Fill reason in the confirmation dialog
    const reasonInput = this.page.getByLabel(/reason/i).or(
      this.page.getByPlaceholder(/reason/i),
    );
    await reasonInput.fill(reason);
    await this.page.getByRole('button', { name: /confirm|yes|ok/i }).click();
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/orders') && resp.request().method() === 'PUT',
    );
  }

  async expectStatus(status: string) {
    await expect(this.page.getByText(new RegExp(status, 'i')).first()).toBeVisible();
  }
}
