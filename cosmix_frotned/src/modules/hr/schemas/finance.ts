import { z } from 'zod';

const id = z.uuid();
const version = z.string();
const date = z.iso.date();
const stamp = z.string();
const money = z.number();
const text = z.string();

export const allowanceKindValue = z.union([
  z.literal(1),
  z.literal(2),
  z.literal(3),
  z.literal(4),
  z.literal(5),
  z.literal(6),
]);
export const advanceKindValue = z.union([z.literal(1), z.literal(2)]);

const page = {
  page: z.number(),
  pageSize: z.number(),
  hasMore: z.boolean(),
};

export const allowanceEligibility = z.object({
  kind: allowanceKindValue,
  state: text,
  continuousHours: z.number().nullable(),
  requiredHours: z.number().nullable(),
  eligibilityPeriodStartUtc: stamp.nullable(),
  eligibilityPeriodEndUtc: stamp.nullable(),
  workSessionId: id.nullable(),
  eligibilityKey: text.nullable(),
  claimedAmount: money.nullable(),
  currency: text.nullable(),
  claimExists: z.boolean(),
  reason: text,
});

export const allowanceSource = z.object({
  employeeId: id,
  businessDate: date,
  staffCategory: text,
  policyState: text,
  policyVersionId: id.nullable(),
  assignmentIds: z.array(id),
  currency: text.nullable(),
  kinds: z.array(allowanceEligibility),
});

export const allowanceClaim = z.object({
  id,
  employeeId: id,
  kind: allowanceKindValue,
  state: text,
  version,
  submissionVersion: z.number(),
  claimedAmount: money,
  currency: text,
  payableDate: date,
  payrollPeriodStart: date.nullable(),
  policyVersionId: id.nullable(),
  settlementState: text,
  approvalRequestId: id.nullable(),
  decisionId: id.nullable(),
  workSessionId: id.nullable(),
  deploymentCategoryCode: text,
  eligibilityPeriodStart: stamp,
  eligibilityPeriodEnd: stamp,
  eligibilityKey: text,
  createdAt: stamp,
  submittedAt: stamp.nullable(),
  decidedAt: stamp.nullable(),
});

export const allowanceDetail = z.object({
  claim: allowanceClaim,
  eligibility: allowanceEligibility,
  reason: text.nullable(),
  visibility: text,
});

export const allowancePage = z.object({ items: z.array(allowanceClaim), ...page });

export const policyView = z.object({
  id,
  code: text,
  name: text,
  revision: z.number(),
  version,
});

export const policyVersionView = z.object({
  id,
  number: z.number(),
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  canonicalJson: text,
  configurationSha256: text,
  publishedAt: stamp,
  authority: text,
  publishedByUserId: text,
});

export const policyAssignmentView = z.object({
  id,
  policyVersionId: id,
  target: z.number(),
  targetId: id,
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  endId: id.nullable(),
  reason: text,
  authority: text,
  recordedByUserId: text,
  recordedAt: stamp,
  originalEffectiveTo: date.nullable(),
  endingReason: text.nullable(),
  endedByUserId: text.nullable(),
  endRecordedAt: stamp.nullable(),
});

export const policyPage = z.object({ items: z.array(policyView), ...page });

export const policyVersionPage = z.object({ items: z.array(policyVersionView), ...page });

export const policyAssignmentPage = z.object({ items: z.array(policyAssignmentView), ...page });

export const policyRuleError = z.object({ path: text, code: text, message: text });

export const policyPublicationPreview = z.object({
  canPublish: z.boolean(),
  revision: z.number(),
  version,
  canonicalJson: text.nullable(),
  configurationSha256: text.nullable(),
  previewFingerprint: text,
  errors: z.array(policyRuleError),
});

export const policyPublicationResult = z.object({
  policy: policyView,
  published: policyVersionView,
});

export const policyAssignmentResult = z.object({
  policy: policyView,
  assignment: policyAssignmentView,
});

export const policyResolution = z.object({
  state: text,
  policyVersionId: id.nullable(),
  assignmentIds: z.array(id),
  rules: z.unknown().nullable(),
  canonicalJson: text.nullable(),
  fingerprint: text,
});

export const advanceRequest = z.object({
  id,
  employeeId: id,
  employeeName: text,
  employeeCode: text,
  kind: advanceKindValue,
  requestedAmount: money,
  currency: text,
  reason: text,
  requestedOn: date,
  createdAt: stamp,
  state: text,
  agreementId: id.nullable(),
  approvalRequestId: id.nullable(),
});

export const repaymentScheduleEntry = z.object({
  id,
  sequence: z.number(),
  dueDate: date,
  principalAmount: money,
  otherApprovedAmount: money,
  totalDue: money,
  currency: text,
  paidAmount: money,
  isSettled: z.boolean(),
});

