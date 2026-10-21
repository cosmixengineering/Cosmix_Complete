import type { SessionUser } from '@/auth/types';
import { sameId, type FoundationPolicyRequest } from './contracts';

export const foundationPolicyPermissions = {
  read: 'hr.foundation-policies.read',
  request: 'hr.foundation-policies.request',
  review: 'hr.foundation-policies.review',
} as const;
export function canFoundationPolicy(
  user: SessionUser | null,
  status: string,
  action: keyof typeof foundationPolicyPermissions,
) {
  return Boolean(
    status === 'authenticated' &&
    user?.account?.webAccessEnabled &&
    user.account.client === 1 &&
    [foundationPolicyPermissions.read, foundationPolicyPermissions[action]].every((permission) =>
      user.grants.some(
        (grant) =>
          grant.permission === permission && grant.scopeKind === 7 && grant.targetId === null,
      ),
    ),
  );
}
export function canFoundationPolicyAction(
  user: SessionUser | null,
  status: string,
  action: 'decision' | 'cancel',
  request: Pick<FoundationPolicyRequest, 'state' | 'requesterId'>,
) {
  if (
    request.state !== 0 ||
    !canFoundationPolicy(user, status, action === 'cancel' ? 'request' : 'review')
  )
    return false;
  const own = sameId(user?.account?.accountId, request.requesterId);
  return action === 'cancel' ? own : !own;
}
export function foundationPolicyScope(user: SessionUser | null, status: string) {
  return JSON.stringify([
    status,
    user?.account,
    user?.grants
      .map((grant) => [
        grant.permission,
        grant.scopeKind,
        grant.targetId,
        grant.roleAssignmentId,
        grant.sensitive,
      ])
      .sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b))),
  ]);
}
