import { z } from 'zod';
import { date, id, reason } from '../../schemas/common';
import { readableLeavePolicy, type PublishedLeavePolicy } from '../leave-benefits/api';
import { configurableLeaveSettings, leaveKind, leaveSettings } from './contracts';

const unique = <T>(values: T[]) => new Set(values).size === values.length;
const referenceId = id
  .refine(
    (value) => value !== '00000000-0000-0000-0000-000000000000',
    'A non-empty ID is required.',
  )
  .transform((value) => value.toLowerCase());
const eligibleKind = leaveKind.refine((value) => value !== 5, 'Site injury rest is not eligible.');
const eligibleKinds = z.array(eligibleKind).min(1).max(7).refine(unique, 'Kinds must be distinct.');
const integer = (min: number, max: number) => z.number().int().min(min).max(max);
const kpiPercent = z.number().min(0).max(100);

export const encashmentSettings = z.strictObject({
  eligibleKinds,
  minimumServiceMonths: integer(0, 1200),
  minimumKpiPercent: kpiPercent,
  maximumMinutesPerYear: integer(1, 527040),
  salaryDivisorDays: integer(1, 366),
  rateMultiplier: z.number().positive().max(100),
  amountDecimalPlaces: integer(0, 2),
  rounding: z.union([z.literal(0), z.literal(1)]),
  currency: z.string().regex(/^[A-Z]{3}$/, 'Enter the configured three-letter uppercase currency.'),
  valuationDate: z.literal(1),
  requestWindowDaysAfterYearEnd: integer(1, 3660),
});
export const recoverySettings = z
  .strictObject({
    eligibleKinds,
    eligibleTreatments: z
      .array(z.union([z.literal(1), z.literal(2)]))
      .min(1)
      .max(2)
      .refine(unique),
    maximumMinutesPerLeave: integer(1, 527040),
    maximumMinutesPerDay: integer(1, 1440),
    maximumDaysBeforeLeave: integer(0, 366),
    maximumDaysAfterLeave: integer(0, 366),
  })
  .refine((value) => value.maximumDaysBeforeLeave + value.maximumDaysAfterLeave > 0, {
    path: ['maximumDaysAfterLeave'],
    message: 'At least one recovery window must extend beyond zero days.',
  });
export const benefitSettings = z.strictObject({
  eligibleKind,
  minimumServiceMonths: integer(0, 1200),
  minimumKpiPercent: kpiPercent,
  awardMinutes: integer(1, 527040),
  targetYearOffset: integer(1, 5),
  awardWindowDaysAfterSourceEnd: integer(1, 3660),
  encashmentEligible: z.boolean(),
});
const pendingOrConfigured = z.union([z.literal(0), z.literal(1)]);
export const recoveryFulfilmentSettings = z
  .strictObject({
    qualifyingWork: pendingOrConfigured,
    creditBasis: pendingOrConfigured,
    reviewAuthority: pendingOrConfigured,
    allowPartialFulfilment: z.boolean(),
    minimumClaimMinutes: integer(1, 1440),
    maximumClaimMinutes: integer(1, 1440),
    maximumClaimsPerCommitment: integer(1, 1000),
    submissionWindowDays: integer(0, 366),
    parentChangeHandling: pendingOrConfigured,
    sourceChangeHandling: pendingOrConfigured,
  })
  .refine((value) => value.maximumClaimMinutes >= value.minimumClaimMinutes, {
    path: ['maximumClaimMinutes'],
    message: 'Maximum claim minutes must be at least minimum claim minutes.',
  });

