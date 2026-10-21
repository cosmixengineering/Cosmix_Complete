import { useHrAccess } from '../../hr/hooks/use-hr-access';

export const approvalPermissions = {
  read: 'approvals.routes.read',
  manage: 'approvals.routes.manage',
  delegate: 'approvals.delegations.manage',
} as const;

// Server authorization (administrator status, All scope, fixed SuperAdmin) stays
// authoritative; these checks only decide which views and controls are presented.
export function useApprovalAccess() {
  const access = useHrAccess();
  return {
    ready: access.ready,
    subject: access.subject,
    accountId: access.user?.account?.accountId ?? null,
    canRead: access.can(approvalPermissions.read),
    canManage: access.can(approvalPermissions.manage),
    canDelegate: access.can(approvalPermissions.delegate),
    holdsPermission: (permission: string) =>
      Boolean(access.user?.grants.some((grant) => grant.permission === permission)),
    holdsDelegationAuthority: (permission: string, scopeKind: number, targetId: string | null) =>
      Boolean(
        access.user?.grants.some(
          (grant) =>
            grant.permission === permission &&
            (grant.scopeKind === 7 ||
              (grant.scopeKind === scopeKind &&
                (targetId === null ? grant.targetId === null : grant.targetId === targetId))),
        ),
      ),
  };
}
