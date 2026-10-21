import { z } from 'zod';

const id = z.uuid();
const day = z.iso.date();

// ApprovalRouteDto from ApprovalRoutingContracts.cs.
export const approvalRoute = z.object({
  id,
  code: z.string(),
  version: z.number().int(),
  requestType: z.string(),
  approverPermission: z.string(),
  stage: z.number().int(),
  effectiveFrom: day,
  effectiveTo: day.nullable(),
  staffCategoryId: id.nullable(),
  departmentId: id.nullable(),
  siteId: id.nullable(),
  minimumAmount: z.number().nullable(),
  maximumAmount: z.number().nullable(),
  currency: z.string().nullable(),
  minimumDurationMinutes: z.number().nullable(),
  maximumDurationMinutes: z.number().nullable(),
  riskCode: z.string().nullable(),
});
export type ApprovalRoute = z.infer<typeof approvalRoute>;
export const approvalRouteList = z.array(approvalRoute);

// ApprovalDelegationDto from ApprovalRoutingContracts.cs. ScopeKind serializes as its
// AccessScopeKind number (Self 1 .. All 7); Version is the base64 row version.
export const approvalDelegation = z.object({
  id,
  delegatorAccountId: z.string(),
  delegateAccountId: z.string(),
  requestType: z.string(),
  permission: z.string(),
  scopeKind: z.number().int().min(1).max(7),
  targetId: id.nullable(),
  startsAt: z.string(),
  endsAt: z.string(),
  revokedAt: z.string().nullable(),
  version: z.string(),
});
export type ApprovalDelegation = z.infer<typeof approvalDelegation>;
export const approvalDelegationList = z.array(approvalDelegation);

// ApprovalRouteResolver.ConfigurableTypes: the only request types this API accepts.
export const approvalRequestTypeOptions = [
  { value: 'allowance', label: 'Allowance' },
  { value: 'fuel', label: 'Fuel' },
  { value: 'vehicle-maintenance', label: 'Vehicle maintenance' },
  { value: 'leave-encashment', label: 'Leave encashment' },
  { value: 'advance', label: 'Advance' },
  { value: 'loan', label: 'Loan' },
  { value: 'policy-exception', label: 'Policy exception' },
];

export const stageOptions = Array.from({ length: 20 }, (_, index) => ({
  value: String(index + 1),
  label: `Stage ${index + 1}`,
}));

// ApprovalPermissions.StageReview: the only approver permission a stage may carry.
export function stagePermission(stage: number) {
  return `approvals.stages.${stage}.review`;
}
