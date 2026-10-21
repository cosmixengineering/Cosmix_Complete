import type { SessionStatus, SessionUser } from '@/auth/types';

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