export const repaymentLedgerEntry = z.object({
  id,
  repaymentScheduleEntryId: id.nullable(),
  kind: z.number(),
  amount: money,
  currency: text,
  valueDate: date,
  sourceReference: text,
  reversesEntryId: id.nullable(),
  recordedAt: stamp,
});

export const advanceAgreement = z.object({
  id,
  advanceLoanRequestId: id,
  employeeId: id,
  employeeName: text,
  employeeCode: text,
  kind: advanceKindValue,
  approvedPrincipal: money,
  currency: text,
  effectiveOn: date,
  approvedTerms: text,
  totalRepaid: money,
  outstandingBalance: money,
  isFullyRepaid: z.boolean(),
  recordedAt: stamp,
  schedule: z.array(repaymentScheduleEntry),
  ledger: z.array(repaymentLedgerEntry),
});

export const loanSummary = z.object({
  employeeId: id,
  totalRequestsCount: z.number(),
  activeAgreementsCount: z.number(),
  totalApprovedPrincipal: money,
  totalRepaidAmount: money,
  currentOutstandingBalance: money,
  currency: text,
  recentRequests: z.array(advanceRequest),
  activeAgreements: z.array(advanceAgreement),
});

export const advanceRequestPage = z.object({
  items: z.array(advanceRequest),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  hasMore: z.boolean(),
});

export const advanceAgreementPage = z.object({
  items: z.array(advanceAgreement),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  hasMore: z.boolean(),
});

export const vehicle = z.object({
  id,
  registrationNumber: text,
  vehicleType: text,
  makeModel: text,
  assetReference: text.nullable(),
  isActive: z.boolean(),
});

export const vehicleList = z.array(vehicle);

export const vehicleCustody = z.object({
  id,
  vehicleId: id,
  registrationNumber: text,
  employeeId: id,
  employeeName: text,
  issuedAt: stamp,
  issueOdometerKm: money,
  issueConditionReference: text,
  returnedAt: stamp.nullable(),
  returnOdometerKm: money.nullable(),
  returnConditionReference: text.nullable(),
  isActive: z.boolean(),
});

export const vehicleTrip = z.object({
  id,
  employeeId: id,
  employeeName: text,
  vehicleId: id,
  registrationNumber: text,
  purpose: z.number(),
  startedAt: stamp,
  endedAt: stamp,
  originLabel: text,
  destinationLabel: text,
  odometerStartKm: money,
  odometerEndKm: money,
  tripDistanceKm: money,
  mapDistanceKm: money,
  routeEvidenceReference: text,
  workReference: text,
  createdAt: stamp,
  approvalState: text,
  approvalRequestId: id.nullable(),
});

export const fuelClaim = z.object({
  id,
  employeeId: id,
  employeeName: text,
  vehicleTripId: id,
  eligibleDistanceKm: money,
  excludedCommuteKm: money,
  fuelLiters: money,
  claimedAmount: money,
  currency: text,
  calculationReference: text,
  receiptReference: text.nullable(),
  createdAt: stamp,
  approvalState: text,
  approvalRequestId: id.nullable(),
});

export const vehicleService = z.object({
  id,
  vehicleId: id,
  registrationNumber: text,
  employeeId: id,
  employeeName: text,
  serviceKind: z.number(),
  serviceDate: date,
  odometerKm: money,
  approvedOutstationKm: money,
  authorizedWorkshopReference: text,
  evidenceReference: text,
  claimedAmount: money,
  currency: text,
  createdAt: stamp,
  approvalState: text,
  approvalRequestId: id.nullable(),
});

export const vehicleIncident = z.object({
  id,
  vehicleCustodyId: id,
  occurredAt: stamp,
  incidentEvidenceReference: text,
  thirdPartyDetailsReference: text.nullable(),
  insuranceClaimReference: text.nullable(),
  proposedEmployeeLiability: money.nullable(),
  currency: text.nullable(),
  createdAt: stamp,
  approvalState: text,
  approvalRequestId: id.nullable(),
});

export const commuteDistance = z.object({
  id,
  employeeId: id,
  effectiveFrom: date,
  effectiveTo: date.nullable(),
  oneWayKm: money,
  dailyRoundTripKm: money,
  routeReference: text,
});

const vehiclePageOf = <T extends z.ZodType>(item: T) =>
  z.object({
    items: z.array(item),
    page: z.number(),
    pageSize: z.number(),
    totalCount: z.number(),
    hasMore: z.boolean(),
  });

export const vehicleTripPage = vehiclePageOf(vehicleTrip);
export const fuelClaimPage = vehiclePageOf(fuelClaim);
export const vehicleServicePage = vehiclePageOf(vehicleService);
export const vehicleIncidentPage = vehiclePageOf(vehicleIncident);
export const custodyList = z.array(vehicleCustody);

export const payrollTotals = z.object({
  overtimeTotal: money,
  allowanceTotal: money,
  attendanceDeductionTotal: money,
  netAmount: money,
});

