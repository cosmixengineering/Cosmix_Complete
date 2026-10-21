import { z } from 'zod';

export const taxonomyId = z
  .uuid()
  .refine((value) => value !== '00000000-0000-0000-0000-000000000000');
export const sameId = (left: string, right: string) => left.toLowerCase() === right.toLowerCase();
export const rowVersion = z.string().regex(/^[A-Za-z0-9+/]{10}[AEIMQUYcgkosw048]=$/);
export const categoryCode = z.string().regex(/^[A-Z0-9][A-Z0-9_.-]{0,29}$/);
const day = z.iso.date().refine((value) => value !== '0001-01-01' && value !== '9999-12-31');
const instant = z.iso.datetime({ offset: true });
const hash = z.string().regex(/^[A-Fa-f0-9]{64}$/);
export const text = (maximum: number) =>
  z
    .string()
    .min(1)
    .max(maximum)
    .refine(
      (value) =>
        value.trim().length > 0 &&
        ![...value].some(
          (c) => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159),
        ),
      'Enter printable text without line breaks or control characters.',
    );
export const policyUses = [
  { value: 1, label: 'Attendance' },
  { value: 2, label: 'Leave' },
  { value: 3, label: 'Meal' },
  { value: 4, label: 'Deployment' },
  { value: 5, label: 'Fuel' },
] as const;
const location = z.union([z.literal(1), z.literal(2)]);
const referenceSchema = z
  .strictObject({
    use: z.union([z.literal(1), z.literal(2), z.literal(3), z.literal(4), z.literal(5)]),
    applicability: z.union([z.literal(0), z.literal(1), z.literal(2)]),
    policyVersionId: taxonomyId.nullable(),
  })
  .refine(
    (r) => (r.applicability === 2) === (r.policyVersionId !== null),
    'Required applicability needs an immutable policy version; other choices cannot retain one.',
  );
export const mappingSchema = z
  .strictObject({
    code: categoryCode,
    name: text(150),
    workLocation: location,
    scheduleBasis: location,
    requireSiteBeforeActivation: z.boolean(),
    outsideOvertimeInheritance: z.boolean(),
    policyReferences: z.array(referenceSchema).length(5),
  })
  .superRefine((m, ctx) => {
    if (
      m.workLocation !== m.scheduleBasis ||
      (m.workLocation === 1 && m.requireSiteBeforeActivation)
    )
      ctx.addIssue({
        code: 'custom',
        message: 'Location and schedule must agree; Office cannot require a site.',
      });
    if (new Set(m.policyReferences.map((r) => r.use)).size !== 5)
      ctx.addIssue({ code: 'custom', message: 'Select each policy use exactly once.' });
    if (
      (m.code === 'OFFICE' && (m.workLocation !== 1 || m.outsideOvertimeInheritance)) ||
      (m.code === 'SITE' &&
        (m.workLocation !== 2 || m.requireSiteBeforeActivation || !m.outsideOvertimeInheritance))
    )
      ctx.addIssue({
        code: 'custom',
        message: 'The confirmed OFFICE/SITE baseline must be retained.',
      });
  });
export const publishedMappingSchema = mappingSchema.refine(
  (m) => m.policyReferences.every((r) => r.applicability !== 0),
  'Choose explicit applicability for every policy use.',
);
export function retainedRulesLength(rules: { categories: readonly { name: string }[] }) {
  // Names are the only free-text JSON values here. Count the backend's default encoder
  // without confusing a name's escaped quote with the JSON object's structural quotes.
  return (
    JSON.stringify(rules).length +
    rules.categories.reduce((extra, { name }) => {
      let encodedLength = 2;
      for (let index = 0; index < name.length; index++) {
        const character = name[index]!;
        const code = name.charCodeAt(index);
        encodedLength +=
          character === '\\' || [8, 9, 10, 12, 13].includes(code)
            ? 2
            : code < 32 || code >= 127 || '<>&\'"+`'.includes(character)
              ? 6
              : 1;
      }
      return extra + encodedLength - JSON.stringify(name).length;
    }, 0)
  );
}
export const rulesSchema = z
  .strictObject({
    schemaVersion: z.literal(1),
    categories: z.array(publishedMappingSchema).min(2).max(100),
  })
  .superRefine((r, ctx) => {
    const codes = r.categories.map((m) => m.code);
    if (
      new Set(codes).size !== codes.length ||
      !codes.includes('OFFICE') ||
      !codes.includes('SITE')
    )
      ctx.addIssue({
        code: 'custom',
        message: 'Keep OFFICE and SITE and use distinct category codes.',
      });
    if (retainedRulesLength(r) > 32000)
      ctx.addIssue({
        code: 'custom',
        message: 'The complete rules exceed the supported policy size.',
      });
  });
