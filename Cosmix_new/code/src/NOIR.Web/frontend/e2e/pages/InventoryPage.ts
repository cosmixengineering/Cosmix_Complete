import { type Page, type Locator, expect } from '@playwright/test';

export class InventoryPage {
  readonly createButton: Locator;
  readonly receiptTable: Locator;
  readonly receiptRows: Locator;

  constructor(private page: Page) {
    this.createButton = page.getByRole('button', { name: /create|new|add/i });
    this.receiptTable = page.getByRole('table');
    this.receiptRows = page.getByRole('table').getByRole('row');
  }

  async goto() {
    await this.page.goto('/portal/ecommerce/inventory');
    await this.page.waitForLoadState('networkidle');
  }

  async gotoReceipt(receiptId: string) {
    await this.page.goto(`/portal/ecommerce/inventory/${receiptId}`);
    await this.page.waitForLoadState('networkidle');
  }

  async createReceipt(type: 'StockIn' | 'StockOut') {
    await this.createButton.click();
    // Select receipt type
    const typeSelect = this.page.getByRole('combobox', { name: /type/i });
    if (await typeSelect.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await typeSelect.click();
      await this.page.getByRole('option', { name: new RegExp(type, 'i') }).click();
    }
  }

  async addLineItem(productName: string, quantity: number) {
    await this.page.getByRole('button', { name: /add item|add line/i }).click();
    // Select product
    const productSelect = this.page.getByRole('combobox', { name: /product/i }).last();
    await productSelect.click();
    await this.page.getByRole('option', { name: new RegExp(productName, 'i') }).click();
    // Set quantity
    await this.page.getByLabel(/quantity/i).last().fill(String(quantity));
  }

  async saveAsDraft() {
    await this.page.getByRole('button', { name: /save|create/i }).click();
  }

  async confirmReceipt() {
    await this.page.getByRole('button', { name: /confirm/i }).click();
  }

  async cancelReceipt() {
    await this.page.getByRole('button', { name: /cancel/i }).click();
    // Confirm the cancellation dialog
    await this.page.getByRole('button', { name: /confirm|yes|ok/i }).click();
  }

  async expectReceiptStatus(status: string) {
    await expect(this.page.getByText(new RegExp(status, 'i')).first()).toBeVisible();
  }

  async expectReceiptInList(code: string) {
    await expect(this.page.getByRole('row', { name: new RegExp(code, 'i') })).toBeVisible();
  }
}
