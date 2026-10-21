import { type Page, type Locator, expect } from '@playwright/test';

export class CustomerGroupsPage {
  readonly createButton: Locator;
  readonly groupTable: Locator;
  readonly groupRows: Locator;

  constructor(private page: Page) {
    this.createButton = page.getByRole('button', { name: /create|add|new/i });
    this.groupTable = page.getByRole('table');
    this.groupRows = page.getByRole('table').getByRole('row');
  }

  async goto() {
    await this.page.goto('/portal/ecommerce/customer-groups');
    await this.page.waitForLoadState('networkidle');
  }

  async expectGroupInList(name: string) {
    await expect(this.page.getByRole('row', { name: new RegExp(name, 'i') })).toBeVisible();
  }
}
