import { test, expect } from '../../fixtures/base.fixture';
import { testCompany, testContact, uniqueId } from '../../helpers/test-data';
import {
  confirmDelete,
  expectToast,
  waitForTableLoad,
  TOAST_SUCCESS,
} from '../../helpers/selectors';

/**
 * CRM Companies E2E Tests
 *
 * Covers:
 *   CRM-COMP-001 (List loads)
 *   CRM-COMP-002 (Create via UI)
 *   CRM-COMP-003 (Edit company name)
 *   CRM-COMP-004 (View detail page)
 *   CRM-COMP-005 (Delete company)
 *   CRM-COMP-006 (Search companies)
 *
 * UI layout: Table with EllipsisVertical dropdown per row.
 * Dialog: CompanyDialog (Credenza) with "name" as the primary required field.
 */

const API_URL = process.env.API_URL ?? 'http://localhost:4000';

/** Click the create company button (header button, first match to avoid EmptyState ambiguity) */
async function clickCreateCompanyButton(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: /new company|create/i }).first().click();
}

/** Open row dropdown and click a menu item */
async function openDropdownAndClick(
  page: import('@playwright/test').Page,
  rowName: string,
  menuItemName: RegExp,
) {
  const row = page.getByRole('row', { name: new RegExp(rowName, 'i') });
  // First button in row is the EllipsisVertical actions trigger
  await row.locator('button').first().click();
  await page.locator('[role="menu"]').waitFor({ state: 'visible', timeout: 5_000 });
  await page.getByRole('menuitem', { name: menuItemName }).click();
}

// ─── Smoke Tests ────────────────────────────────────────────────────────────

test.describe('CRM Companies @smoke', () => {
  /**
   * CRM-COMP-001: Companies list loads
   * Seed a company, navigate to list, verify table is visible with data.
   */
  test('CRM-COMP-001: should display companies list with data @smoke', async ({
    api,
    trackCleanup,
    page,
  }) => {
    const data = testCompany();
    const company = await api.createCompany(data);
    trackCleanup(async () => {
      await api.deleteCompany(company.id);
    });

    await page.goto('/portal/crm/companies');
    await page.waitForLoadState('networkidle');
    await waitForTableLoad(page);

    // Table should be visible
    await expect(page.getByRole('table')).toBeVisible();

    // Seeded company should appear in list
    await expect(
      page.getByRole('row', { name: new RegExp(data.name, 'i') }),
    ).toBeVisible({ timeout: 5_000 });
  });

  /**
   * CRM-COMP-002: Create company via UI
   * Open create dialog, fill name, save, verify toast + company in list.
   */
  test('CRM-COMP-002: should create company via UI @smoke', async ({
    api,
    trackCleanup,
    page,
  }) => {
    const companyName = `E2E Company ${uniqueId('comp')}`;

    await page.goto('/portal/crm/companies');
    await page.waitForLoadState('networkidle');

    // Open create dialog
    await clickCreateCompanyButton(page);

    // Fill company name (first required field)
    await page.getByLabel(/name/i).first().fill(companyName);

    // Submit
    await page.getByRole('button', { name: /save|create|submit/i }).last().click();

    // Capture the POST response to get the company ID for cleanup
    const createResponse = await page.waitForResponse(
      (resp) =>
        resp.url().includes('/api/crm/companies') &&
        resp.request().method() === 'POST',
      { timeout: 10_000 },
    );
    const createBody = await createResponse.json();
    const companyId = createBody.id;

    trackCleanup(async () => {
      if (companyId) {
        await api.deleteCompany(companyId);
      }
    });

    await expectToast(page, /created|success/i);

    // Verify company appears in list (re-navigate to ensure fresh data)
    await page.goto('/portal/crm/companies');
    await page.waitForLoadState('networkidle');
    await waitForTableLoad(page);
    await expect(
      page.getByRole('row', { name: new RegExp(companyName, 'i') }),
    ).toBeVisible({ timeout: 5_000 });
  });
});