export type Mapping = z.infer<typeof mappingSchema>;
export type Rules = z.infer<typeof rulesSchema>;
export const policySchema = z.strictObject({
  id: taxonomyId,
  code: z.literal('HR-TAXONOMY-STAFF'),
  revision: z.number().int().positive(),
  version: rowVersion,
});
export type TaxonomyPolicy = z.infer<typeof policySchema>;
export const requestSchema = z
  .strictObject({
    id: taxonomyId,
    policyId: taxonomyId,
    state: z.enum(['Pending', 'Approved', 'Returned', 'Rejected', 'Cancelled']),
    version: rowVersion,
    requesterId: taxonomyId,
    requestedAt: instant,
    closedAt: instant.nullable(),
  })
  .refine(
    (r) =>
      r.state === 'Pending'
        ? r.closedAt === null
        : r.closedAt !== null && Date.parse(r.closedAt) >= Date.parse(r.requestedAt),
    'Request state and closing time do not agree.',
  );
export type TaxonomyRequest = z.infer<typeof requestSchema>;
function pageSchema<T extends z.ZodType<{ id: string; policyId: string }>>(
  schema: T,
  policyId: string | null,
  page: number,
  pageSize: number,
) {
  return z
    .strictObject({
      items: z.array(schema),
      totalCount: z.number().int().nonnegative(),
      page: z.literal(page),
      pageSize: z.literal(pageSize),
    })
    .refine(
      (p) =>
        p.items.length === Math.min(pageSize, Math.max(0, p.totalCount - (page - 1) * pageSize)) &&
        new Set(p.items.map((r) => r.id.toLowerCase())).size === p.items.length &&
        p.items.every((r) => policyId !== null && sameId(r.policyId, policyId)),
      'The page metadata or resource identity does not match this read.',
    );
}
export const requestPageSchema = (policyId: string | null, page: number, pageSize = 25) =>
  pageSchema(requestSchema, policyId, page, pageSize);
export const versionSchema = z
  .strictObject({
    id: taxonomyId,
    policyId: taxonomyId,
    number: z.number().int().positive(),
    effectiveFrom: day,
    effectiveTo: day.nullable(),
    rulesJson: z.string().min(1).max(32000),
    configurationSha256: hash,
    publisherId: taxonomyId,
    publishedAt: instant,
    evidenceState: z.enum(['verified', 'invalid_evidence']),
  })
  .superRefine((v, ctx) => {
    if (v.effectiveTo !== null && v.effectiveTo <= v.effectiveFrom)
      ctx.addIssue({ code: 'custom', message: 'Invalid retained effective period.' });
    if (v.evidenceState === 'verified') {
      try {
        rulesSchema.parse(JSON.parse(v.rulesJson));
      } catch {
        ctx.addIssue({
          code: 'custom',
          message: 'Verified publication contains unsupported rules.',
        });
      }
    }
  });
export type TaxonomyVersion = z.infer<typeof versionSchema>;
export const historyPageSchema = (policyId: string | null, page: number, pageSize = 25) =>
  pageSchema(versionSchema, policyId, page, pageSize).refine(
    (p) => new Set(p.items.map((v) => v.number)).size === p.items.length,
    'Duplicate publication numbers.',
  );
