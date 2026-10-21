import { z } from 'zod';

export const otpChallengeSchema = z.object({
  challengeId: z.uuid(),
  expiresAt: z.iso.datetime({ offset: true }),
  resendAvailableAt: z.iso.datetime({ offset: true }),
  nextStep: z.literal('enter_otp'),
});
export type OtpChallenge = z.infer<typeof otpChallengeSchema>;

export const browserSessionSchema = z.object({
  sessionId: z.uuid(),
  accessTokenExpiresAt: z.iso.datetime({ offset: true }),
  refreshTokenExpiresAt: z.iso.datetime({ offset: true }),
  nextStep: z.literal('authenticated'),
});
export const accountSchema = z.object({
  accountId: z.uuid(),
  employeeId: z.uuid(),
  displayName: z.string().nullable(),
  mobileAccessEnabled: z.boolean(),
  webAccessEnabled: z.literal(true),
  client: z.literal(1), // SessionClient.Web in the current .NET JSON contract.
});
export const grantsSchema = z.array(
  z.object({
    permission: z.string().min(1),
    sensitive: z.boolean(),
    roleAssignmentId: z.uuid(),
    scopeKind: z.number().int().min(1).max(7),
    targetId: z.uuid().nullable(),
  }),
);
export type CurrentAccount = z.infer<typeof accountSchema>;
export type PermissionGrant = z.infer<typeof grantsSchema>[number];

export function isSystemAdmin(
  user: { grants?: readonly { permission: string }[] } | null | undefined,
): boolean {
  return Boolean(
    user?.grants?.some(
      (g) =>
        g.permission.startsWith('access.roles.') ||
        g.permission.startsWith('access.accounts.') ||
        g.permission.startsWith('admin.'),
    ),
  );
}
