import { z } from 'zod';
import { id, version } from './contracts';
export const statutoryScheme = z
  .union([z.literal(1), z.literal(2), z.literal(3)])
  .transform((value) => ({ 1: 'EOBI', 2: 'PESSI', 3: 'SESSI' })[value]);
export const statutorySchemeCode = (value: string) =>
  ({ EOBI: 1, PESSI: 2, SESSI: 3 })[value as 'EOBI' | 'PESSI' | 'SESSI'];
export const account = z.object({
  id,
  code: z.string(),
  name: z.string(),
  kind: z.string(),
  isActive: z.boolean(),
  version,
});
export const accountingPeriod = z.object({
  id,
  periodStart: z.string(),
  periodEndExclusive: z.string(),
  currency: z.string(),
  state: z.string(),
  createdByUserId: z.string(),
  approvedByUserId: z.string().nullable(),
  version,
});
export const postingProfile = z.object({
  id,
  name: z.string(),
  currency: z.string(),
  effectiveFrom: z.string(),
  effectiveToExclusive: z.string().nullable(),
  salaryExpenseAccountCode: z.string(),
  salaryPayableAccountCode: z.string(),
  deductions: z.array(z.object({ component: z.string(), creditAccountCode: z.string() })),
  statutory: z.array(
    z.object({
      scheme: statutoryScheme,
      employerExpenseAccountCode: z.string(),
      payableAccountCode: z.string(),
    }),
  ),
  state: z.string(),
  createdByUserId: z.string(),
  approvedByUserId: z.string().nullable(),
  version,
});
export const accountingSetup = z.object({
  accounts: z.array(account),
  periods: z.array(accountingPeriod),
  profiles: z.array(postingProfile),
});
export const obligation = z.object({
  id,
  batchId: id,
  totalGrossAmount: z.number(),
  totalNetAmount: z.number(),
  currency: z.string(),
  state: z.string(),
  sourceFingerprint: z.string().nullable().optional(),
  totalDeductions: z.number().optional(),
  periodStart: z.string().nullable().optional(),
  periodEndExclusive: z.string().nullable().optional(),
});
export const journal = z.object({
  id,
  obligationId: id,
  kind: z.string(),
  debitAccountCode: z.string(),
  creditAccountCode: z.string(),
  amount: z.number(),
  currency: z.string(),
  description: z.string(),
  postingDate: z.string().nullable().optional(),
  accountingPeriodId: id.nullable().optional(),
  sourceKey: z.string().nullable().optional(),
  statutoryContributionId: id.nullable().optional(),
});
export const payment = z.object({
  id,
  obligationId: id,
  employeeId: id.nullable(),
  amount: z.number(),
  state: z.string(),
  paymentReference: z.string(),
});
export const entitlement = z.object({
  employeeId: id,
  netEntitlement: z.number(),
  disbursedAmount: z.number(),
  remainingBalance: z.number(),
});
export const contributionEntry = z.object({
  id,
  employeeId: id,
  registrationId: id,
  registrationNumber: z.string(),
  employeeContributionAmount: z.number(),
  employerContributionAmount: z.number(),
  evidenceReference: z.string(),
  evidenceSha256: z.string(),
});
export const contribution = z.object({
  id,
  scheme: z.string(),
  contributionMonth: z.string(),
  currency: z.string(),
  state: z.string(),
  employeeContributionAmount: z.number(),
  employerContributionAmount: z.number(),
  totalContributionAmount: z.number(),
  createdByUserId: z.string(),
  version: version.optional(),
  entries: z.array(contributionEntry).optional(),
  obligationId: id.nullable().optional(),
  postingJournalEntryId: id.nullable().optional(),
});
export const contributionPage = z.object({ items: z.array(contribution), totalCount: z.number() });
export const registration = z.object({
  id,
  employeeId: id,
  scheme: z.string(),
  registrationNumber: z.string(),
  registeredAt: z.string(),
  recordedAt: z.string(),
});
export const coverage = z.object({
  id,
  employeeId: id,
  effectiveFrom: z.string(),
  effectiveToExclusive: z.string().nullable(),
  schemes: z.array(statutoryScheme),
  evidenceReference: z.string(),
  evidenceSha256: z.string(),
  state: z.string(),
  version,
  createdByUserId: z.string(),
  approvedByUserId: z.string().nullable(),
});
