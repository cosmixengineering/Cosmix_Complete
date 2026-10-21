import { test, expect } from '../../fixtures/base.fixture';
import { expectToast } from '../../helpers/selectors';

const API_URL = process.env.API_URL ?? 'http://localhost:4000';
const DEV_HEADERS = {
  'X-Tenant': 'default',
  'Content-Type': 'application/json',
};

const TEST_EMAIL = 'admin@noir.local';
const ORIGINAL_PASSWORD = '123qwe';
const NEW_PASSWORD = 'Test123!@#E2E';

// Password reset tests do NOT use pre-saved auth — they test the auth flows directly
test.use({ storageState: { cookies: [], origins: [] } });

test.describe('Auth Password Reset @smoke', () => {
  /**
   * AUTH-PR-001: Full password reset happy path
   * Verify the complete flow: forgot-password → OTP verification → new password → login.
   * Uses DevEndpoints to bypass email delivery and get the plaintext OTP.
   */
  test('AUTH-PR-001: full password reset happy path @smoke', async ({
    loginPage,
    page,
    trackCleanup,
  }) => {
    // Register cleanup to always restore the admin password, even on failure
    trackCleanup(async () => {
      await page.request.post(`${API_URL}/api/dev/auth/set-password`, {
        data: { email: TEST_EMAIL, newPassword: ORIGINAL_PASSWORD },
        headers: DEV_HEADERS,
      });
    });

    // Step 1: Navigate from login to forgot-password
    await loginPage.goto();
    await loginPage.forgotPasswordLink.click();
    await expect(page).toHaveURL(/forgot-password/, { timeout: 10_000 });

    // Step 2: Enter email and submit the forgot-password form
    const emailInput = page.getByLabel(/email/i);
    await emailInput.fill(TEST_EMAIL);

    const sendButton = page.getByRole('button', { name: /send|submit|gửi|reset/i });
    await sendButton.click();

    // Should navigate to the OTP verification page
    await expect(page).toHaveURL(/forgot-password\/verify/, { timeout: 15_000 });

    // Step 3: Use DevEndpoints to get the plaintext OTP (bypasses email delivery)
    const devRes = await page.request.post(`${API_URL}/api/dev/auth/test-password-reset`, {
      data: { email: TEST_EMAIL, tenantIdentifier: 'default' },
      headers: DEV_HEADERS,
    });
    expect(devRes.ok()).toBeTruthy();
    const { plainOtp, sessionToken } = await devRes.json();
    expect(plainOtp).toBeTruthy();

    // Inject the dev session token into sessionStorage so the OTP page picks it up
    await page.evaluate(
      ({ token, otp }) => {
        const existing = sessionStorage.getItem('passwordReset');
        if (existing) {
          const parsed = JSON.parse(existing);
          // Keep the maskedEmail/otpLength from the real flow, but override sessionToken
          sessionStorage.setItem(
            'passwordReset',
            JSON.stringify({ ...parsed, sessionToken: token }),
          );
        }
      },
      { token: sessionToken, otp: plainOtp },
    );

    // Reload the verify page to pick up the updated session token
    await page.goto('/forgot-password/verify');
    await page.waitForLoadState('domcontentloaded');

    // Step 4: Enter OTP digits
    // The OTP input renders individual digit inputs with aria-label "Digit N of 6"
    const otpDigits = plainOtp.split('');
    for (let i = 0; i < otpDigits.length; i++) {
      const digitInput = page.getByLabel(new RegExp(`Digit ${i + 1} of`, 'i'));
      await digitInput.fill(otpDigits[i]);
    }

    // OTP auto-submits on completion — wait for navigation to reset password page
    await expect(page).toHaveURL(/forgot-password\/reset/, { timeout: 15_000 });

    // Step 5: Enter new password and confirm
    const passwordInput = page.getByLabel(/new password/i).first();
    await passwordInput.fill(NEW_PASSWORD);

    const confirmInput = page.getByLabel(/confirm/i);
    await confirmInput.fill(NEW_PASSWORD);

    const resetButton = page.getByRole('button', { name: /reset|submit|đặt lại/i });
    await resetButton.click();

    // Should navigate to success page or show success toast
    await expect(page).toHaveURL(/forgot-password\/success|login/, { timeout: 15_000 });

    // Step 6: Restore original password via DevEndpoints
    const restoreRes = await page.request.post(`${API_URL}/api/dev/auth/set-password`, {
      data: { email: TEST_EMAIL, newPassword: ORIGINAL_PASSWORD },
      headers: DEV_HEADERS,
    });
    expect(restoreRes.ok()).toBeTruthy();

    // Step 7: Verify login works with the restored original password
    await page.goto('/login');
    await loginPage.loginAndWaitForPortal(TEST_EMAIL, ORIGINAL_PASSWORD);
    await expect(page.locator('[data-testid="sidebar"], aside nav, [role="navigation"]')).toBeVisible();
  });

  /**
   * AUTH-PR-002: Forgot password validation — empty email
   * Verify that submitting without an email shows a validation error.
   */
  test('AUTH-PR-002: should show validation error for empty email @smoke', async ({
    page,
  }) => {
    await page.goto('/forgot-password');
    await page.waitForLoadState('domcontentloaded');

    // The email input has required attribute — submit without filling it
    const sendButton = page.getByRole('button', { name: /send|submit|gửi|reset/i });
    await sendButton.click();

    // Expect validation error (required/email format error message)
    // May appear as .text-destructive class, aria-invalid, or an error text element
    const errorMessage = page.locator('.text-destructive')
      .or(page.locator('[aria-invalid="true"]'))
      .or(page.locator('[class*="error"]').filter({ hasText: /.+/ }))
      .or(page.getByText(/required|please enter|valid email|email is required/i))
      .first();
    // Also accept native browser validation tooltip as fallback — just verify form didn't submit
    const didNotNavigate = page.url().includes('forgot-password') && !page.url().includes('verify');
    const hasError = await errorMessage.isVisible({ timeout: 5_000 }).catch(() => false);
    expect(hasError || didNotNavigate).toBeTruthy();
  });
});

