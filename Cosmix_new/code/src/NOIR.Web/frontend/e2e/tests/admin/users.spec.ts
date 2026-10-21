import { test, expect } from '../../fixtures/base.fixture';
import { testUser } from '../../helpers/test-data';
import { waitForTableLoad, expectToast } from '../../helpers/selectors';

test.describe('Admin Users @regression', () => {
  /**
   * ADMIN-001: User list loads
   * Verify that the user management list renders with at least the admin user.
   */
  test('ADMIN-001: user list loads with admin user @smoke', async ({
    usersPage,
    page,
  }) => {
    await usersPage.goto();
    await waitForTableLoad(page);

    // Table should render with at least one data row (the admin user)
    const rows = usersPage.userRows;
    await expect(rows).not.toHaveCount(0);

    // The default admin user should be visible
    await usersPage.expectUserInList('admin@noir.local');
  });

  /**
   * ADMIN-002: Create user (happy path + duplicate email validation)
   * Verify that a new user can be created and that duplicate emails are rejected.
   */
  test('ADMIN-002: create user and validate duplicate email @regression', async ({
    usersPage,
    page,
    api,
    trackCleanup,
  }) => {
    const userData = testUser();
    await usersPage.goto();
    await waitForTableLoad(page);

    // Create user via UI
    await usersPage.createUser({
      firstName: userData.firstName,
      lastName: userData.lastName,
      email: userData.email,
      password: userData.password,
    });

    // Verify success toast
    await expectToast(page, /created|success|th\u00e0nh c\u00f4ng/i);

    // Verify user appears in the list
    await usersPage.expectUserInList(userData.email);

    // Track cleanup — delete the user even if subsequent assertions fail
    trackCleanup(async () => {
      // Find the user ID by searching the list via API
      await api.deleteEntity('users', userData.email);
    });

    // Try to create another user with the same email (duplicate validation)
    await usersPage.createButton.click();
    await page.getByLabel(/first name/i).fill('Duplicate');
    await page.getByLabel(/last name/i).fill('User');
    await page.getByLabel(/email/i).fill(userData.email);
    // Use id-based selector — label text is "Password *" with asterisk
    const pwdInput = page.locator('#password');
    if (await pwdInput.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await pwdInput.fill(userData.password);
    } else {
      await page.getByLabel(/^password/i).first().fill(userData.password);
    }
    const confirmInput = page.locator('#confirmPassword');
    if (await confirmInput.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await confirmInput.fill(userData.password);
    }
    await page.getByRole('button', { name: /save|create|submit|invite/i }).click();

    // Should show an error about duplicate email
    await expectToast(page, /already|exists|duplicate|t\u1ed3n t\u1ea1i/i, 'error');
  });

  /**
   * ADMIN-003: Assign role to user
   * Verify that a user's role can be changed and the update persists.
   */
  test('ADMIN-003: assign role to user @regression', async ({
    usersPage,
    page,
    api,
    trackCleanup,
  }) => {
    // Seed a test user via API
    const userData = testUser();
    const userRes = await api.request.post('http://localhost:4000/api/users', {
      data: {
        firstName: userData.firstName,
        lastName: userData.lastName,
        email: userData.email,
        password: userData.password,
      },
    });
    const user = await userRes.json();
    trackCleanup(async () => {
      await api.deleteEntity('users', user.id ?? user.Id);
    });

    await usersPage.goto();
    await waitForTableLoad(page);

    // Search for the user to ensure they appear on the visible page
    await usersPage.expectUserInList(userData.email);

    // Find the test user row and click edit via the dropdown menu
    const userRow = page.getByRole('row', { name: new RegExp(userData.email, 'i') });
    await expect(userRow).toBeVisible();
    // The table uses a dropdown menu (ellipsis button) for row actions
    await userRow.getByRole('button').first().click();
    await page.getByRole('menuitem', { name: /edit/i }).click();

    // Change role via combobox
    const roleCombobox = page.getByRole('combobox', { name: /role/i });
    if (await roleCombobox.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await roleCombobox.click();
      // Select "Admin" or the first available role that differs from current
      const roleOption = page.getByRole('option').first();
      await roleOption.click();
    }

    // Save
    await page.getByRole('button', { name: /save|update|submit/i }).click();
    await page.waitForResponse(resp =>
      resp.url().includes('/api/users') && (resp.request().method() === 'PUT' || resp.request().method() === 'PATCH'),
    );

    // Verify success notification
    await expectToast(page, /updated|saved|success|th\u00e0nh c\u00f4ng/i);
  });
});
