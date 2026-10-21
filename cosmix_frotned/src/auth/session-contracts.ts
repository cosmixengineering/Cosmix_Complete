import { z } from 'zod';
import type { SessionUser } from './types';

// CurrentAccountController / AuthenticationContracts: numeric SessionClient JSON.
export const deviceResponseSchema = z.object({
  id: z.uuid(),
  platform: z.string(),
  name: z.string(),
  registeredAt: z.iso.datetime({ offset: true }),
  lastSeenAt: z.iso.datetime({ offset: true }).nullable(),
  revokedAt: z.iso.datetime({ offset: true }).nullable(),
});
export const sessionSummarySchema = z.object({
  id: z.uuid(),
  client: z.union([z.literal(1), z.literal(2)]),
  deviceId: z.uuid().nullable(),
  createdAt: z.iso.datetime({ offset: true }),
  expiresAt: z.iso.datetime({ offset: true }),
  isCurrent: z.boolean(),
});
export const devicesPageSchema = z.array(deviceResponseSchema).max(100);
export const sessionsPageSchema = z.array(sessionSummarySchema).max(100);
export type DeviceResponse = z.infer<typeof deviceResponseSchema>;
export type SessionSummary = z.infer<typeof sessionSummarySchema>;

export const accountSessionContract = {
  permissions: { read: 'auth.self.read', manage: 'auth.self.sessions.manage' },
  scopeKind: 1,
  pageSize: 20,
  maxPage: 1_000_000,
  maxPageSize: 100,
  devices: '/me/devices',
  sessions: '/me/sessions',
} as const;

export function hasOwnAccountPermission(
  user: SessionUser | null,
  permission: (typeof accountSessionContract.permissions)[keyof typeof accountSessionContract.permissions],
) {
  const emptyId = '00000000-0000-0000-0000-000000000000';
  // /me/permissions caps self-service grants to Self, even for original All grants.
  // /me can be forbidden independently: a server-projected Self target still identifies the subject.
  return Boolean(
    user?.grants.some(
      (grant) =>
        grant.permission === permission &&
        grant.scopeKind === accountSessionContract.scopeKind &&
        grant.roleAssignmentId !== emptyId &&
        grant.targetId !== null &&
        grant.targetId !== emptyId &&
        (!user.account || grant.targetId === user.account.employeeId),
    ),
  );
}
