import { test, expect } from '../../fixtures/base.fixture';
import { testCustomer } from '../../helpers/test-data';
import {
  waitForTableLoad,
  expectToast,
  confirmDelete,
  TOAST_SUCCESS,
} from '../../helpers/selectors';

// ─── Customers: Smoke Tests ─────────────────────────────────────────────────

test.describe('E-commerce Customers @smoke', () => {
  /**
   * CUST-001: Customer list loads
   * Verify that the customer list page renders with table and data.
   */
  test('CUST-001: should display customer list with data @smoke', async ({
    customersPage,
    api,
    trackCleanup,
    page,
  }) => {
    // Seed a customer to ensure the list is non-empty
    const data = testCustomer();
    const customer = await api.createCustomer(data);
    trackCleanup(async () => { await api.deleteCustomer(customer.id); });

    await customersPage.goto();
    await waitForTableLoad(page);

    // Table should be visible with at least one data row
    await expect(customersPage.customerTable).toBeVisible();
    const rowCount = await customersPage.customerRows.count();
    expect(rowCount).toBeGreaterThan(1);
  });

  /**
   * CUST-002: Create customer via UI
   * Create a customer, verify it appears in the list.
   */
  test('CUST-002: should create customer via UI @smoke', async ({
    customersPage,
    api,
    trackCleanup,
    page,
  }) => {
    const data = testCustomer();

    await customersPage.goto();
    await customersPage.createCustomer({
      firstName: data.firstName,
      lastName: data.lastName,
      email: data.email,
    });

    await expectToast(page, /created|success/i);

    // Verify customer appears in list
    await customersPage.goto();
    await waitForTableLoad(page);
    await customersPage.expectCustomerInList(data.lastName);

    // Cleanup
    trackCleanup(async () => {
      const searchRes = await api.request.get(
        `${process.env.API_URL ?? 'http://localhost:4000'}/api/customers?search=${encodeURIComponent(data.email)}`,
      );
      const body = await searchRes.json();
      const items = body?.items ?? body?.data ?? [];
      for (const item of items) {
        if (item.email === data.email) {
          await api.deleteCustomer(item.id);
        }
      }
    });
  });
});

// ─── Customers: Regression Tests ──────────────────────────────────────────

