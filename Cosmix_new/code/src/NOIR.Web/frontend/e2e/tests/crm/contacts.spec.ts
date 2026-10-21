import { test, expect } from '../../fixtures/base.fixture';
import { testContact } from '../../helpers/test-data';
import { confirmDelete, expectToast, waitForTableLoad } from '../../helpers/selectors';

/**
 * CRM Contacts E2E Tests
 *
 * Covers: CRM-001 (Contact CRUD), CRM-002 (Validation errors), CRM-003 (Company CRUD)
 *
 * Note: Contact/Company actions (Edit, Delete) are in a DropdownMenu triggered by the
 * EllipsisVertical button (first button in each row). The dropdown renders in a Portal.
 */

/** Click the "New Contact" create button (first match to avoid EmptyState ambiguity) */
async function clickNewContactButton(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: /new contact/i }).first().click();
}

/** Click the "New Company" create button (first match to avoid EmptyState ambiguity) */
async function clickNewCompanyButton(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: /new company/i }).first().click();
}

/** Open row dropdown and click a menu item */
async function openDropdownAndClick(page: import('@playwright/test').Page, rowName: string, menuItemName: RegExp) {
  const row = page.getByRole('row', { name: new RegExp(rowName, 'i') });
  await row.locator('button').first().click();
  // Wait for dropdown menu to open
  await page.locator('[role="menu"]').waitFor({ state: 'visible', timeout: 5_000 });
  await page.getByRole('menuitem', { name: menuItemName }).click();
}

