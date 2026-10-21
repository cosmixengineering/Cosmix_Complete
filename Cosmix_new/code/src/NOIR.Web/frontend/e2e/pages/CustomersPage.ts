import { type Page, type Locator, expect } from '@playwright/test';

export class CustomersPage {
  readonly createButton: Locator;
  readonly customerTable: Locator;
  readonly customerRows: Locator;
  readonly searchInput: Locator;

  constructor(private page: Page) {
    this.createButton = page.getByRole('button', { name: /create|add|new/i });
    this.customerTable = page.getByRole('table');
    this.customerRows = page.getByRole('table').getByRole('row');
    // Use aria-label to avoid strict mode violation from sidebar "Search menu..." input
    this.searchInput = page.getByRole('textbox', { name: /search customer/i })
      .or(page.getByPlaceholder(/search customer/i));
  }

  async goto() {
    await this.page.goto('/portal/ecommerce/customers');
    await this.page.waitForLoadState('networkidle');
  }

  async gotoDetail(customerId: string) {
    await this.page.goto(`/portal/ecommerce/customers/${customerId}`);
    await this.page.waitForLoadState('networkidle');
  }

  async createCustomer(data: {
    firstName: string;
    lastName: string;
    email: string;
    phone?: string;
  }) {
    await this.createButton.click();
    await this.page.getByLabel(/first name/i).fill(data.firstName);
    await this.page.getByLabel(/last name/i).fill(data.lastName);
    await this.page.getByLabel(/email/i).fill(data.email);
    if (data.phone) {
      await this.page.getByLabel(/phone/i).fill(data.phone);
    }
    await this.page.getByRole('button', { name: /save|create|submit/i }).click();
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/customers') && resp.request().method() === 'POST',
    );
  }

  async searchCustomer(query: string) {
    await this.searchInput.fill(query);
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/customers') && resp.status() === 200,
    );
  }

  async expectCustomerInList(name: string) {
    await expect(this.page.getByRole('row', { name: new RegExp(name, 'i') })).toBeVisible();
  }
}