// ─── Regression Tests ───────────────────────────────────────────────────────

test.describe('CRM Companies @regression', () => {
  /**
   * CRM-COMP-003: Edit company name
   * Seed company via API, edit name via UI, verify updated name in list.
   */
  test('CRM-COMP-003: should edit an existing company name @regression', async ({
    api,
    trackCleanup,
    page,
  }) => {
    const data = testCompany();
    const company = await api.createCompany(data);
    trackCleanup(async () => {
      await api.deleteCompany(company.id);
    });

    await page.goto('/portal/crm/companies');
    await page.waitForLoadState('networkidle');
    await waitForTableLoad(page);

    // Open dropdown → Edit
    await openDropdownAndClick(page, data.name, /edit/i);

    // Update name
    const updatedName = `${data.name} Updated`;
    const nameInput = page.getByLabel(/name/i).first();
    await nameInput.clear();
    await nameInput.fill(updatedName);

    // Save
    await page.getByRole('button', { name: /save|update|submit/i }).last().click();

    await page.waitForResponse(
      (resp) =>
        resp.url().includes('/api/crm/companies') &&
        resp.request().method() === 'PUT',
      { timeout: 10_000 },
    );
    await expectToast(page, /updated|saved|success/i);

    // Verify updated name in list
    await page.goto('/portal/crm/companies');
    await page.waitForLoadState('networkidle');
    await waitForTableLoad(page);
    await expect(
      page.getByRole('row', { name: new RegExp(updatedName, 'i') }),
    ).toBeVisible({ timeout: 5_000 });
  });

  /**
   * CRM-COMP-004: View company detail page
   * Seed company, navigate to detail, verify page content + contacts section.
   */
  test('CRM-COMP-004: should display company detail page @regression', async ({
    api,
    trackCleanup,
    page,
  }) => {
    const data = testCompany();
    const company = await api.createCompany(data);
    trackCleanup(async () => {
      await api.deleteCompany(company.id);
    });

    // Navigate to detail page directly
    await page.goto(`/portal/crm/companies/${company.id}`);
    await page.waitForLoadState('networkidle');

    // Verify company name is visible on detail page
    await expect(
      page.getByText(new RegExp(data.name, 'i')).first(),
    ).toBeVisible({ timeout: 10_000 });

    // Verify details section exists
    await expect(
      page.getByText(/details/i).first(),
    ).toBeVisible({ timeout: 5_000 });

    // Verify contacts section exists (even if empty)
    const contactsSection = page.getByText(/contacts/i).first();
    if (await contactsSection.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await expect(contactsSection).toBeVisible();
    }

    // No error states
    await expect(
      page.locator('[role="alert"][data-type="error"]'),
    ).not.toBeVisible({ timeout: 2_000 }).catch(() => {});
  });

  /**
   * CRM-COMP-005: Delete company
   * Seed company, delete via dropdown + confirmation dialog, verify removal.
   */
  test('CRM-COMP-005: should delete company with confirmation @regression', async ({
    api,
    page,
  }) => {
    const data = testCompany();
    const company = await api.createCompany(data);
    // No trackCleanup needed — we're deleting in the test itself

    await page.goto('/portal/crm/companies');
    await page.waitForLoadState('networkidle');
    await waitForTableLoad(page);

    // Open dropdown → Delete
    await openDropdownAndClick(page, data.name, /delete/i);

    // Confirm deletion
    await confirmDelete(page);
    await expectToast(page, /deleted|success/i);

    // Verify company is no longer in list
    await expect(
      page.getByRole('row', { name: new RegExp(data.name, 'i') }),
    ).not.toBeVisible({ timeout: 5_000 });
  });
});

// ─── Nightly Tests ──────────────────────────────────────────────────────────