const extendedShape = {
  ...configurableLeaveSettings.shape,
  eligibleStaffCategoryIds: z.array(referenceId).min(1).max(100).refine(unique),
  holidayCalendarId: referenceId,
  settingsVersion: z.union([z.literal(3), z.literal(4)]),
  encashment: encashmentSettings.nullable().optional(),
  recovery: recoverySettings.nullable().optional(),
  benefits: benefitSettings.nullable().optional(),
  recoveryFulfilment: recoveryFulfilmentSettings.nullable().optional(),
};
const extendedObject = z.strictObject(extendedShape);
function validateDependencies(value: z.infer<typeof extendedObject>, context: z.RefinementCtx) {
  const issue = (path: string[], message: string) =>
    context.addIssue({ code: 'custom', path, message });
  if (value.yearStartDay > new Date(Date.UTC(2001, value.yearStartMonth, 0)).getUTCDate())
    issue(['yearStartDay'], 'The leave-year boundary must be valid in every year.');
  if (!value.encashment && !value.recovery && !value.benefits && !value.recoveryFulfilment)
    issue([], 'V4 requires at least one explicitly configured capability.');
  if (value.settingsVersion !== 4 && (value.benefits || value.recoveryFulfilment))
    issue(['settingsVersion'], 'Benefits and recovery fulfilment require v4.');
  if (value.recoveryFulfilment && !value.recovery)
    issue(['recoveryFulfilment'], 'Recovery fulfilment requires recovery settings.');
  if (value.benefits && !value.paidKinds.includes(value.benefits.eligibleKind))
    issue(['benefits', 'eligibleKind'], 'The benefit kind must be included in paid kinds.');
  if (
    value.benefits?.encashmentEligible &&
    !value.encashment?.eligibleKinds.includes(value.benefits.eligibleKind)
  )
    issue(
      ['benefits', 'encashmentEligible'],
      'Cash-eligible benefits require encashment configured for the same kind.',
    );
}
const editableExtendedSettings = extendedObject.superRefine(validateDependencies);
export const v4Settings = z
  .strictObject({ ...extendedShape, settingsVersion: z.literal(4) })
  .superRefine(validateDependencies);
export type V4Settings = z.infer<typeof v4Settings>;

export const publishV4Input = z
  .strictObject({
    code: reason(40)
      .regex(/^[A-Za-z0-9][A-Za-z0-9_.-]{0,39}$/)
      .transform((value) => value.toUpperCase()),
    name: reason(200),
    effectiveFrom: date.refine((value) => value >= '1901-01-01' && value <= '9997-12-31'),
    effectiveTo: date.refine((value) => value <= '9998-12-31'),
    settings: v4Settings,
    reason: reason(),
  })
  .refine((value) => value.effectiveTo > value.effectiveFrom, {
    path: ['effectiveTo'],
    message: 'Exclusive end must follow effective from.',
  });

export const publishBaseInput = z
  .strictObject({
    ...publishV4Input.shape,
    settings: configurableLeaveSettings.transform((settings) => ({
      ...settings,
      eligibleStaffCategoryIds: settings.eligibleStaffCategoryIds.map((value) =>
        value.toLowerCase(),
      ),
      holidayCalendarId: settings.holidayCalendarId.toLowerCase(),
    })),
  })
  .refine((value) => value.effectiveTo > value.effectiveFrom, {
    path: ['effectiveTo'],
    message: 'Exclusive end must follow effective from.',
  });

export const capabilities = ['encashment', 'recovery', 'benefits', 'recoveryFulfilment'] as const;
export type Capability = (typeof capabilities)[number];
export type CapabilityChoice = '' | 'enabled' | 'unconfigured';
export type PolicyDraft = {
  fields: Record<string, string>;
  selections: Record<string, number[]>;
  capabilities: Record<Capability, CapabilityChoice>;
  copiedNulls: Capability[];
  paidReviewed: boolean;
  copyProblem: string;
};

export function policyDraft(source?: PublishedLeavePolicy): PolicyDraft {
  const draft: PolicyDraft = {
    fields: {},
    selections: {},
    capabilities: { encashment: '', recovery: '', benefits: '', recoveryFulfilment: '' },
    copiedNulls: [],
    paidReviewed: false,
    copyProblem: '',
  };
  if (!source) return draft;
  const sourceSchema =
    source.settings.settingsVersion === 3 || source.settings.settingsVersion === 4
      ? editableExtendedSettings
      : leaveSettings;
  const result = sourceSchema.safeParse(source.settings);
  if (!result.success) {
    draft.copyProblem = `This revision contains unsupported or invalid settings; copying is disabled. ${result.error.issues.map((issue) => `${issue.path.join('.') || 'settings'}: ${issue.message}`).join('; ')}`;
    return draft;
  }
  draft.fields.name = source.name;
  draft.fields.effectiveFrom = source.effectiveFrom;
  draft.fields.effectiveTo = source.effectiveTo ?? '';
  const retainedSettings: Record<string, unknown> = source.settings;
  // The endpoint creates a separate immutable publication. Code and reason are never inherited.
  for (const [key, value] of Object.entries(retainedSettings)) {
    if (key === 'settingsVersion' || capabilities.includes(key as Capability)) continue;
    if (key === 'eligibleStaffCategoryIds') draft.fields[key] = (value as string[]).join(', ');
    else if (Array.isArray(value)) draft.selections[key] = [...value] as number[];
    else if (value != null) draft.fields[key] = String(value);
  }
  for (const capability of capabilities) {
    const block = retainedSettings[capability];
    draft.capabilities[capability] = block == null ? 'unconfigured' : 'enabled';
    if (block === null) draft.copiedNulls.push(capability);
    if (block && typeof block === 'object' && !Array.isArray(block)) {
      for (const [key, value] of Object.entries(block)) {
        const field = `${capability}.${key}`;
        if (Array.isArray(value)) draft.selections[field] = [...value] as number[];
        else draft.fields[field] = String(value);
      }
    }
  }
  return draft;
}