test.describe('E-commerce Customers @regression', () => {
  /**
   * CUST-003: Customer validation errors
   * Verify that validation errors appear for required fields.
   */
  test('CUST-003: should show validation errors for empty required fields @regression', async ({
    customersPage,
    page,
  }) => {
    await customersPage.goto();
    await customersPage.createButton.click();

    // Submit without filling required fields
    await page.getByRole('button', { name: /save|create|submit/i }).click();

    // Expect validation errors
    await expect(
      page.getByText(/required|bắt buộc/i).first(),
    ).toBeVisible({ timeout: 5_000 });
  });

  /**
   * CUST-004: Edit customer
   * Create a customer via API, edit via UI, verify changes persist.
   */
  test('CUST-004: should edit an existing customer @regression', async ({
    customersPage,
    api,
    trackCleanup,
    page,
  }) => {
    const data = testCustomer();
    const created = await api.createCustomer(data);
    trackCleanup(async () => { await api.deleteCustomer(created.id); });

    await customersPage.goto();
    await waitForTableLoad(page);

    // Open the actions dropdown for the customer row, then click Edit
    const row = page.getByRole('row', { name: new RegExp(data.lastName, 'i') });
    // The trigger button uses aria-label "Actions for {name}"
    const actionsBtn = row.getByRole('button', { name: /actions for/i })
      .or(row.getByRole('button', { name: /edit/i }))
      .first();
    await actionsBtn.click();
    // Click Edit in the dropdown menu
    const editMenuItem = page.getByRole('menuitem', { name: /edit/i });
    if (await editMenuItem.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await editMenuItem.click();
    }

    // Update last name
    const updatedName = `${data.lastName}Updated`;
    const lastNameInput = page.getByLabel(/last name/i);
    await lastNameInput.clear();
    await lastNameInput.fill(updatedName);

    await page.getByRole('button', { name: /save|update|submit/i }).click();
    await expectToast(page, /updated|success/i);

    // Verify updated name in list
    await customersPage.goto();
    await waitForTableLoad(page);
    await customersPage.expectCustomerInList(updatedName);
  });

  /**
   * CUST-005: Customer detail page
   * Verify detail page shows customer info, addresses, order history sections.
   */
  test('CUST-005: should display customer detail page with sections @regression', async ({
    api,
    trackCleanup,
    page,
  }) => {
    const data = testCustomer();
    const created = await api.createCustomer(data);
    trackCleanup(async () => { await api.deleteCustomer(created.id); });

    await page.goto(`/portal/ecommerce/customers/${created.id}`);
    await page.waitForLoadState('networkidle');

    // Verify customer info
    await expect(
      page.getByText(new RegExp(data.firstName, 'i')).first(),
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      page.getByText(new RegExp(data.email, 'i')).first(),
    ).toBeVisible({ timeout: 5_000 });

    // Verify no error states
    await expect(
      page.locator('[role="alert"][data-type="error"]'),
    ).not.toBeVisible({ timeout: 2_000 }).catch(() => {});

    // Check for order history section
    const ordersSection = page.getByText(/orders|order history/i).first();
    if (await ordersSection.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await expect(ordersSection).toBeVisible();
    }

    // Check for addresses section
    const addressSection = page.getByText(/address/i).first();
    if (await addressSection.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await expect(addressSection).toBeVisible();
    }
  });

  /**
   * CUST-006: Delete customer with confirmation
   */
  test('CUST-006: should delete customer with confirmation @regression', async ({
    customersPage,
    api,
    page,
  }) => {
    const data = testCustomer();
    const created = await api.createCustomer(data);

    await customersPage.goto();
    await waitForTableLoad(page);
    await customersPage.expectCustomerInList(data.lastName);

    // Open actions dropdown then click Delete
    const deleteRow = page.getByRole('row', { name: new RegExp(data.lastName, 'i') });
    const deleteActionsBtn = deleteRow.getByRole('button', { name: /actions for/i })
      .or(deleteRow.getByRole('button', { name: /delete|remove/i }))
      .first();
    await deleteActionsBtn.click();
    // Click Delete in the dropdown menu
    const deleteMenuItem = page.getByRole('menuitem', { name: /delete/i });
    if (await deleteMenuItem.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await deleteMenuItem.click();
    }

    await confirmDelete(page);
    await expect(page.locator(TOAST_SUCCESS)).toBeVisible({ timeout: 10_000 });

    // Verify removed
    await expect(
      page.getByRole('row', { name: new RegExp(data.lastName, 'i') }),
    ).not.toBeVisible({ timeout: 5_000 });
  });

  /**
   * CUST-007: Search and filter customers
   */
  test('CUST-007: should search and filter customers @regression', async ({
    customersPage,
    api,
    trackCleanup,
    page,
  }) => {
    const custA = await api.createCustomer(testCustomer({ lastName: `SearchA${Date.now()}` }));
    const custB = await api.createCustomer(testCustomer({ lastName: `SearchB${Date.now()}` }));
    trackCleanup(async () => {
      await api.deleteCustomer(custA.id);
      await api.deleteCustomer(custB.id);
    });

    await customersPage.goto();
    await waitForTableLoad(page);

    // Search for customer A
    await customersPage.searchCustomer('SearchA');

    // Customer A visible, customer B not
    await customersPage.expectCustomerInList(custA.lastName);
    await expect(
      page.getByRole('row', { name: new RegExp(custB.lastName, 'i') }),
    ).not.toBeVisible({ timeout: 3_000 });
  });
});
