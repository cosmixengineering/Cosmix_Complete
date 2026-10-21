import { z } from 'zod';
import { decimalFits, intervalFits } from './decimals';

const resourceId = z.uuid().refine((value) => value !== '00000000-0000-0000-0000-000000000000');
const fingerprint = z.string().regex(/^[A-Fa-f0-9]{64}$/);
const text = (maximum: number) =>
  z
    .string()
    .min(1)
    .max(maximum)
    .refine(
      (value) =>
        value.trim() === value &&
        ![...value].some((character) => {
          const code = character.charCodeAt(0);
          return code < 32 || (code >= 127 && code <= 159);
        }),
      'Enter text without surrounding whitespace or control characters.',
    );
export const utcTimestamp = z.iso
  .datetime({ offset: true })
  .refine(
    (value) =>
      /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]00:00)$/.test(value) &&
      value.slice(0, 4) !== '0000' &&
      !/^0001-01-01T00:00:00(?:\.0+)?(?:Z|[+-]00:00)$/.test(value) &&
      !/^9999-12-31T23:59:59\.9999999(?:Z|[+-]00:00)$/.test(value),
    'A valid UTC timestamp is required.',
  );

// Preserve .NET's sub-millisecond ordering for immutable effective-date chains.
export function utcTicks(value: string) {
  const fraction = /\.(\d+)/.exec(value)?.[1] ?? '';
  return BigInt(Date.parse(value.slice(0, 19) + 'Z')) * 10000n + BigInt(fraction.padEnd(7, '0'));
}
export function sameUtc(first: string, second: string) {
  return utcTicks(first) === utcTicks(second);
}

const limit = z.number().positive().refine(decimalFits, 'Unsupported decimal range.').nullable();
const riskMapping = z.strictObject({
  observationCode: text(100),
  riskKind: z.enum(['device_integrity', 'mock_location']),
});
export const settingsSchema = z
  .strictObject({
    duplicate: z.strictObject({ enabled: z.boolean(), toleranceSeconds: limit }),
    activeDuration: z.strictObject({ enabled: z.boolean(), maximumMinutes: limit }),
    travel: z.strictObject({
      enabled: z.boolean(),
      lookbackSeconds: limit,
      maximumMetersPerSecond: limit,
      maximumAccuracyMeters: limit,
    }),
    device: z.strictObject({
      enabled: z.boolean(),
      approvedCodeSetReference: text(200).nullable(),
      riskMappings: z.array(riskMapping).max(100).nullable(),
    }),
  })
  .superRefine((settings, ctx) => {
    function check(enabled: boolean, value: number | null, path: string[]) {
      if (enabled ? value === null : value !== null)
        ctx.addIssue({
          code: 'custom',
          path,
          message: enabled
            ? 'An explicit positive limit is required.'
            : 'Disabled groups must have null limits.',
        });
    }
    check(settings.duplicate.enabled, settings.duplicate.toleranceSeconds, [
      'duplicate',
      'toleranceSeconds',
    ]);
    check(settings.activeDuration.enabled, settings.activeDuration.maximumMinutes, [
      'activeDuration',
      'maximumMinutes',
    ]);
    for (const field of [
      'lookbackSeconds',
      'maximumMetersPerSecond',
      'maximumAccuracyMeters',
    ] as const)
      check(settings.travel.enabled, settings.travel[field], ['travel', field]);
    for (const [group, field, value] of [
      ['duplicate', 'toleranceSeconds', settings.duplicate.toleranceSeconds],
      ['travel', 'lookbackSeconds', settings.travel.lookbackSeconds],
    ] as const)
      if (value !== null && !intervalFits(value))
        ctx.addIssue({
          code: 'custom',
          path: [group, field],
          message: 'The interval exceeds the server TimeSpan range.',
        });
    const device = settings.device;
    if (device.enabled) {
      if (device.approvedCodeSetReference === null || !device.riskMappings?.length)
        ctx.addIssue({
          code: 'custom',
          path: ['device'],
          message: 'An approved code-set reference and mappings are required.',
        });
      if (
        new Set(device.riskMappings?.map((row) => row.observationCode)).size !==
        device.riskMappings?.length
      )
        ctx.addIssue({
          code: 'custom',
          path: ['device', 'riskMappings'],
          message: 'Observation codes must be unique (case-sensitive).',
        });
    } else if (device.approvedCodeSetReference !== null || device.riskMappings?.length) {
      ctx.addIssue({
        code: 'custom',
        path: ['device'],
        message: 'Disabled device rules cannot retain settings.',
      });
    }
  });
export type CaptureSettings = z.infer<typeof settingsSchema>;

export function canonicalSettings(settings: CaptureSettings) {
  const parsed = settingsSchema.parse(settings);
  return {
    ...parsed,
    device: {
      ...parsed.device,
      riskMappings:
        parsed.device.riskMappings === null
          ? null
          : [...parsed.device.riskMappings].sort((first, second) =>
              first.observationCode < second.observationCode
                ? -1
                : first.observationCode > second.observationCode
                  ? 1
                  : 0,
            ),
    },
  };
}

