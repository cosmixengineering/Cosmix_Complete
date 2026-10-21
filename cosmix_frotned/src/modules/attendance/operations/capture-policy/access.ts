import type { SessionUser } from '@/auth/types';

export const captureValidationPolicyPermissions = {
  read: 'attendance.capture.validation.read',
  manage: 'attendance.capture.validation.manage',
} as const;

export function hasCapturePolicyGrant(user: SessionUser | null, permission: string) {
  return Boolean(
    user?.account?.webAccessEnabled &&
    user.account.client === 1 &&
    user.grants.some(
      (grant) =>
        grant.permission === permission && grant.scopeKind === 7 && grant.targetId === null,
    ),
  );
}
