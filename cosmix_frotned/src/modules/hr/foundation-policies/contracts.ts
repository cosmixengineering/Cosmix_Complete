import { z } from 'zod';

export const policyKinds = ['Fields', 'Lifecycle', 'Records'] as const;
export type FoundationPolicyKind = (typeof policyKinds)[number];
export const sameId = (a: string | null | undefined, b: string | null | undefined) =>
  Boolean(a && b && a.toLowerCase() === b.toLowerCase());
export const policyId = z.uuid().refine((id) => id !== '00000000-0000-0000-0000-000000000000');
export const policyDate = z.iso.date().refine((date) => date > '0001-01-01' && date < '9999-12-31');
const endDate = z.iso
  .date()
  .refine((date) => date > '0001-01-01')
  .nullable();
const instant = z.iso.datetime({ offset: true });
export const rowVersion = z.string().regex(/^[A-Za-z0-9+/]{10}[AEIMQUYcgkosw048]=$/);
const printable = (max: number) =>
  z
    .string()
    .min(1)
    .max(max)
    .refine(
      (value) =>
        value === value.trim() &&
        ![...value].some((character) => {
          const code = character.charCodeAt(0);
          return code < 32 || (code >= 127 && code <= 159);
        }),
      'Use trimmed printable text.',
    );
export const policyReason = printable(2000);
export const identityTypes = ['Cnic', 'Passport'] as const;
export const documentTypes = [
  'Offer',
  'Contract',
  'Identity',
  'Degree',
  'Certificate',
  'License',
  'Signature',
  'ThumbImpression',
  'ProfilePhoto',
  'Other',
] as const;
const distinct = (values: readonly string[]) =>
  new Set(values.map((v) => v.toLowerCase())).size === values.length;
const vocabulary = (max: number) =>
  z.array(printable(max)).max(100).refine(distinct, 'Values must be distinct.');
const limit = z.number().int().min(0).max(2147483647).nullable();
export const fieldRulesSchema = z
  .strictObject({
    genderValues: vocabulary(50),
    maritalStatusValues: vocabulary(50),
    simStatusValues: vocabulary(50),
    simCreditTierValues: vocabulary(40),
    requireGender: z.boolean(),
    requireMaritalStatus: z.boolean(),
    requireFatherOrSpouseName: z.boolean(),
    requireProbationEnd: z.boolean(),
    minimumAgeAtJoining: limit,
    maximumAgeAtJoining: limit,
    maximumProbationDays: limit,
    requireExpiryForIdentityTypes: z.array(z.enum(identityTypes)).max(2).refine(distinct),
    requiredDocuments: z
      .array(
        z.strictObject({
          staffCategoryCode: z
            .string()
            .regex(/^[A-Z0-9][A-Z0-9_.-]{0,39}$/)
            .nullable(),
          types: z.array(z.enum(documentTypes)).min(1).max(10).refine(distinct),
        }),
      )
      .max(100)
      .refine(
        (rows) => new Set(rows.map((row) => row.staffCategoryCode)).size === rows.length,
        'Each staff category may appear only once.',
      ),
  })
  .superRefine((rules, context) => {
    if (rules.requireGender && !rules.genderValues.length)
      context.addIssue({
        code: 'custom',
        path: ['genderValues'],
        message: 'Required gender needs an approved value list.',
      });
    if (rules.requireMaritalStatus && !rules.maritalStatusValues.length)
      context.addIssue({
        code: 'custom',
        path: ['maritalStatusValues'],
        message: 'Required marital status needs an approved value list.',
      });
    if (
      rules.minimumAgeAtJoining !== null &&
      rules.maximumAgeAtJoining !== null &&
      rules.maximumAgeAtJoining < rules.minimumAgeAtJoining
    )
      context.addIssue({
        code: 'custom',
        path: ['maximumAgeAtJoining'],
        message: 'Maximum age must be at least the minimum age.',
      });
  });