export const policySchema = z
  .strictObject({
    id: resourceId,
    revision: z.number().int().min(1).max(512),
    supersedesVersionId: resourceId.nullable(),
    effectiveFromUtc: utcTimestamp,
    publishedAtUtc: utcTimestamp,
    publishedBy: resourceId,
    approvalReference: text(200),
    settings: settingsSchema,
    reason: text(1000),
    sourceFingerprint: fingerprint,
  })
  .superRefine((row, ctx) => {
    if (
      !utcTimestamp.safeParse(row.effectiveFromUtc).success ||
      !utcTimestamp.safeParse(row.publishedAtUtc).success
    )
      return;
    if (
      (row.revision === 1 ? row.supersedesVersionId !== null : row.supersedesVersionId === null) ||
      row.supersedesVersionId?.toLowerCase() === row.id.toLowerCase()
    )
      ctx.addIssue({
        code: 'custom',
        path: ['supersedesVersionId'],
        message: 'Invalid immutable version linkage.',
      });
    if (utcTicks(row.effectiveFromUtc) < utcTicks(row.publishedAtUtc))
      ctx.addIssue({
        code: 'custom',
        path: ['effectiveFromUtc'],
        message: 'Effective time precedes publication.',
      });
  });
export type CapturePolicy = z.infer<typeof policySchema>;

const previewContract = z.strictObject({
  asOfUtc: utcTimestamp,
  latestRevision: z.number().int().min(0).max(512),
  timelineFingerprint: fingerprint,
  state: z.enum(['not_configured', 'approved_configured']),
  effective: policySchema.nullable(),
});
export type CapturePreview = z.infer<typeof previewContract>;
export function previewSchema(asOfUtc: string) {
  utcTimestamp.parse(asOfUtc);
  return previewContract.superRefine((value, ctx) => {
    if (!previewContract.safeParse(value).success) return;
    if (
      !sameUtc(value.asOfUtc, asOfUtc) ||
      (value.state === 'not_configured') !== (value.effective === null) ||
      (value.effective &&
        (value.effective.revision > value.latestRevision ||
          utcTicks(value.effective.effectiveFromUtc) > utcTicks(asOfUtc) ||
          utcTicks(value.effective.publishedAtUtc) > utcTicks(asOfUtc)))
    )
      ctx.addIssue({
        code: 'custom',
        message:
          'The policy preview does not match the requested UTC time and configuration state.',
      });
  });
}

const historyContract = z.strictObject({
  items: z.array(policySchema).max(100),
  totalCount: z.number().int().min(0).max(512),
  page: z.number().int().min(1).max(1000),
  pageSize: z.number().int().min(1).max(100),
});
export type CaptureHistory = z.infer<typeof historyContract>;
export function historySchema(page: number, pageSize = 25) {
  return historyContract.superRefine((value, ctx) => {
    if (!historyContract.safeParse(value).success) return;
    const offset = (page - 1) * pageSize;
    let valid =
      value.page === page &&
      value.pageSize === pageSize &&
      value.items.length === Math.min(pageSize, Math.max(0, value.totalCount - offset)) &&
      new Set(value.items.map((row) => row.id.toLowerCase())).size === value.items.length;
    value.items.forEach((row, index) => {
      if (row.revision !== value.totalCount - offset - index) valid = false;
      const older = value.items[index + 1];
      if (
        older &&
        (row.supersedesVersionId?.toLowerCase() !== older.id.toLowerCase() ||
          utcTicks(row.effectiveFromUtc) <= utcTicks(older.effectiveFromUtc) ||
          utcTicks(row.publishedAtUtc) < utcTicks(older.publishedAtUtc))
      )
        valid = false;
    });
    if (!valid)
      ctx.addIssue({
        code: 'custom',
        message:
          'Publication history does not match the requested page or immutable version chain.',
      });
  });
}

export const publicationInput = z.strictObject({
  effectiveFromUtc: utcTimestamp,
  expectedRevision: z.number().int().min(0).max(512),
  expectedTimelineFingerprint: fingerprint,
  approvalReference: text(200),
  settings: settingsSchema,
  reason: text(1000),
});
export type CapturePublication = z.infer<typeof publicationInput>;
export function publicationSchema(
  request: CapturePublication,
  predecessor: string | null,
  actor: string,
) {
  return policySchema.superRefine((row, ctx) => {
    if (!policySchema.safeParse(row).success) return;
    if (
      row.revision !== request.expectedRevision + 1 ||
      row.supersedesVersionId?.toLowerCase() !== predecessor?.toLowerCase() ||
      row.id.toLowerCase() === predecessor?.toLowerCase() ||
      row.publishedBy.toLowerCase() !== actor.toLowerCase() ||
      !sameUtc(row.effectiveFromUtc, request.effectiveFromUtc) ||
      row.approvalReference !== request.approvalReference ||
      row.reason !== request.reason ||
      JSON.stringify(canonicalSettings(row.settings)) !==
        JSON.stringify(canonicalSettings(request.settings))
    )
      ctx.addIssue({
        code: 'custom',
        message: 'The publication response does not acknowledge this exact reviewed request.',
      });
  });
}