export const payrollPreviewLine = z.object({
  employeeId: id,
  component: text,
  sourceType: text,
  sourceId: id,
  sourceVersion: z.number(),
  amount: money,
  currency: text,
  policyVersionId: id.nullable(),
  approvalRequestId: id.nullable(),
});

export const payrollSkipped = z.object({
  reason: text,
  employeeId: id,
  sourceType: text,
  sourceId: id.nullable(),
});

const salaryEmployeeTotal = z.object({ employeeId: id, grossEarnings: money, totalDeductions: money, netPay: money });
const salaryRunFields = {
  calculationVersion: text.optional(),
  salaryComplete: z.boolean().optional(),
  employeeTotals: z.array(salaryEmployeeTotal).optional(),
};
export const payrollPreview = z.object({
  ...salaryRunFields,
  periodStart: date,
  periodEndExclusive: date,
  attendancePeriodLockEventId: id,
  lockedAtUtc: stamp,
  employeeCount: z.number(),
  lines: z.array(payrollPreviewLine),
  skipped: z.array(payrollSkipped),
  totals: payrollTotals,
  currency: text,
  previewFingerprint: text,
});

export const payrollBatch = z.object({
  ...salaryRunFields,
  previewFingerprint: text.optional(),
  frozenFingerprint: text.optional(),
  hrApprovalRequestId: id.nullable().optional(),
  hrApprovedByUserId: text.nullable().optional(),
  hrApprovedAt: stamp.nullable().optional(),
  reviewState: text.optional(),
  id,
  batchReference: text,
  periodStart: date,
  periodEndExclusive: date,
  currency: text,
  state: text,
  linesRevision: z.number(),
  version,
  preparedAt: stamp,
  preparedByUserId: text,
  lockedAt: stamp.nullable(),
  lockedByUserId: text.nullable(),
  approvalRequestId: id.nullable(),
  totals: payrollTotals,
  lineCount: z.number(),
});

export const payrollLine = z.object({
  id,
  employeeId: id,
  component: text,
  sourceType: text,
  sourceId: id,
  sourceVersion: z.number(),
  attendancePeriodLockEventId: id,
  amount: money,
  currency: text,
  policyVersionId: id.nullable(),
  approvalRequestId: id.nullable(),
  traceId: text,
});

export const payrollProcessing = z.object({
  id,
  externalProcessingReference: text,
  processedByUserId: text,
  processedAt: stamp,
});

export const payrollBatchDetail = z.object({
  batch: payrollBatch,
  lines: z.array(payrollLine),
  processing: z.array(payrollProcessing),
});

export const payrollPage = z.object({ items: z.array(payrollBatch), ...page });

export type AllowanceKindEligibility = z.infer<typeof allowanceEligibility>;
export type AllowanceSource = z.infer<typeof allowanceSource>;
export type AllowanceClaim = z.infer<typeof allowanceClaim>;
export type AllowanceDetail = z.infer<typeof allowanceDetail>;
export type AllowancePage = z.infer<typeof allowancePage>;
export type PolicyView = z.infer<typeof policyView>;
export type PolicyVersionView = z.infer<typeof policyVersionView>;
export type PolicyAssignmentView = z.infer<typeof policyAssignmentView>;
export type PolicyPublicationPreview = z.infer<typeof policyPublicationPreview>;
export type PolicyPublicationResult = z.infer<typeof policyPublicationResult>;
export type PolicyAssignmentResult = z.infer<typeof policyAssignmentResult>;
export type AdvanceRequest = z.infer<typeof advanceRequest>;
export type AdvanceAgreement = z.infer<typeof advanceAgreement>;
export type LoanSummary = z.infer<typeof loanSummary>;
export type RepaymentScheduleEntry = z.infer<typeof repaymentScheduleEntry>;
export type RepaymentLedgerEntry = z.infer<typeof repaymentLedgerEntry>;
export type Vehicle = z.infer<typeof vehicle>;
export type VehicleCustody = z.infer<typeof vehicleCustody>;
export type VehicleTrip = z.infer<typeof vehicleTrip>;
export type FuelClaim = z.infer<typeof fuelClaim>;
export type VehicleService = z.infer<typeof vehicleService>;
export type VehicleIncident = z.infer<typeof vehicleIncident>;
export type CommuteDistance = z.infer<typeof commuteDistance>;
export type PayrollPreview = z.infer<typeof payrollPreview>;
export type PayrollPreviewLine = z.infer<typeof payrollPreviewLine>;
export type PayrollSkipped = z.infer<typeof payrollSkipped>;
export type PayrollBatch = z.infer<typeof payrollBatch>;
export type PayrollBatchDetail = z.infer<typeof payrollBatchDetail>;
export type PayrollLine = z.infer<typeof payrollLine>;
