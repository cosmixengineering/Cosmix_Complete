import type { SessionUser } from '@/auth/types';
import { z } from 'zod';

const resourceId = z.uuid().refine((value) => value !== '00000000-0000-0000-0000-000000000000');

// Historical membership is server-owned. This only permits the scoped request UI.
export function hasReportGrant(user: SessionUser | null, permission: string) {
  return Boolean(
    user?.account &&
    user.grants.some(
      (grant) =>
        grant.permission === permission &&
        ((grant.scopeKind === 7 && grant.targetId === null) ||
          (grant.scopeKind === 1 &&
            resourceId.safeParse(grant.targetId).success &&
            grant.targetId === user.account?.employeeId) ||
          ([2, 3, 4, 5, 6].includes(grant.scopeKind) &&
            resourceId.safeParse(grant.targetId).success)),
    ),
  );
}
