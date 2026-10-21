import { test, expect } from '../../fixtures/base.fixture';
import { Tags } from '../../helpers/performance';

// AUTH login tests do NOT use pre-saved auth — they test the login flow directly
test.use({ storageState: { cookies: [], origins: [] } });

test.describe('Auth Login @smoke', () => {
  /**
   * AUTH-001: Login success
   * Verify that a user can log in with valid credentials and reach the dashboard.
   */
  test('AUTH-001: should login successfully with valid credentials @smoke', async ({
    loginPage,
    page,
  }) => {
    await loginPage.goto();
    await loginPage.loginAndWaitForPortal('admin@noir.local', '123qwe');

    // Dashboard loaded — sidebar navigation is visible
    await expect(page.locator('[data-testid="sidebar"], aside nav, [role="navigation"]')).toBeVisible();
  });

  /**
   * AUTH-002: Login validation errors
   * Verify that validation messages appear for empty and invalid fields.
   */
  test('AUTH-002: should show validation errors for empty fields @smoke', async ({
    loginPage,
  }) => {
    await loginPage.goto();

    // Click Sign In without entering any fields
    await loginPage.submitButton.click();

    // Expect validation messages for required fields
    // Email and password fields should show required errors
    const emailError = loginPage.emailInput.locator('..').locator('..');
    await expect(
      emailError.page().getByText(/required|email|bắt buộc/i).first(),
    ).toBeVisible({ timeout: 5_000 });

    // Enter an invalid email format
    await loginPage.emailInput.fill('not-an-email');
    await loginPage.emailInput.press('Tab'); // trigger blur validation

    // Expect invalid email format error
    await expect(
      emailError.page().getByText(/invalid|email|không hợp lệ|valid/i).first(),
    ).toBeVisible({ timeout: 5_000 });
  });

  /**
   * AUTH-003: Login invalid credentials
   * Verify that an error alert appears when logging in with wrong password.
   */
  test('AUTH-003: should show error for invalid credentials @smoke', async ({
    loginPage,
  }) => {
    await loginPage.goto();
    await loginPage.login('admin@noir.local', 'wrongpassword');

    // Error alert should appear with invalid credentials message
    await loginPage.expectErrorVisible();

    // User should remain on the login page
    await loginPage.expectOnLoginPage();
  });
});

test.describe('Auth Redirect @smoke', () => {
  /**
   * AUTH-006: Unauthenticated redirect
   * Verify that accessing a protected route without auth redirects to /login.
   */
  test('AUTH-006: should redirect to login when accessing protected route without auth @smoke', async ({
    page,
  }) => {
    // Navigate directly to a protected route without auth
    await page.goto('/portal');

    // Should be redirected to login page
    await expect(page).toHaveURL(/login/, { timeout: 10_000 });
  });
});

test.describe('Auth Logout @regression', () => {
  /**
   * AUTH-007: Logout
   * Verify that logging out redirects to login and clears the session.
   *
   * This test logs in first (since storageState is cleared above),
   * then tests the logout flow.
   */
  test('AUTH-007: should logout and redirect to login @regression', async ({
    loginPage,
    page,
  }) => {
    // First, log in
    await loginPage.goto();
    await loginPage.loginAndWaitForPortal('admin@noir.local', '123qwe');

    // Open user menu (avatar/profile dropdown in header)
    // Look for user avatar/button in the header area
    const userMenuTrigger = page.getByRole('button', { name: /avatar|profile|user|account|admin/i })
      .or(page.locator('[data-testid="user-menu"]'))
      .or(page.locator('header button').last());
    await userMenuTrigger.click();

    // Click logout/sign out
    const logoutButton = page.getByRole('menuitem', { name: /log\s?out|sign\s?out|đăng xuất/i })
      .or(page.getByRole('button', { name: /log\s?out|sign\s?out|đăng xuất/i }));
    await logoutButton.click();

    // Should redirect to login page
    await expect(page).toHaveURL(/login/, { timeout: 10_000 });

    // Verify that navigating back to portal redirects to login again
    await page.goto('/portal');
    await expect(page).toHaveURL(/login/, { timeout: 10_000 });
  });
});
