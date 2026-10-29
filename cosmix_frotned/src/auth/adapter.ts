import { request, allowSessionRefresh, blockSessionRefresh } from '@/services/api/http-client';
import { ApiError } from '@/services/api/api-error';
import { accountSchema, grantsSchema, otpChallengeSchema, browserSessionSchema } from './contracts';
import type { SessionUser } from './types';

const web = '/auth/web';
const post = (path: string, body?: unknown) =>
  request(web + path, { method: 'POST', body, anonymous: true });
let restorePending: Promise<SessionUser | null> | undefined;

async function readSession(): Promise<SessionUser | null> {
  try {
    const grants = grantsSchema.parse(await request('/me/permissions'));
    try {
      return { account: accountSchema.parse(await request('/me')), grants };
    } catch (error) {
      if (error instanceof ApiError && error.status === 403) return { account: null, grants };
      throw error;
    }
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null;
    throw error;
  }
}

export const authAdapter = {
  restoreSession(fresh = false) {
    if (fresh) return readSession();
    restorePending ??= readSession().finally(() => {
      restorePending = undefined;
    });
    return restorePending;
  },
  async login(email: string, password: string, authenticator = false) {
    return otpChallengeSchema.parse(
      await post(authenticator ? '/authenticator/login' : '/login', { email, password }),
    );
  },
  async resend(challengeId: string, email: string, password: string) {
    return otpChallengeSchema.parse(await post('/otp/resend', { challengeId, email, password }));
  },
  async verify(challengeId: string, code: string) {
    browserSessionSchema.parse(await post('/otp/verify', { challengeId, code }));
    allowSessionRefresh();
  },
  async verifyAuthenticator(challengeId: string, code: string, recoveryCode: boolean) {
    browserSessionSchema.parse(
      await post('/authenticator/verify', { challengeId, code, recoveryCode }),
    );
    allowSessionRefresh();
  },
  async signOut() {
    await request('/me/permissions');
    await post('/logout');
    blockSessionRefresh();
  },
  async forgotPassword(email: string) {
    return otpChallengeSchema.parse(await post('/password/forgot', { email }));
  },
  async resendReset(challengeId: string, email: string) {
    return otpChallengeSchema.parse(await post('/password/resend', { challengeId, email }));
  },
  async resetPassword(challengeId: string, code: string, newPassword: string) {
    await post('/password/reset', { challengeId, code, newPassword });
    blockSessionRefresh();
  },
};
