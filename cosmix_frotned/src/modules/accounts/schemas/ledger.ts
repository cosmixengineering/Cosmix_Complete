import { z } from 'zod';

const money = z.number().finite();
export const trialBalanceSchema = z.object({
  from: z.string(),
  toExclusive: z.string(),
  includedSources: z.array(z.string()),
  accounts: z.array(
    z.object({
      accountCode: z.string(),
      currency: z.string(),
      openingBalance: money,
      debit: money,
      credit: money,
      closingBalance: money,
    }),
  ),
});
export const ledgerPageSchema = z.object({
  page: z.number().int(),
  pageSize: z.number().int(),
  hasMore: z.boolean(),
  items: z.array(
    z.object({
      journalId: z.string().uuid(),
      sourceType: z.string(),
      sourceKey: z.string(),
      postingDate: z.string().nullable(),
      currency: z.string(),
      accountCode: z.string(),
      debit: money,
      credit: money,
      obligationId: z.string().uuid(),
    }),
  ),
});
