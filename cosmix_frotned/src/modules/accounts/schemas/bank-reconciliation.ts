import { z } from 'zod';
export const bankStatementSchema = z.object({
  id: z.string().uuid(),
  accountCode: z.string(),
  currency: z.string(),
  month: z.string(),
  revision: z.number().int(),
  fileSha256: z.string(),
  createdBy: z.string(),
  createdAt: z.string(),
  closed: z.boolean(),
});
export const bankRowSchema = z.object({
  row: z.number().int(),
  date: z.string(),
  reference: z.string(),
  amount: z.number(),
});
export const bankResolutionSchema = z.object({
  row: z.number().int(),
  journalIds: z.array(z.string().uuid()),
  outsidePayrollReason: z.string().nullable(),
  referenceDifferenceReason: z.string().nullable(),
});
export const bankProposalSchema = z.object({
  id: z.string().uuid(),
  createdBy: z.string(),
  createdByEmployeeId: z.string().uuid(),
  createdAt: z.string(),
  ledgerFingerprint: z.string(),
  resolutions: z.array(bankResolutionSchema),
  reason: z.string(),
});
export const bankWorkspaceSchema = z.object({
  statement: bankStatementSchema,
  openingBalance: z.number(),
  closingBalance: z.number(),
  rows: z.array(bankRowSchema),
  ledger: z.array(
    z.object({
      journalId: z.string().uuid(),
      date: z.string(),
      reference: z.string(),
      amount: z.number(),
      currency: z.string(),
    }),
  ),
  ledgerFingerprint: z.string(),
  proposal: bankProposalSchema.nullable(),
  matching: z.object({
    canClose: z.boolean(),
    matchedBankNet: z.number(),
    matchedLedgerNet: z.number(),
    outsidePayrollNet: z.number(),
    unmatchedRows: z.array(z.number()),
    unmatchedLedgerIds: z.array(z.string().uuid()),
  }),
  closedBy: z.string().nullable(),
  closedAt: z.string().nullable(),
});
export type BankStatement = z.infer<typeof bankStatementSchema>;
export type BankWorkspace = z.infer<typeof bankWorkspaceSchema>;
export type BankResolution = z.infer<typeof bankResolutionSchema>;
