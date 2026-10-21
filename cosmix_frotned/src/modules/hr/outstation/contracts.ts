import { z } from 'zod';
import type { SessionUser } from '@/auth/types';

export const deploymentPermissions = {
  read: 'attendance.deployments.read',
  request: 'attendance.deployments.request',
  decide: 'attendance.deployments.decide',
} as const;
export const deploymentId = z.uuid().refine((v) => v !== '00000000-0000-0000-0000-000000000000');
export const sameId = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();
const version = z.string().regex(/^[A-Za-z0-9+/]{11}=$/);
const stamp = z.iso.datetime({ offset: true });
const utc = stamp.refine((v) => /(?:Z|\+00:00)$/.test(v), 'Use UTC timestamps.');
const text = (max: number) =>
  z
    .string()
    .trim()
    .min(1)
    .max(max)
    .refine((v) => !/\p{Cc}/u.test(v), 'Control characters and line breaks are not allowed.');
const state = z.enum(['Pending', 'Approved', 'Returned', 'Rejected']);
const fingerprint = z.string().regex(/^[a-fA-F0-9]{64}$/);
const nonnegative = z.number().nonnegative();
const uniqueIds = z
  .array(deploymentId)
  .refine((v) => new Set(v.map((x) => x.toLowerCase())).size === v.length);

// Resource membership and fixed Admin roles are enforced by the API, never guessed here.
// Self-only grants do not expose a management workspace.
export function canDeployment(
  user: SessionUser | null,
  action: keyof typeof deploymentPermissions,
  employeeId?: string,
) {
  return Boolean(
    user?.account &&
    user.grants.some(
      (g) =>
        g.permission === deploymentPermissions[action] &&
        ((g.scopeKind === 7 && g.targetId === null) ||
          (g.scopeKind === 2 &&
            deploymentId.safeParse(g.targetId).success &&
            (!employeeId || sameId(g.targetId!, employeeId))) ||
          ([3, 4, 5, 6].includes(g.scopeKind) && deploymentId.safeParse(g.targetId).success)),
    ),
  );
}
export function canDecideDeployment(
  user: SessionUser | null,
  employeeId: string,
  requesterId?: string,
) {
  return (
    canDeployment(user, 'read', employeeId) &&
    canDeployment(user, 'decide', employeeId) &&
    Boolean(
      user?.account &&
      !sameId(user.account.employeeId, employeeId) &&
      (!requesterId || !sameId(user.account.accountId, requesterId)),
    )
  );
}

export const deploymentRequest = z
  .object({
    startsAtUtc: utc,
    endsAtUtc: utc,
    destination: text(500),
    deploymentCategory: text(60).regex(/^[A-Z0-9_-]+$/),
    writtenEvidence: text(8000),
    reason: text(1000),
  })
  .refine((v) => {
    const days = (Date.parse(v.endsAtUtc) - Date.parse(v.startsAtUtc)) / 86400000;
    return days > 0 && days <= 366;
  }, 'The end must follow the start within 366 days.');
export const deploymentDecision = z.object({
  expectedDeploymentVersion: version,
  expectedApprovalVersion: version,
  decision: z.union([z.literal(1), z.literal(2), z.literal(3)]),
  writtenEvidence: text(8000),
  reason: text(1000),
});
export const homeBreakRequest = z
  .object({
    expectedDeploymentVersion: version,
    departedAtUtc: utc,
    returnedAtUtc: utc.nullable(),
    writtenEvidence: text(8000),
    reason: text(1000),
    supersedesBreakId: deploymentId.nullable(),
  })
  .refine(
    (v) => !v.returnedAtUtc || Date.parse(v.returnedAtUtc) > Date.parse(v.departedAtUtc),
    'Return must follow departure.',
  )
  .refine(
    (v) => !v.supersedesBreakId || v.returnedAtUtc !== null,
    'A replacement requires return evidence.',
  );

export const deployment = z.object({
  id: deploymentId,
  employeeId: deploymentId,
  startsAtUtc: utc,
  endsAtUtc: utc,
  state,
  version,
  approvalVersion: version,
  revision: z.number().int().positive(),
  finalizedClaimId: deploymentId.nullable(),
});
export const homeBreak = z.object({
  id: deploymentId,
  departedAtUtc: utc,
  returnedAtUtc: utc.nullable(),
  state,
  approvalVersion: version,
});
export const deploymentClaim = z.object({
  id: deploymentId,
  deploymentId,
  amount: nonnegative,
  currency: z.string().regex(/^[A-Z]{3}$/),
  payableDate: z.iso.date(),
  policyVersionId: deploymentId,
  state,
  approvalVersion: version,
});
export const deploymentDetail = z
  .object({
    deployment,
    proposal: deploymentRequest,
    homeBreaks: z.array(homeBreak).max(512),
    claims: z.array(deploymentClaim).max(1),
  })
  .refine(
    (v) =>
      v.proposal.startsAtUtc === v.deployment.startsAtUtc &&
      v.proposal.endsAtUtc === v.deployment.endsAtUtc &&
      v.claims.every((c) => sameId(c.deploymentId, v.deployment.id)) &&
      new Set(v.homeBreaks.map((b) => b.id.toLowerCase())).size === v.homeBreaks.length,
    'Deployment evidence does not match the selected record.',
  );
