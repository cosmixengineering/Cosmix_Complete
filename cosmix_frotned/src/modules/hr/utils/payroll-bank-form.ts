import { z } from 'zod';
import type { BankTemplate } from '../schemas/payroll-bank';
const reason = z
  .string()
  .trim()
  .min(1)
  .max(500)
  .refine((value) => !/\p{Cc}/u.test(value));
const evidence = z
  .string()
  .trim()
  .min(1)
  .max(8000)
  .refine((value) => !/\p{Cc}/u.test(value.replace(/[\r\n\t]/g, '')));
export function prepareBankFormat(
  template: BankTemplate,
  name: string,
  formatVersion: number,
  currency: string,
  bankCodes: { bankName: string; bankCode: string }[],
  beneficiaryBankNames: string[],
  templateEvidence: string,
  explanation: string,
) {
  const codes = bankCodes.map((row) => ({
    bankName: row.bankName.trim(),
    bankCode: row.bankCode.trim(),
  }));
  const names = beneficiaryBankNames.map((value) => value.trim()).filter(Boolean);
  if (
    template.rules.columns.some((column) => column.field === 'BankCode') &&
    (!codes.length ||
      codes.some((row) => !row.bankName || !row.bankCode) ||
      new Set(codes.map((row) => row.bankName.toLowerCase())).size !== codes.length)
  )
    throw new Error('Enter distinct bank names and the exact bank-provided routing codes.');
  if (
    template.code === 'HBL_INTERNAL' &&
    (!names.length || new Set(names.map((value) => value.toLowerCase())).size !== names.length)
  )
    throw new Error('Enter distinct reviewed beneficiary bank names.');
  return {
    name: z.string().trim().min(1).max(200).parse(name),
    formatVersion: z.number().int().positive().parse(formatVersion),
    currency: z
      .string()
      .regex(/^[A-Z]{3}$/)
      .parse(currency),
    rules: { ...template.rules, bankCodes: codes, beneficiaryBankNames: names },
    templateEvidence: evidence.parse(templateEvidence),
    reason: reason.parse(explanation),
  };
}
export function bankReviewText(explanation: string, proof: string) {
  return { reason: reason.parse(explanation), evidence: evidence.parse(proof) };
}
export function bankExportReason(value: string) {
  return reason.parse(value);
}
