import { type Page, type Locator, expect } from '@playwright/test';

export class CrmContactsPage {
  readonly createButton: Locator;
  readonly contactTable: Locator;
  readonly searchInput: Locator;
  readonly contactRows: Locator;

  constructor(private page: Page) {
    // Use .first() to handle potential strict-mode violations when EmptyState also shows a "New Contact" button
    this.createButton = page.getByRole('button', { name: /create contact|new contact|add contact/i }).first().or(
      page.getByRole('button', { name: /new contact/i }).first(),
    );
    this.contactTable = page.getByRole('table');
    this.searchInput = page.getByPlaceholder(/search/i);
    this.contactRows = page.getByRole('table').getByRole('row');
  }

  async goto() {
    await this.page.goto('/portal/crm/contacts');
    await this.page.waitForLoadState('networkidle');
  }

  async clickCreateButton() {
    // Click the "New Contact" button — use .first() to avoid strict mode if EmptyState shows the same button
    await this.page.getByRole('button', { name: /new contact/i }).first().click();
  }

  async createContact(data: {
    firstName: string;
    lastName: string;
    email: string;
    phone?: string;
  }) {
    await this.clickCreateButton();
    await this.page.getByLabel(/first name/i).fill(data.firstName);
    await this.page.getByLabel(/last name/i).fill(data.lastName);
    await this.page.getByLabel(/email/i).fill(data.email);
    if (data.phone) {
      await this.page.getByLabel(/phone/i).fill(data.phone);
    }
    await this.page.getByRole('button', { name: /save|create|submit/i }).last().click();
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/crm/contacts') && resp.request().method() === 'POST',
    );
  }

  async openRowDropdown(name: string) {
    // Click the EllipsisVertical trigger button in the row (first button in row = dropdown trigger)
    const row = this.page.getByRole('row', { name: new RegExp(name, 'i') });
    await row.locator('button').first().click();
    // Wait for dropdown to open
    await this.page.locator('[data-state="open"][role="menu"], [role="menu"]').waitFor({ state: 'visible', timeout: 5_000 });
  }

  async editContact(name: string) {
    await this.openRowDropdown(name);
    // Click the Edit menu item (Radix DropdownMenuItem has role="menuitem")
    await this.page.getByRole('menuitem', { name: /edit/i }).click();
  }

  async deleteContact(name: string) {
    await this.openRowDropdown(name);
    await this.page.getByRole('menuitem', { name: /delete/i }).click();
  }

  async searchContact(query: string) {
    await this.searchInput.fill(query);
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/crm/contacts') && resp.status() === 200,
    );
  }

  async expectContactInList(name: string) {
    await expect(this.page.getByRole('row', { name: new RegExp(name, 'i') })).toBeVisible();
  }
}
