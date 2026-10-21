import type { SessionStatus, SessionUser } from '@/auth/types';
import { sameId, type TaxonomyRequest } from './contracts';

export const staffTaxonomyPermissions = {
  read: 'hr.staff-taxonomy.read',
  request: 'hr.staff-taxonomy.request',
  review: 'hr.staff-taxonomy.review',
} as const;
export function canTaxonomy(
  user: SessionUser | null,
  status: SessionStatus,
  action: keyof typeof staffTaxonomyPermissions,
) {
  return (
    status === 'authenticated' &&
    Boolean(
      user?.account &&
      user.account.client === 1 &&
      user.account.webAccessEnabled &&
      user.grants.some(
        (g) =>
          g.permission === staffTaxonomyPermissions[action] &&
          g.scopeKind === 7 &&
          g.targetId === null,
      ),
    )
  );
}
export function canTaxonomyAction(
  user: SessionUser | null,
  status: SessionStatus,
  action: 'decision' | 'cancel',
  request: TaxonomyRequest,
) {
  if (
    !canTaxonomy(user, status, 'read') ||
    !canTaxonomy(user, status, action === 'decision' ? 'review' : 'request') ||
    request.state !== 'Pending'
  )
    return false;
  const requester = sameId(user!.account!.accountId, request.requesterId);
  return action === 'cancel' ? requester : !requester;
}
export function taxonomyScope(user: SessionUser | null, status: SessionStatus) {
  return JSON.stringify([
    status,
    user?.account,
    user?.grants
      .map((g) => [g.permission, g.scopeKind, g.targetId, g.roleAssignmentId, g.sensitive])
      .sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b))),
  ]);
}