export function detailFor(employeeId: string, id: string) {
  return deploymentDetail.refine(
    (v) => sameId(v.deployment.employeeId, employeeId) && sameId(v.deployment.id, id),
    'Wrong deployment returned.',
  );
}
export function historyFor(employeeId: string) {
  return z
    .array(deployment)
    .max(25)
    .refine(
      (rows) =>
        rows.every((r) => sameId(r.employeeId, employeeId)) &&
        new Set(rows.map((r) => r.id.toLowerCase())).size === rows.length,
      'Wrong employee history returned.',
    );
}
export const deploymentCalculation = z
  .object({
    state: z.string().min(1),
    fingerprint,
    policyVersionId: deploymentId.nullable(),
    approvedBreakIds: uniqueIds.max(512),
    policyDays: z
      .array(
        z.object({
          date: z.iso.date(),
          policyVersionId: deploymentId,
          assignments: uniqueIds.min(1),
          fingerprint,
          canonicalRules: z.string().min(1),
        }),
      )
      .max(367),
    valuation: z
      .object({
        state: z.string().min(1),
        dayUnits: nonnegative.nullable(),
        amount: nonnegative.nullable(),
        currency: z
          .string()
          .regex(/^[A-Z]{3}$/)
          .nullable(),
        lines: z.array(
          z.object({
            date: z.iso.date().nullable(),
            startsAtUtc: utc,
            endsAtUtc: utc,
            dutyTicks: nonnegative.int(),
            dayUnits: nonnegative,
            unroundedAmount: nonnegative,
            treatment: z.string().min(1),
          }),
        ),
      })
      .nullable(),
  })
  .refine(
    (c) =>
      c.state === 'ready'
        ? c.valuation?.state === 'ready' &&
          c.valuation.amount !== null &&
          c.valuation.dayUnits !== null &&
          c.valuation.currency !== null &&
          c.policyVersionId !== null &&
          c.policyDays.length > 0
        : c.valuation === null ||
          (c.valuation.state === c.state &&
            c.valuation.amount === null &&
            c.valuation.dayUnits === null &&
            c.valuation.currency === null),
    'Pending calculations must not contain payable money.',
  );
const claimEvidence = z.object({
  schemaVersion: z.literal(1),
  id: deploymentId,
  deploymentId,
  employeeId: deploymentId,
  requesterId: deploymentId,
  proposal: z.object({
    expectedDeploymentVersion: version,
    expectedFingerprint: fingerprint,
    payableDate: z.iso.date(),
    reason: text(1000),
  }),
  calculation: deploymentCalculation,
  deployment: z.object({
    schemaVersion: z.literal(1),
    deploymentId,
    employeeId: deploymentId,
    requesterId: deploymentId,
    proposal: deploymentRequest,
  }),
});
export function evidenceFor(employeeId: string, parent: string, claim: DeploymentClaim) {
  return claimEvidence.refine(
    (v) =>
      sameId(v.employeeId, employeeId) &&
      sameId(v.deploymentId, parent) &&
      sameId(v.id, claim.id) &&
      sameId(v.deployment.employeeId, employeeId) &&
      sameId(v.deployment.deploymentId, parent) &&
      v.calculation.state === 'ready' &&
      v.calculation.fingerprint === v.proposal.expectedFingerprint &&
      v.calculation.policyVersionId === claim.policyVersionId &&
      v.calculation.valuation?.amount === claim.amount &&
      v.calculation.valuation.currency === claim.currency &&
      v.proposal.payableDate === claim.payableDate,
    'Retained claim evidence does not match the selected claim.',
  );
}
export type Deployment = z.infer<typeof deployment>;
export type DeploymentDetail = z.infer<typeof deploymentDetail>;
export type DeploymentClaim = z.infer<typeof deploymentClaim>;
export type DeploymentCalculation = z.infer<typeof deploymentCalculation>;
export type ClaimEvidence = z.infer<typeof claimEvidence>;

export function deploymentPath(employeeId: string, id?: string) {
  return `/hr/employees/${deploymentId.parse(employeeId)}/deployments${id ? '/' + deploymentId.parse(id) : ''}`;
}

export function utcInput(value: string) {
  return utc.parse(value.length === 16 ? value + ':00Z' : value + 'Z');
}
