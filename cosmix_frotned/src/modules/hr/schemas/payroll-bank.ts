import { z } from 'zod';
import { id, date, time } from './contracts';
export const bankRules = z.object({
  delimiter: z.string(),
  includeHeader: z.boolean(),
  utf8Bom: z.boolean(),
  dateFormat: z.string(),
  columns: z.array(
    z.object({
      header: z.string(),
      field: z.string(),
      literal: z.string().nullable(),
      required: z.boolean(),
    }),
  ),
  outputFormat: z.enum(['Csv', 'Xlsx']),
  worksheetName: z.string(),
  templateSha256: z.string().nullable(),
  bankCodes: z.array(z.object({ bankName: z.string(), bankCode: z.string() })).nullable(),
  beneficiaryBankNames: z.array(z.string()).nullable(),
});
export const bankTemplate = z.object({
  code: z.string(),
  name: z.string(),
  sourceTemplateSha256: z.string().regex(/^[a-f0-9]{64}$/i),
  rules: bankRules,
});
export const bankFormat = z.object({
  id,
  name: z.string(),
  formatVersion: z.number().int().positive(),
  currency: z.string(),
  state: z.string(),
  version: z.number().int().positive(),
  rules: bankRules,
  createdAt: time,
  approvedAt: time.nullable(),
  createdByUserId: z.string(),
  approvedByUserId: z.string().nullable(),
});
export const bankExport = z.object({
  id,
  obligationId: id,
  formatProfileId: id,
  valueDate: date,
  paymentCount: z.number().int().positive(),
  totalAmount: z.number().finite().positive(),
  sha256: z.string().regex(/^[a-f0-9]{64}$/i),
  createdAt: time,
  expiresAt: time,
});
export type BankRules = z.infer<typeof bankRules>;
export type BankTemplate = z.infer<typeof bankTemplate>;
export type BankFormat = z.infer<typeof bankFormat>;