export const lifecycleRulesSchema = z.strictObject({
  allowRetirement: z.boolean(),
  allowSuspensionRelease: z.boolean(),
});
export const recordRulesSchema = z.strictObject({
  approvedReviewMode: z.literal('FixedAdminReview'),
});
const allRules = z.strictObject({
  fields: fieldRulesSchema.nullish(),
  lifecycle: lifecycleRulesSchema.nullish(),
  records: recordRulesSchema.nullish(),
});
export function rulesSchema(kind: FoundationPolicyKind) {
  return allRules.refine(
    (rules) =>
      [rules.fields, rules.lifecycle, rules.records].filter((rule) => rule != null).length === 1 &&
      rules[kind === 'Fields' ? 'fields' : kind === 'Lifecycle' ? 'lifecycle' : 'records'] != null,
    'Exactly the selected policy family is required.',
  );
}
export type FoundationPolicyRules = z.infer<typeof allRules>;
export type FoundationFieldRules = z.infer<typeof fieldRulesSchema>;
export function proposalSchema(kind: FoundationPolicyKind) {
  return z
    .strictObject({
      expectedPolicyVersion: rowVersion.nullable(),
      expectedLatestVersion: z.number().int().min(0).max(1023),
      approvedVersion: printable(100),
      effectiveFrom: policyDate,
      effectiveTo: endDate,
      rules: rulesSchema(kind),
      reason: policyReason,
    })
    .refine(
      (value) => value.effectiveTo === null || value.effectiveTo > value.effectiveFrom,
      'Effective end must be after the start.',
    );
}
export type FoundationPolicyProposal = z.infer<ReturnType<typeof proposalSchema>>;
const identitySchema = z
  .strictObject({
    policyId,
    versionId: policyId,
    number: z.number().int().min(1).max(1024),
    approvedVersion: z.string().max(100),
    effectiveFrom: policyDate,
    effectiveTo: endDate,
  })
  .refine(
    (identity) => identity.effectiveTo === null || identity.effectiveTo > identity.effectiveFrom,
  );
