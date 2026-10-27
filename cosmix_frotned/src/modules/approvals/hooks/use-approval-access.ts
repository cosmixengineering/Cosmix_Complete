import { useHrAccess } from '../../hr/hooks/use-hr-access';
import { stagePermission } from '../api/approval-schemas';

export const approvalPermissions = {
  read: 'approvals.routes.read',
  manage: 'approvals.routes.manage',
  delegate: 'approvals.delegations.manage',
} as const;

// The server validates the current web MFA session and permission scopes.
// These checks only decide which views and controls are presented.
export function useApprovalAccess() {
  const access = useHrAccess();
  const canManage = Boolean(
    access.user?.account?.webAccessEnabled &&
    access.user.account.client === 1 &&
    access.can(approvalPermissions.manage),
  );
  return {
    ready: access.ready,
    subject: access.subject,
    accountId: access.user?.account?.accountId ?? null,
    canRead: access.can(approvalPermissions.read),
    canManage,
    canPublishStage: (stage: number) =>
      canManage &&
      Number.isInteger(stage) &&
      stage >= 1 &&
      stage <= 20 &&
      access.can(stagePermission(stage)),
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
