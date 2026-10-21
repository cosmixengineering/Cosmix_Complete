import { z } from 'zod';
import type { AllowanceSource } from '../schemas/finance';

export function validAllowanceDate(value: string) {
  return z.iso.date().safeParse(value).success && value >= '1901-01-01' && value <= '9997-12-31';
}

export function claimEligibility(
  source: AllowanceSource | undefined,
  businessDate: string,
  kind: number,
) {
  if (!source || source.businessDate !== businessDate) return undefined;
  return source.kinds.find((item) => item.kind === kind);
}

// Presentation gate only. The API revalidates the policy, duty evidence and duplicate key.
export function canSubmitAllowance(
  source: AllowanceSource | undefined,
  businessDate: string,
  kind: number,
) {
  const eligibility = claimEligibility(source, businessDate, kind);
  return Boolean(
    validAllowanceDate(businessDate) &&
    source?.policyVersionId &&
    eligibility?.state === 'eligible' &&
    !eligibility.claimExists &&
    eligibility.claimedAmount !== null &&
    eligibility.currency &&
    eligibility.eligibilityKey,
  );
}