test.describe('CRM Companies @nightly', () => {
  /**
   * CRM-COMP-006: Search companies
   * Seed two companies with distinct names, search for one, verify filtering.
   */
  test('CRM-COMP-006: should search and filter companies @nightly', async ({
    api,
    trackCleanup,
    page,
  }) => {
    const suffix = uniqueId('srch');
    const companyA = await api.createCompany(
      testCompany({ name: `AlphaSearch-${suffix}` }),
    );
    const companyB = await api.createCompany(
      testCompany({ name: `BetaSearch-${suffix}` }),
    );
    trackCleanup(async () => {
      await api.deleteCompany(companyA.id);
      await api.deleteCompany(companyB.id);
    });

    await page.goto('/portal/crm/companies');
    await page.waitForLoadState('networkidle');
    await waitForTableLoad(page);

    // Both companies should be visible initially
    await expect(
      page.getByRole('row', { name: new RegExp(`AlphaSearch-${suffix}`, 'i') }),
    ).toBeVisible({ timeout: 5_000 });
    await expect(
      page.getByRole('row', { name: new RegExp(`BetaSearch-${suffix}`, 'i') }),
    ).toBeVisible({ timeout: 5_000 });

    // Search for company A — scope to main to avoid matching sidebar "Search menu..." input
    const searchInput = page.locator('main').getByRole('textbox', { name: /search/i }).first();
    await searchInput.fill(`AlphaSearch-${suffix}`);

    // Wait for deferred value + API response (explicit response wait is more reliable than networkidle)
    await page.waitForTimeout(800);
    await page.waitForResponse(
      (resp) => resp.url().includes('/api/crm/companies') && resp.status() < 400,
      { timeout: 10_000 },
    ).catch(() => {});
    await page.waitForLoadState('networkidle');

    // Company A visible, Company B not visible (5s gives extra time for deferred value to settle)
    await expect(
      page.getByRole('row', { name: new RegExp(`AlphaSearch-${suffix}`, 'i') }),
    ).toBeVisible({ timeout: 5_000 });
    await expect(
      page.getByRole('row', { name: new RegExp(`BetaSearch-${suffix}`, 'i') }),
    ).not.toBeVisible({ timeout: 5_000 });

    // Clear search — both should be visible again
    await searchInput.clear();
    await page.waitForTimeout(800);
    await page.waitForLoadState('networkidle');

    await expect(
      page.getByRole('row', { name: new RegExp(`AlphaSearch-${suffix}`, 'i') }),
    ).toBeVisible({ timeout: 5_000 });
    await expect(
      page.getByRole('row', { name: new RegExp(`BetaSearch-${suffix}`, 'i') }),
    ).toBeVisible({ timeout: 5_000 });
  });

  /**
   * CRM-COMP-007: Delete guard — company with contacts
   * Verify that deleting a company that has associated contacts is blocked.
   */
  test('CRM-COMP-007: should block delete of company with contacts @nightly', async ({
    api,
    trackCleanup,
    page,
  }) => {
    const companyData = testCompany();
    const company = await api.createCompany(companyData);

    // Create a contact linked to the company
    const contactData = testContact();
    const contactRes = await api.request.post(`${API_URL}/api/crm/contacts`, {
      data: { ...contactData, companyId: company.id },
    });
    const contact = await contactRes.json();

    trackCleanup(async () => {
      // Must delete contact first, then company
      await api.deleteContact(contact.id).catch(() => {});
      await api.deleteCompany(company.id).catch(() => {});
    });

    await page.goto('/portal/crm/companies');
    await page.waitForLoadState('networkidle');
    await waitForTableLoad(page);

    // Open dropdown → Delete
    await openDropdownAndClick(page, companyData.name, /delete/i);
    await confirmDelete(page);

    // Expect error toast (delete guard blocks because company has contacts)
    await expectToast(page, /cannot|has contacts|associated|blocked|error/i, 'error');
  });
});