export type NumberField = { key: string; label: string; min: number; max: number; step?: 'any' };
export const baseNumbers: NumberField[] = [
  { key: 'annualDays', label: 'Annual days', min: 0, max: 2147483647 },
  { key: 'sickDays', label: 'Sick days', min: 0, max: 2147483647 },
  { key: 'casualDays', label: 'Casual days', min: 0, max: 2147483647 },
  { key: 'minimumServiceMonths', label: 'Minimum service months', min: 0, max: 2147483647 },
  { key: 'standardDayMinutes', label: 'Standard day minutes', min: 1, max: 1440 },
  { key: 'yearStartMonth', label: 'Leave year starting month', min: 1, max: 12 },
  { key: 'yearStartDay', label: 'Leave year starting day', min: 1, max: 31 },
];
export const capabilityNumbers: Record<Capability, NumberField[]> = {
  encashment: [
    { key: 'minimumServiceMonths', label: 'Minimum service months', min: 0, max: 1200 },
    { key: 'minimumKpiPercent', label: 'Minimum KPI percent', min: 0, max: 100, step: 'any' },
    { key: 'maximumMinutesPerYear', label: 'Maximum minutes per year', min: 1, max: 527040 },
    { key: 'salaryDivisorDays', label: 'Salary divisor days', min: 1, max: 366 },
    {
      key: 'rateMultiplier',
      label: 'Rate multiplier (greater than zero)',
      min: 0,
      max: 100,
      step: 'any',
    },
    { key: 'amountDecimalPlaces', label: 'Amount decimal places', min: 0, max: 2 },
    {
      key: 'requestWindowDaysAfterYearEnd',
      label: 'Request window days after year end',
      min: 1,
      max: 3660,
    },
  ],
  recovery: [
    { key: 'maximumMinutesPerLeave', label: 'Maximum minutes per leave', min: 1, max: 527040 },
    { key: 'maximumMinutesPerDay', label: 'Maximum minutes per day', min: 1, max: 1440 },
    { key: 'maximumDaysBeforeLeave', label: 'Maximum days before leave', min: 0, max: 366 },
    { key: 'maximumDaysAfterLeave', label: 'Maximum days after leave', min: 0, max: 366 },
  ],
  benefits: [
    { key: 'minimumServiceMonths', label: 'Minimum service months', min: 0, max: 1200 },
    { key: 'minimumKpiPercent', label: 'Minimum KPI percent', min: 0, max: 100, step: 'any' },
    { key: 'awardMinutes', label: 'Award minutes', min: 1, max: 527040 },
    { key: 'targetYearOffset', label: 'Target year offset', min: 1, max: 5 },
    {
      key: 'awardWindowDaysAfterSourceEnd',
      label: 'Award window days after source end',
      min: 1,
      max: 3660,
    },
  ],
  recoveryFulfilment: [
    { key: 'minimumClaimMinutes', label: 'Minimum claim minutes', min: 1, max: 1440 },
    { key: 'maximumClaimMinutes', label: 'Maximum claim minutes', min: 1, max: 1440 },
    {
      key: 'maximumClaimsPerCommitment',
      label: 'Maximum claims per commitment',
      min: 1,
      max: 1000,
    },
    { key: 'submissionWindowDays', label: 'Submission window days', min: 0, max: 366 },
  ],
};

