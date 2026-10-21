import { useAuth } from '@/auth/use-auth';
import type { SessionUser } from '@/auth/types';

export function hasHrPermission(user: SessionUser | null, code: string) {
  return Boolean(
    user?.grants.some((g) => g.permission === code && g.scopeKind === 7 && g.targetId === null),
  );
}
export function useHrAccess() {
  const { user, status } = useAuth();
  return {
    user,
    ready: status === 'authenticated',
    subject:
      user?.account?.accountId ??
      user?.grants
        .map((g) => g.roleAssignmentId)
        .sort()
        .join(':') ??
      'none',
    can: (...permissions: string[]) =>
      status === 'authenticated' && permissions.every((p) => hasHrPermission(user, p)),
  };
}
