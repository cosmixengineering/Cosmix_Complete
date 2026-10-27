import type { SessionStatus, SessionUser } from '@/auth/types';

export function hrConfigurationNavigation(user: SessionUser | null, status: SessionStatus) {
  const can = (permission: string) =>
    status === 'authenticated' &&
    Boolean(
      user?.account?.webAccessEnabled &&
      user.account.client === 1 &&
      user.grants.some(
        (grant) =>
          grant.permission === permission && grant.scopeKind === 7 && grant.targetId === null,
      ),
    );
  return [
    {
      to: '/hr/attendance/policies',
      label: 'Attendance policies',
      allowed: can('attendance.policies.configure') || can('attendance.policies.publish'),
    },
    {
      to: '/hr/attendance/leave-settings',
      label: 'Leave settings',
      allowed: can('attendance.leaves.configure'),
    },
    {
      to: '/hr/approval-routing',
      label: 'Approval routing',
      allowed:
        can('approvals.routes.read') ||
        can('approvals.routes.manage') ||
        can('approvals.delegations.manage'),
    },
  ];
}

export function hrPolicyNavigation(user: SessionUser | null, status: SessionStatus) {
  const canRead = (permission: string) =>
    status === 'authenticated' &&
    Boolean(
      user?.account?.webAccessEnabled &&
      user.account.client === 1 &&
      user.grants.some(
        (grant) =>
          grant.permission === permission && grant.scopeKind === 7 && grant.targetId === null,
      ),
    );
  return [
    {
      to: '/hr/foundation-policies/fields',
      label: 'Employee field policy',
      allowed: canRead('hr.foundation-policies.read'),
    },
    {
      to: '/hr/foundation-policies/lifecycle',
      label: 'Employment lifecycle policy',
      allowed: canRead('hr.foundation-policies.read'),
    },
    {
      to: '/hr/foundation-policies/records',
      label: 'Employee records policy',
      allowed: canRead('hr.foundation-policies.read'),
    },
    {
      to: '/hr/staff-taxonomy',
      label: 'Staff category policy',
      allowed: canRead('hr.staff-taxonomy.read'),
    },
  ];
}
