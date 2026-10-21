import type { SessionUser } from '@/auth/types';

export function attendanceSubject(user: SessionUser | null) {
  return [
    user?.account?.accountId ?? 'none',
    user?.account?.employeeId ?? 'none',
    ...(user?.grants
      .map((grant) =>
        [grant.roleAssignmentId, grant.permission, grant.scopeKind, grant.targetId].join(':'),
      )
      .sort() ?? []),
  ].join('|');
}

export function hasAttendancePermission(user: SessionUser | null, code: string, siteId?: string) {
  const sitePermission = code === 'attendance.rosters.read' || code === 'attendance.rosters.manage';
  return Boolean(
    user?.grants.some(
      (grant) =>
        grant.permission === code &&
        ((grant.scopeKind === 7 && grant.targetId === null) ||
          (sitePermission && siteId && grant.scopeKind === 5 && grant.targetId === siteId)),
    ),
  );
}