export function inputFromDraft(draft: PolicyDraft, baseOnly = false) {
  if (draft.copyProblem) throw new Error(draft.copyProblem);
  if (!draft.paidReviewed)
    throw new Error('Confirm paid kinds, including an explicit empty selection.');
  const number = (key: string) => {
    const value = draft.fields[key];
    if (value === undefined || value.trim() === '')
      throw new Error(`${key}: an explicit number is required.`);
    return Number(value);
  };
  const boolean = (key: string) => {
    const value = draft.fields[key];
    if (value !== 'true' && value !== 'false')
      throw new Error(`${key}: choose Yes or No explicitly.`);
    return value === 'true';
  };
  const numbers = (fields: NumberField[], prefix = '') =>
    Object.fromEntries(fields.map(({ key }) => [key, number(prefix + key)]));
  const blocks: Partial<Record<Capability, unknown>> = {};
  for (const capability of baseOnly ? [] : capabilities) {
    const choice = draft.capabilities[capability];
    if (choice === 'unconfigured') {
      if (draft.copiedNulls.includes(capability)) blocks[capability] = null;
      continue;
    }
    if (choice !== 'enabled')
      throw new Error(`${capability}: choose Enabled or Unconfigured explicitly.`);
    const prefix = `${capability}.`;
    const values = numbers(capabilityNumbers[capability], prefix);
    if (capability === 'encashment')
      blocks.encashment = {
        ...values,
        eligibleKinds: draft.selections[prefix + 'eligibleKinds'] ?? [],
        rounding: number(prefix + 'rounding'),
        currency: draft.fields[prefix + 'currency'],
        valuationDate: number(prefix + 'valuationDate'),
      };
    if (capability === 'recovery')
      blocks.recovery = {
        ...values,
        eligibleKinds: draft.selections[prefix + 'eligibleKinds'] ?? [],
        eligibleTreatments: draft.selections[prefix + 'eligibleTreatments'] ?? [],
      };
    if (capability === 'benefits')
      blocks.benefits = {
        ...values,
        eligibleKind: number(prefix + 'eligibleKind'),
        encashmentEligible: boolean(prefix + 'encashmentEligible'),
      };
    if (capability === 'recoveryFulfilment')
      blocks.recoveryFulfilment = {
        ...values,
        qualifyingWork: number(prefix + 'qualifyingWork'),
        creditBasis: number(prefix + 'creditBasis'),
        reviewAuthority: number(prefix + 'reviewAuthority'),
        allowPartialFulfilment: boolean(prefix + 'allowPartialFulfilment'),
        parentChangeHandling: number(prefix + 'parentChangeHandling'),
        sourceChangeHandling: number(prefix + 'sourceChangeHandling'),
      };
  }
  return (baseOnly ? publishBaseInput : publishV4Input).parse({
    code: draft.fields.code,
    name: draft.fields.name,
    effectiveFrom: draft.fields.effectiveFrom,
    effectiveTo: draft.fields.effectiveTo,
    reason: draft.fields.reason,
    settings: {
      ...numbers(baseNumbers),
      settingsVersion: baseOnly ? 2 : 4,
      eligibleStaffCategoryIds: (draft.fields.eligibleStaffCategoryIds ?? '')
        .split(/[\s,]+/)
        .filter(Boolean),
      holidayCalendarId: draft.fields.holidayCalendarId,
      holidayTreatment: number('holidayTreatment'),
      paidKinds: draft.selections.paidKinds ?? [],
      ...blocks,
    },
  });
}

function sameValue(left: unknown, right: unknown): boolean {
  if (Array.isArray(left) && Array.isArray(right))
    return (
      left.length === right.length && left.every((item, index) => sameValue(item, right[index]))
    );
  if (left && right && typeof left === 'object' && typeof right === 'object') {
    const a = left as Record<string, unknown>,
      b = right as Record<string, unknown>;
    const keys = Object.keys(a).filter((key) => a[key] != null);
    return (
      keys.length === Object.keys(b).filter((key) => b[key] != null).length &&
      keys.every((key) => sameValue(a[key], b[key]))
    );
  }
  return left === right;
}
export function publishV4Intent(input: unknown) {
  return publicationIntent(input, false);
}
export function publishBaseIntent(input: unknown) {
  return publicationIntent(input, true);
}
function publicationIntent(input: unknown, baseOnly: boolean) {
  const body = (baseOnly ? publishBaseInput : publishV4Input).parse(input);
  const expected = structuredClone(body);
  const schema = readableLeavePolicy.superRefine((response, context) => {
    const settings = (baseOnly ? configurableLeaveSettings : v4Settings).safeParse(
      response.settings,
    );
    if (
      !settings.success ||
      !sameValue(settings.data, expected.settings) ||
      response.code !== expected.code ||
      response.name !== expected.name ||
      response.effectiveFrom !== expected.effectiveFrom ||
      response.effectiveTo !== expected.effectiveTo ||
      response.authority !== 'DirectHr'
    )
      context.addIssue({
        code: 'custom',
        message: 'The returned publication does not match the submitted settings.',
      });
  });
  return { path: '/hr/leave-settings/policies', method: 'POST' as const, body, schema };
}