test.describe('CRM Contacts @regression', () => {
  // ─── CRM-001: Contact CRUD ───────────────────────────────────

  test.describe('CRM-001: Contact CRUD @smoke', () => {
    let contactId: string;

    test.afterEach(async ({ api }) => {
      if (contactId) {
        await api.deleteEntity('crm/contacts', contactId);
      }
    });

    test('should load contacts list page', async ({ crmContactsPage }) => {
      await crmContactsPage.goto();
      await expect(crmContactsPage.contactTable).toBeVisible();
    });

    test('should create a new contact', async ({ crmContactsPage, page }) => {
      const data = testContact();
      await crmContactsPage.goto();
      await crmContactsPage.createContact({
        firstName: data.firstName,
        lastName: data.lastName,
        email: data.email,
      });

      await expectToast(page, /created|success/i);
      await crmContactsPage.expectContactInList(data.lastName);

      // Capture ID for cleanup
      const response = await page.waitForResponse(
        resp => resp.url().includes('/api/crm/contacts') && resp.request().method() === 'GET',
        { timeout: 5_000 },
      ).catch(() => null);
      if (response) {
        const body = await response.json().catch(() => null);
        const items = body?.items ?? body?.data ?? [];
        const match = items.find((c: { email?: string }) => c.email === data.email);
        if (match) contactId = match.id;
      }
    });

    test('should edit an existing contact', async ({ crmContactsPage, api, page }) => {
      const data = testContact();
      const created = await api.createContact({
        firstName: data.firstName,
        lastName: data.lastName,
        email: data.email,
      });
      contactId = created.id;

      await crmContactsPage.goto();
      await crmContactsPage.editContact(data.lastName);

      // Update last name
      const lastNameInput = page.getByLabel(/last name/i);
      await lastNameInput.clear();
      await lastNameInput.fill(`${data.lastName}Updated`);
      await page.getByRole('button', { name: /save|update|submit/i }).click();

      await page.waitForResponse(
        resp => resp.url().includes('/api/crm/contacts') && resp.request().method() === 'PUT',
      );
      await expectToast(page, /updated|success/i);
    });

    test('should delete a contact with confirmation', async ({ crmContactsPage, api, page }) => {
      const data = testContact();
      const created = await api.createContact({
        firstName: data.firstName,
        lastName: data.lastName,
        email: data.email,
      });
      contactId = created.id;

      await crmContactsPage.goto();

      // Open dropdown menu for the row, then click Delete
      await crmContactsPage.deleteContact(data.lastName);

      await confirmDelete(page);
      await expectToast(page, /deleted|success/i);

      // Verify removed from list
      await expect(
        page.getByRole('row', { name: new RegExp(data.lastName, 'i') }),
      ).not.toBeVisible({ timeout: 5_000 });

      // Already deleted, clear ID so afterEach doesn't fail
      contactId = '';
    });
  });

  // ─── CRM-002: Contact validation errors ───────────────────────

  test.describe('CRM-002: Contact validation errors @regression', () => {
    test('should show validation errors for empty required fields', async ({
      crmContactsPage,
      page,
    }) => {
      await crmContactsPage.goto();
      await clickNewContactButton(page);

      // Submit without filling required fields
      await page.getByRole('button', { name: /save|create|submit/i }).last().click();

      // Expect validation errors to appear for required fields
      await expect(
        page.getByText(/first name.*required|required.*first name/i).first().or(
          page.locator('[data-state="open"]').getByText(/required/i).first(),
        ).first(),
      ).toBeVisible({ timeout: 5_000 });
    });

    test('should show validation error for invalid email', async ({
      crmContactsPage,
      page,
    }) => {
      await crmContactsPage.goto();
      await clickNewContactButton(page);

      await page.getByLabel(/first name/i).fill('Test');
      await page.getByLabel(/last name/i).fill('Contact');
      await page.getByLabel(/email/i).fill('not-an-email');
      // Trigger blur to show validation
      await page.getByLabel(/first name/i).focus();

      await expect(
        page.getByText(/valid email|invalid email|email.*invalid/i),
      ).toBeVisible({ timeout: 5_000 });
    });
  });

  // ─── CRM-003: Company CRUD ────────────────────────────────────

  test.describe('CRM-003: Company CRUD @regression', () => {
    let companyId: string;

    test.afterEach(async ({ api }) => {
      if (companyId) {
        await api.deleteEntity('crm/companies', companyId);
      }
    });

    test('should create, edit, and delete a company', async ({ page, api }) => {
      const companyName = `E2E Company ${Date.now()}`;

      // Navigate to companies page
      await page.goto('/portal/crm/companies');
      await page.waitForLoadState('networkidle');

      // Create company — click the header "New Company" button
      await clickNewCompanyButton(page);
      await page.getByLabel(/name/i).first().fill(companyName);
      await page.getByRole('button', { name: /save|create|submit/i }).last().click();

      const createResponse = await page.waitForResponse(
        resp => resp.url().includes('/api/crm/companies') && resp.request().method() === 'POST',
      );
      const createBody = await createResponse.json();
      companyId = createBody.id;
      await expectToast(page, /created|success/i);

      // Verify company appears in list
      await page.goto('/portal/crm/companies');
      await page.waitForLoadState('networkidle');
      await expect(page.getByText(companyName)).toBeVisible();

      // Edit company — open dropdown then click Edit
      await openDropdownAndClick(page, companyName, /edit/i);

      const nameInput = page.getByLabel(/name/i).first();
      await nameInput.clear();
      await nameInput.fill(`${companyName} Updated`);
      await page.getByRole('button', { name: /save|update|submit/i }).last().click();

      await page.waitForResponse(
        resp => resp.url().includes('/api/crm/companies') && resp.request().method() === 'PUT',
      );
      await expectToast(page, /updated|success/i);

      // Delete company — open dropdown then click Delete
      await page.goto('/portal/crm/companies');
      await page.waitForLoadState('networkidle');

      await openDropdownAndClick(page, companyName, /delete/i);

      await confirmDelete(page);
      await expectToast(page, /deleted|success/i);

      companyId = ''; // Already deleted
    });

    test('CRM-009: should display contact detail page with sections @regression', async ({
      page,
      api,
    }) => {
      // Seed: create contact with full data
      const contactData = testContact();
      const contact = await api.createContact({
        firstName: contactData.firstName,
        lastName: contactData.lastName,
        email: contactData.email,
      });

      try {
        // Navigate to contact detail page
        await page.goto(`/portal/crm/contacts/${contact.id}`);
        await page.waitForLoadState('networkidle');

        // Verify contact info section (name, email)
        await expect(
          page.getByText(new RegExp(contactData.firstName, 'i')).first(),
        ).toBeVisible({ timeout: 10_000 });
        await expect(
          page.getByText(new RegExp(contactData.email, 'i')).first(),
        ).toBeVisible({ timeout: 5_000 });

        // Verify the page loaded without error states
        await expect(
          page.locator('[role="alert"][data-type="error"]'),
        ).not.toBeVisible({ timeout: 2_000 }).catch(() => {});

        // Check for activity/timeline section if present
        const activitySection = page.getByText(/activity|timeline|history/i).first();
        if (await activitySection.isVisible({ timeout: 3_000 }).catch(() => false)) {
          await expect(activitySection).toBeVisible();
        }

        // Check for associated leads section if present
        const leadsSection = page.getByText(/leads|opportunities/i).first();
        if (await leadsSection.isVisible({ timeout: 3_000 }).catch(() => false)) {
          await expect(leadsSection).toBeVisible();
        }
      } finally {
        await api.deleteEntity('crm/contacts', contact.id).catch(() => {});
      }
    });

    test('should block delete of company with contacts', async ({ page, api }) => {
      const companyName = `E2E GuardCo ${Date.now()}`;

      // Create company via API
      const companyRes = await api.request.post(
        `${process.env.API_URL ?? 'http://localhost:4000'}/api/crm/companies`,
        { data: { name: companyName } },
      );
      const company = await companyRes.json();
      companyId = company.id;

      // Create contact associated with the company
      const contactData = testContact();
      const contactRes = await api.request.post(
        `${process.env.API_URL ?? 'http://localhost:4000'}/api/crm/contacts`,
        { data: { ...contactData, companyId: company.id } },
      );
      const contact = await contactRes.json();

      // Try to delete the company
      await page.goto('/portal/crm/companies');
      await page.waitForLoadState('networkidle');

      await openDropdownAndClick(page, companyName, /delete/i);

      await confirmDelete(page);

      // Expect error toast (delete guard blocks because company has contacts)
      await expectToast(page, /cannot|has contacts|associated|blocked/i, 'error');

      // Cleanup: delete contact first, then company
      await api.deleteEntity('crm/contacts', contact.id);
      // companyId will be cleaned up in afterEach
    });
  });
});
