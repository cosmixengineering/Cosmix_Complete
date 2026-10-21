import type { SessionUser } from '@/auth/types';
import { id } from '../../schemas/common';

export type Scope = { kind: number; targetId: string | null };
export const scopeNames: Record<number, string> = {
  2: 'Employee',
  3: 'Department',
  4: 'Team',
  5: 'Site',
  6: 'Project',
  7: 'All',
};
export const queueScopes: Record<number, number> = { 7: 1, 2: 2, 5: 3, 3: 4, 4: 5, 6: 6 };

export function supportedScopes(user: SessionUser | null, permission: string): Scope[] {
  return (user?.grants ?? [])
    .filter(
      (g) =>
        g.permission === permission &&
        (g.scopeKind === 7
          ? g.targetId === null
          : Boolean(scopeNames[g.scopeKind]) && id.safeParse(g.targetId).success),
    )
    .map((g) => ({ kind: g.scopeKind, targetId: g.targetId }))
    .filter(
      (g, index, all) =>
        all.findIndex((x) => x.kind === g.kind && x.targetId === g.targetId) === index,
    );
}
export function scopeKey(scope: Scope) {
  return `${scope.kind}:${scope.targetId ?? ''}`;
}
export function canScope(user: SessionUser | null, permission: string, scope: Scope) {
  return supportedScopes(user, permission).some(
    (g) => g.kind === 7 || (g.kind === scope.kind && g.targetId === scope.targetId),
  );
}
export function canSelf(user: SessionUser | null, permission: string) {
  const employeeId = user?.account?.employeeId;
  return Boolean(
    employeeId &&
    user?.grants.some(
      (g) =>
        g.permission === permission &&
        (g.scopeKind === 1 || g.scopeKind === 2) &&
        g.targetId === employeeId,
    ),
  );
}
export function canEmployee(
  user: SessionUser | null,
  permission: string,
  employeeId: string,
  scope?: Scope,
) {
  if (!id.safeParse(employeeId).success) return false;
  return supportedScopes(user, permission).some(
    (g) =>
      g.kind === 7 ||
      (g.kind === 2 && g.targetId === employeeId) ||
      (scope &&
        scope.kind !== 2 &&
        scope.kind !== 7 &&
        g.kind === scope.kind &&
        g.targetId === scope.targetId),
  );
}
