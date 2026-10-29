import { z } from 'zod';
import { id, time, pageOf } from './contracts';

export const accessVersionSchema = z.string().regex(/^[A-Za-z0-9+/]{11}=$/);
export const accessAccountSchema = z.object({
  id,
  employeeId: id,
  status: z.number().int().min(0).max(5),
  mobileAccessEnabled: z.boolean(),
  webAccessEnabled: z.boolean(),
  mfaRequired: z.boolean(),
  authenticatorRequired: z.boolean(),
  version: accessVersionSchema,
});
export const employeeAccessSetupSchema = z.object({
  employeeId: id,
  account: accessAccountSchema.nullable(),
  mobileAccessRequested: z.boolean(),
  initialAccessAssigned: z.boolean(),
  mobilePhoneEnrolled: z.boolean(),
});
export const accessPermissionSchema = z.object({
  code: z.string().min(1),
  description: z.string(),
  sensitive: z.boolean(),
  management: z.boolean(),
  selfService: z.boolean(),
});
export const accessRoleSchema = z.object({
  id,
  code: z.string(),
  name: z.string(),
  isActive: z.boolean(),
  permissions: z.array(z.string()),
  version: accessVersionSchema,
  systemRole: z.number().int().min(0).max(2),
  isSystemRole: z.boolean(),
});
export const accessAssignmentSchema = z.object({
  id,
  roleId: id,
  roleCode: z.string(),
  effectiveFrom: time,
  effectiveTo: time.nullable(),
  revokedAt: time.nullable(),
  scopes: z.array(z.object({ kind: z.number().int().min(1).max(7), targetId: id.nullable() })),
  version: accessVersionSchema,
});
export const accessRolePageSchema = pageOf(accessRoleSchema);
export const accessAssignmentPageSchema = pageOf(accessAssignmentSchema);
export const initialAccessResultSchema = z.object({
  assignment: accessAssignmentSchema,
  accountVersion: accessVersionSchema,
});
export const mobileProvisioningResultSchema = z.object({
  accountId: id,
  status: z.number().int().min(0).max(5),
  accessAssignmentRequired: z.boolean(),
});
export const firstWebCredentialsSchema = z.object({
  email: z.email().max(320),
  password: z
    .string()
    .min(1)
    .max(1024)
    .refine((value) => value.trim().length > 0),
  expectedAccountVersion: accessVersionSchema,
});
export type AccessRole = z.infer<typeof accessRoleSchema>;
export type AccessAssignment = z.infer<typeof accessAssignmentSchema>;
export type AccessAccount = z.infer<typeof accessAccountSchema>;
export type AccessPermission = z.infer<typeof accessPermissionSchema>;
export const accessScopeRequestSchema = z
  .object({
    kind: z.number().int().min(1).max(7),
    targetId: id.nullable(),
  })
  .refine((scope) =>
    scope.kind === 1 || scope.kind === 7
      ? scope.targetId === null
      : scope.targetId !== null && scope.targetId !== '00000000-0000-0000-0000-000000000000',
  );
export const accessReasonSchema = z
  .string()
  .min(1)
  .max(1000)
  .refine((value) =>
    [...value].every((character) => {
      const code = character.charCodeAt(0);
      return code > 31 && (code < 127 || code > 159);
    }),
  )
  .transform((value) => value.trim())
  .refine((value) => value.length > 0);
export const initialRoleAssignmentSchema = z.object({
  roleId: id,
  scopes: z.tuple([z.object({ kind: z.literal(1), targetId: z.null() })]),
  effectiveFrom: time,
  effectiveTo: z.null(),
  expectedAccountVersion: accessVersionSchema,
  expectedRoleVersion: accessVersionSchema,
  configureAsInitialAccess: z.literal(true),
  reason: accessReasonSchema,
});
export const additionalRoleAssignmentSchema = z
  .object({
    roleId: id,
    scopes: z.array(accessScopeRequestSchema).min(1).max(50),
    effectiveFrom: time,
    effectiveTo: time.nullable(),
    expectedAccountVersion: accessVersionSchema,
    expectedRoleVersion: accessVersionSchema,
    configureAsInitialAccess: z.literal(false),
    reason: accessReasonSchema,
  })
  .refine(
    (value) =>
      !value.effectiveTo || Date.parse(value.effectiveTo) > Date.parse(value.effectiveFrom),
  )
  .refine((value) => value.scopes.length === 1 || !value.scopes.some((scope) => scope.kind === 7))
  .refine(
    (value) =>
      new Set(value.scopes.map((scope) => JSON.stringify(scope))).size === value.scopes.length,
  );
export type AdditionalRoleAssignmentRequest = z.infer<typeof additionalRoleAssignmentSchema>;
export const accessStatusLabels = [
  'Pending access',
  'Ready for invitation',
  'Invited',
  'Active',
  'Suspended',
  'Disabled',
] as const;