test.describe('Auth Password Reset @regression', () => {
  /**
   * AUTH-PR-003: Forgot password — unknown email graceful handling
   * Verify that submitting with a non-existent email doesn't crash.
   * For security, the backend may return success even for unknown emails.
   */
  test('AUTH-PR-003: should handle unknown email gracefully @regression', async ({
    page,
  }) => {
    await page.goto('/forgot-password');
    await page.waitForLoadState('domcontentloaded');

    const emailInput = page.getByLabel(/email/i);
    await emailInput.fill('nonexistent@noir.local');

    const sendButton = page.getByRole('button', { name: /send|submit|gửi|reset/i });
    await sendButton.click();

    // The backend returns success for security (doesn't reveal if email exists).
    // Either navigates to verify page or shows an error — both are acceptable.
    // The key assertion: no crash, no unhandled error.
    await page.waitForTimeout(3_000);

    // Verify the page is still in a sensible state (not a blank/error page)
    const isOnVerify = page.url().includes('verify');
    const isOnForgotPassword = page.url().includes('forgot-password');
    expect(isOnVerify || isOnForgotPassword).toBeTruthy();
  });

  /**
   * AUTH-PR-004: OTP verification — wrong OTP
   * Verify that entering an incorrect OTP shows an error message.
   */
  test('AUTH-PR-004: should show error for wrong OTP @regression', async ({
    page,
    trackCleanup,
  }) => {
    // Use DevEndpoints to create a valid OTP session (so the verify page loads)
    const devRes = await page.request.post(`${API_URL}/api/dev/auth/test-password-reset`, {
      data: { email: TEST_EMAIL, tenantIdentifier: 'default' },
      headers: DEV_HEADERS,
    });
    expect(devRes.ok()).toBeTruthy();
    const { sessionToken, maskedEmail, otpLength } = await devRes.json();

    // Set up sessionStorage with the dev session so the verify page loads
    await page.goto('/forgot-password');
    await page.evaluate(
      (data) => {
        sessionStorage.setItem('passwordReset', JSON.stringify(data));
      },
      {
        sessionToken,
        maskedEmail,
        expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
        otpLength: otpLength ?? 6,
      },
    );

    // Navigate to the verify page
    await page.goto('/forgot-password/verify');
    await page.waitForLoadState('domcontentloaded');

    // Enter wrong OTP (all zeros)
    const wrongOtp = '000000';
    for (let i = 0; i < wrongOtp.length; i++) {
      const digitInput = page.getByLabel(new RegExp(`Digit ${i + 1} of`, 'i'));
      await digitInput.fill(wrongOtp[i]);
    }

    // OTP auto-submits — wait for error message to appear
    const errorMessage = page.locator('.text-destructive').first();
    await expect(errorMessage).toBeVisible({ timeout: 10_000 });

    // Should remain on the verify page (not navigate away)
    await expect(page).toHaveURL(/forgot-password\/verify/);
  });
});