export function detailSchema(requestId: string, policyId: string) {
  return z
    .strictObject({
      request: requestSchema,
      effectiveFrom: day,
      effectiveTo: day.nullable(),
      rules: rulesSchema,
      reason: text(500),
      writtenEvidence: text(8000),
      authorityVersion: text(100),
    })
    .refine(
      (d) =>
        sameId(d.request.id, requestId) &&
        sameId(d.request.policyId, policyId) &&
        (d.effectiveTo === null || d.effectiveTo > d.effectiveFrom),
      'The proposal does not match the selected request or period.',
    );
}
export function resolutionSchema(code: string, on: string) {
  return z
    .strictObject({
      state: z.enum([
        'effective',
        'legacy_baseline',
        'category_not_configured',
        'taxonomy_not_effective',
        'ambiguous_taxonomy',
        'invalid_taxonomy_evidence',
        'taxonomy_history_conflict',
        'invalid_taxonomy_rules',
        'mapping_changes_during_interval',
      ]),
      mapping: mappingSchema.nullable(),
      policyVersionId: taxonomyId.nullable(),
      fingerprint: hash,
      baseline: z.boolean(),
      effectiveFrom: day.nullable(),
      effectiveTo: day.nullable(),
      available: z.boolean(),
    })
    .superRefine((r, ctx) => {
      const valid =
        r.state === 'effective'
          ? r.available &&
            !r.baseline &&
            r.mapping !== null &&
            r.mapping.policyReferences.every((p) => p.applicability !== 0) &&
            r.policyVersionId !== null &&
            r.effectiveFrom !== null &&
            r.effectiveFrom <= on &&
            (r.effectiveTo === null || on < r.effectiveTo)
          : r.state === 'legacy_baseline'
            ? r.available &&
              r.baseline &&
              r.mapping !== null &&
              ['OFFICE', 'SITE'].includes(r.mapping.code) &&
              r.mapping.policyReferences.every((p) => p.applicability === 0) &&
              r.policyVersionId === null &&
              r.effectiveFrom === null &&
              r.effectiveTo === null
            : !r.available &&
              !r.baseline &&
              r.mapping === null &&
              r.policyVersionId === null &&
              r.effectiveFrom === null &&
              r.effectiveTo === null;
      if (!valid || (r.mapping && r.mapping.code !== code))
        ctx.addIssue({
          code: 'custom',
          message: 'The resolution does not match this category/date or supported state.',
        });
    });
}
export function businessDate(now = new Date()) {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Karachi',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}
export function proposalSchema(policy: TaxonomyPolicy | null, today = businessDate()) {
  return z
    .strictObject({
      expectedPolicyVersion: rowVersion.nullable(),
      expectedRevision: z.number().int().nonnegative(),
      effectiveFrom: day,
      effectiveTo: day.nullable(),
      rules: rulesSchema,
      reason: text(500),
      writtenEvidence: text(8000),
    })
    .refine(
      (p) =>
        p.expectedPolicyVersion === (policy?.version ?? null) &&
        p.expectedRevision === (policy?.revision ?? 0) &&
        p.effectiveFrom > today &&
        (p.effectiveTo === null || p.effectiveTo > p.effectiveFrom),
      'Use the current policy revision and a future effective period.',
    );
}
export const decisionSchema = z.strictObject({
  expectedPolicyVersion: rowVersion,
  expectedRequestVersion: rowVersion,
  decision: z.union([z.literal(1), z.literal(2), z.literal(3)]),
  reason: text(500),
  writtenEvidence: text(8000),
});
export const cancelSchema = z.strictObject({
  expectedPolicyVersion: rowVersion,
  expectedRequestVersion: rowVersion,
  reason: text(500),
  writtenEvidence: text(8000),
});
export type WriteExpectation =
  | { action: 'request'; policy: TaxonomyPolicy | null; actorId: string }
  | {
      action: 'decision' | 'cancel';
      policy: TaxonomyPolicy;
      request: TaxonomyRequest;
      decision?: 1 | 2 | 3;
    };
export function writeResultSchema(expected: WriteExpectation) {
  return z
    .strictObject({
      policy: policySchema,
      request: requestSchema,
      publishedVersionId: taxonomyId.nullable(),
    })
    .superRefine((r, ctx) => {
      const state =
        expected.action === 'request'
          ? 'Pending'
          : expected.action === 'cancel'
            ? 'Cancelled'
            : expected.decision === 1
              ? 'Approved'
              : expected.decision === 2
                ? 'Returned'
                : 'Rejected';
      const published = expected.action === 'decision' && expected.decision === 1;
      if (
        !sameId(r.policy.id, r.request.policyId) ||
        (expected.policy && !sameId(r.policy.id, expected.policy.id)) ||
        r.policy.revision !== (expected.policy?.revision ?? 0) + 1 ||
        r.policy.version === expected.policy?.version ||
        r.request.state !== state ||
        published !== (r.publishedVersionId !== null) ||
        (expected.action === 'request'
          ? !sameId(r.request.requesterId, expected.actorId)
          : !sameId(r.request.id, expected.request.id) ||
            !sameId(r.request.requesterId, expected.request.requesterId) ||
            r.request.requestedAt !== expected.request.requestedAt ||
            r.request.version === expected.request.version)
      )
        ctx.addIssue({
          code: 'custom',
          message: 'The write receipt does not match the exact action or resource versions.',
        });
    });
}
