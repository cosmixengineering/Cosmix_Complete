import type { SessionUser } from '@/auth/types';
import { hasHrPermission } from '../hooks/use-hr-access';
import { sameId, type RecordKind, type RecordRequest } from './contracts';

export const recordFamilies = [
  { kind: 'Asset', segment: 'assets', label: 'Assets' },
  { kind: 'Sim', segment: 'sims', label: 'Company SIMs' },
  { kind: 'Certification', segment: 'certifications', label: 'Certifications' },
] as const;
export const familyOf = (kind: RecordKind) =>
  recordFamilies.find((family) => family.kind === kind)!;
export const recordPermissions = (
  kind: RecordKind,
  action: 'read' | 'request' | 'review' = 'read',
) => [
  'hr.employees.read',
  `hr.employees.${familyOf(kind).segment}.read`,
  ...(action === 'read' ? [] : [`hr.employees.${familyOf(kind).segment}.${action}`]),
];
export function recordScope(user: SessionUser | null, status: string) {
  return JSON.stringify([
    status,
    user?.account?.accountId,
    user?.account?.employeeId,
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
export function canRecordAction(
  user: SessionUser | null,
  kind: RecordKind,
  action: 'cancel' | 'decision',
  request: RecordRequest,
) {
  if (
    !user?.account ||
    request.state !== 0 ||
    !recordPermissions(kind, action === 'cancel' ? 'request' : 'review').every((permission) =>
      hasHrPermission(user, permission),
    )
  )
    return false;
  return action === 'cancel'
    ? sameId(user.account.accountId, request.requesterId)
    : !sameId(user.account.accountId, request.requesterId) &&
        !sameId(user.account.employeeId, request.employeeId);
}
