import type { SessionUser } from '@/auth/types';

const sitePermissions = new Set([
  'attendance.zones.read',
  'attendance.zones.manage',
  'attendance.zones.review',
  'attendance.zones.location.read',
  'attendance.site-reports.read',
  'attendance.site-reports.decide',
]);
const selfPermissions = new Set(['attendance.zones.self', 'attendance.site-reports.self']);

export function fieldPermission(
  user: SessionUser | null,
  permission: string,
  siteId?: string,
  employeeId?: string,
) {
  if (!user?.account) return false;
  return user.grants.some((grant) => {
    if (grant.permission !== permission) return false;
    if (selfPermissions.has(permission))
      return (
        ((grant.scopeKind === 1 || grant.scopeKind === 2) &&
          grant.targetId === user.account?.employeeId) ||
        (grant.scopeKind === 7 && grant.targetId === null)
      );
    if (!sitePermissions.has(permission)) return false;
    return (
      (grant.scopeKind === 7 && grant.targetId === null) ||
      (grant.scopeKind === 5 && Boolean(siteId) && grant.targetId === siteId) ||
      (permission === 'attendance.zones.location.read' &&
        grant.scopeKind === 2 &&
        Boolean(employeeId) &&
        grant.targetId === employeeId)
    );
  });
}
