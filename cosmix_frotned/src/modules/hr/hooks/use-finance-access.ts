import type { SessionUser } from '@/auth/types';
import { useHrAccess } from './use-hr-access';

// Self-service permissions are granted either with All scope or with the employee's own
// employee scope (the same rule the server applies to self-service attendance operations).
export function hasFinanceSelfPermission(user: SessionUser | null, code: string) {
  const employeeId = user?.account?.employeeId;
  return Boolean(
    user?.grants.some(
      (g) =>
        g.permission === code &&
        ((g.scopeKind === 7 && g.targetId === null) ||
          ((g.scopeKind === 1 || g.scopeKind === 2) && g.targetId === employeeId)),
    ),
  );
}

export function useFinanceAccess() {
  const access = useHrAccess();
  return {
    user: access.user,
    ready: access.ready,
    subject: access.subject,
    employeeId: access.user?.account?.employeeId ?? null,
    can: access.can,
    canSelf: (...permissions: string[]) =>
      access.ready && permissions.every((p) => hasFinanceSelfPermission(access.user, p)),
  };
}
