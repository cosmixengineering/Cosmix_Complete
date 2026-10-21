import { ApiError } from '@/services/api/api-error';

export function authErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401)
      return 'Verification failed or expired. Check your details and request a new code if needed.';
    if (error.code === 'invalid_csrf_token' || error.code === 'browser_context_required')
      return 'Your browser session changed. Please try again.';
    if (error.code === 'invalid_new_password')
      return 'The new password does not meet your company password policy. Contact your administrator for the required length.';
    if (error.status === 400) return 'Check the information entered and try again.';
    if (error.status === 403)
      return 'Access is not available for this account. Contact your administrator.';
    if (error.status === 429) return 'Too many attempts. Please wait before trying again.';
    if (error.status >= 500)
      return 'Sign-in is temporarily unavailable. Please contact your administrator or try again later.';
  }
  return 'Unable to complete this request. Check your connection before trying again.';
}