export function versionSchema(kind: FoundationPolicyKind) {
  return z
    .strictObject({
      identity: identitySchema,
      rules: rulesSchema(kind).nullable(),
      state: z.enum(['effective', 'published', 'unavailable']),
      unavailableReason: z.string().min(1).nullable(),
      approvalRequestId: policyId.nullable(),
      publishedByUserId: policyId,
      publishedAt: instant,
    })
    .refine((value) =>
      value.state === 'unavailable'
        ? value.rules === null && value.unavailableReason !== null
        : value.rules !== null &&
          value.unavailableReason === null &&
          value.approvalRequestId !== null &&
          printable(100).safeParse(value.identity.approvedVersion).success,
    );
}
export type FoundationPolicyVersion = z.infer<ReturnType<typeof versionSchema>>;
export function currentSchema(kind: FoundationPolicyKind, date?: string) {
  return z
    .strictObject({
      policy: z
        .strictObject({
          id: policyId,
          kind: z.literal(kind),
          code: z.literal(`HR-FOUNDATION-${kind.toUpperCase()}`),
          version: rowVersion,
          latestVersion: z.number().int().min(0).max(1024),
          isActive: z.boolean(),
        })
        .nullable(),
      selectedDate: policyDate,
      state: z.enum(['effective', 'none', 'unavailable']),
      effective: versionSchema(kind).nullable(),
      unavailableReason: z.string().min(1).nullable(),
    })
    .refine(
      (value) => !date || value.selectedDate === date,
      'Response date does not match the selected date.',
    )
    .refine((value) => {
      if (value.state === 'effective')
        return (
          value.policy?.isActive &&
          value.effective?.state === 'effective' &&
          value.unavailableReason === null &&
          sameId(value.policy.id, value.effective.identity.policyId) &&
          value.effective.identity.number <= value.policy.latestVersion &&
          value.effective.identity.effectiveFrom <= value.selectedDate &&
          (value.effective.identity.effectiveTo === null ||
            value.effective.identity.effectiveTo > value.selectedDate)
        );
      if (value.state === 'none')
        return (
          value.effective === null &&
          value.unavailableReason === null &&
          (!value.policy || value.policy.latestVersion === 0)
        );
      return value.effective === null && value.unavailableReason !== null;
    }, 'Policy resolution is inconsistent.');
}
export type FoundationPolicyCurrent = z.infer<ReturnType<typeof currentSchema>>;
function pageSchema<T extends z.ZodType>(item: T, page: number) {
  return z
    .strictObject({
      items: z.array(item).max(25),
      totalCount: z.number().int().nonnegative(),
      page: z.literal(page),
      pageSize: z.literal(25),
    })
    .refine(
      (value) =>
        value.items.length === Math.min(25, Math.max(0, value.totalCount - (page - 1) * 25)),
      'The server page is incomplete.',
    );
}
export function historySchema(kind: FoundationPolicyKind, page: number) {
  return pageSchema(versionSchema(kind), page).refine(
    (value) =>
      value.items.every(
        (row, index, rows) =>
          sameId(row.identity.policyId, rows[0]?.identity.policyId) &&
          row.state !== 'effective' &&
          (index === 0 || rows[index - 1]!.identity.number > row.identity.number),
      ) &&
      new Set(value.items.map((row) => row.identity.versionId.toLowerCase())).size ===
        value.items.length,
  );
}
export const requestStateLabels = [
  'Pending',
  'Approved',
  'Returned',
  'Rejected',
  'Cancelled',
] as const;
export const decisionNames = ['Approved', 'Returned', 'Rejected'] as const;
export type PolicyDecision = (typeof decisionNames)[number];
export function requestSchema(kind: FoundationPolicyKind) {
  return z
    .strictObject({
      id: policyId,
      policyId,
      kind: z.literal(kind),
      state: z.number().int().min(0).max(4),
      requesterId: policyId,
      requestedAt: instant,
      closedAt: instant.nullable(),
      version: rowVersion,
    })
    .refine((value) =>
      value.state === 0
        ? value.closedAt === null
        : value.closedAt !== null && Date.parse(value.closedAt) >= Date.parse(value.requestedAt),
    );
}
export type FoundationPolicyRequest = z.infer<ReturnType<typeof requestSchema>>;
export function requestsSchema(kind: FoundationPolicyKind, page: number, state?: number) {
  return pageSchema(requestSchema(kind), page).refine(
    (value) =>
      value.items.every(
        (row) =>
          sameId(row.policyId, value.items[0]?.policyId) &&
          (state === undefined || row.state === state),
      ) && new Set(value.items.map((row) => row.id.toLowerCase())).size === value.items.length,
  );
}
export function detailSchema(
  kind: FoundationPolicyKind,
  requestId: string,
  expectedPolicyId?: string,
) {
  return z
    .strictObject({
      request: requestSchema(kind),
      proposal: proposalSchema(kind),
      approvedAuthorityVersion: printable(100),
      decisions: z
        .array(
          z.strictObject({
            id: policyId,
            decision: z.number().int().min(1).max(3),
            actorId: policyId,
            roleCode: z.enum(['ADMIN', 'SUPERADMIN']),
            reason: policyReason,
            decidedAt: instant,
          }),
        )
        .max(1),
    })
    .refine(
      (value) =>
        sameId(value.request.id, requestId) &&
        (!expectedPolicyId || sameId(value.request.policyId, expectedPolicyId)),
      'Request does not match this resource.',
    )
    .refine(
      (value) =>
        value.request.state === 0 || value.request.state === 4
          ? value.decisions.length === 0
          : value.decisions.length === 1 &&
            value.decisions[0]!.decision === value.request.state &&
            !sameId(value.decisions[0]!.actorId, value.request.requesterId) &&
            Date.parse(value.decisions[0]!.decidedAt) === Date.parse(value.request.closedAt!),
      'Decision evidence is inconsistent.',
    );
}
export type FoundationPolicyDetail = z.infer<ReturnType<typeof detailSchema>>;
export const decisionSchema = z.strictObject({
  expectedPolicyVersion: rowVersion,
  expectedRequestVersion: rowVersion,
  expectedLatestVersion: z.number().int().min(0).max(1023),
  decision: z.enum(decisionNames),
  reason: policyReason,
});
export const cancelSchema = z.strictObject({
  expectedPolicyVersion: rowVersion,
  expectedRequestVersion: rowVersion,
  reason: policyReason,
});
export type PolicyOperation = 'request' | 'decision' | 'cancel';
export function writeSchema(
  operation: PolicyOperation,
  expectedPolicyId?: string,
  requestId?: string,
  decision?: PolicyDecision,
) {
  return z
    .strictObject({
      policyId,
      requestId: policyId,
      state: z.enum(['pending', 'approved', 'returned', 'rejected', 'cancelled']),
      versionId: policyId.nullish(),
      decisionId: policyId.nullish(),
    })
    .refine(
      (result) =>
        (!expectedPolicyId || sameId(result.policyId, expectedPolicyId)) &&
        (!requestId || sameId(result.requestId, requestId)) &&
        (operation === 'request'
          ? result.state === 'pending' && !result.versionId && !result.decisionId
          : operation === 'cancel'
            ? result.state === 'cancelled' && !result.versionId && !result.decisionId
            : result.state === decision?.toLowerCase() &&
              Boolean(result.decisionId) &&
              (decision === 'Approved' ? Boolean(result.versionId) : !result.versionId)),
      'Mutation result does not match this action.',
    );
}
export type FoundationPolicyWriteResult = z.infer<ReturnType<typeof writeSchema>>;
