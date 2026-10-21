import { test, expect } from '../../fixtures/base.fixture';
import { Tags } from '../../helpers/performance';

test.describe('Auth Session @regression', () => {
  /**
   * AUTH-004: Forgot password flow initiation
   * Verify that the forgot password link navigates to the forgot-password page
   * and the form can be submitted.
   *
   * This test does NOT use pre-saved auth — it starts from the login page.
   */
  test.describe('Forgot Password', () => {
    test.use({ storageState: { cookies: [], origins: [] } });

    test('AUTH-004: should navigate to forgot password and submit email @regression', async ({
      loginPage,
      page,
    }) => {
      await loginPage.goto();

      // Click "Forgot Password" link
      await loginPage.forgotPasswordLink.click();

      // Verify redirect to forgot-password page
      await expect(page).toHaveURL(/forgot-password/, { timeout: 10_000 });

      // Enter email and submit
      const emailInput = page.getByLabel(/email/i);
      await emailInput.fill('admin@noir.local');

      const submitButton = page.getByRole('button', { name: /send|submit|gửi|reset/i });
      await submitButton.click();

      // Verify redirect to OTP verification page
      await expect(page).toHaveURL(/forgot-password\/verify|verify/, { timeout: 10_000 });
    });
  });

  /**
   * AUTH-005: Session persistence on refresh
   * Verify that the user remains authenticated after a page refresh.
   *
   * This test USES pre-saved auth (storageState) — it starts already logged in.
   */
  test('AUTH-005: should persist session across page refresh @regression', async ({
    dashboardPage,
    page,
  }) => {
    // Navigate to dashboard (already authenticated via storageState)
    await dashboardPage.goto();
    await dashboardPage.expectLoaded();

    // Remember the current URL
    const urlBeforeRefresh = page.url();

    // Reload the page (simulates F5)
    await page.reload();

    // Verify dashboard loads again without redirect to login
    await dashboardPage.expectLoaded();

    // Verify no flash of login page — still on dashboard
    await expect(page).not.toHaveURL(/login/);
    await expect(page).toHaveURL(/portal/);
  });
});
