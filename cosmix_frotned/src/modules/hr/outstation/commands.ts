import type { SessionUser } from '@/auth/types';
import {
  canDecideDeployment,
  canDeployment,
  deployment,
  deploymentClaim,
  deploymentDecision,
  deploymentPath,
  deploymentRequest,
  homeBreak,
  homeBreakRequest,
  sameId,
  utcInput,
  type ClaimEvidence,
  type DeploymentDetail,
} from './contracts';
import type { OutstationIntent } from './api';

export type FormTarget = {
  kind: 'request' | 'break' | 'deployment-decision' | 'break-decision' | 'claim-decision';
  id?: string;
  detail?: DeploymentDetail;
  evidence?: ClaimEvidence;
};
export type FormValues = {
  start: string;
  end: string;
  destination: string;
  category: string;
  writtenEvidence: string;
  reason: string;
  decision: string;
  supersedes: string;
};
export function canRunDeployment(user: SessionUser | null, employeeId: string, target: FormTarget) {
  if (!canDeployment(user, 'read', employeeId)) return false;
  if (target.kind === 'request') return canDeployment(user, 'request', employeeId);
  const detail = target.detail;
  if (
    !detail ||
    !sameId(detail.deployment.employeeId, employeeId) ||
    detail.deployment.finalizedClaimId
  )
    return false;
  const activeClaim = detail.claims.some((c) => ['Pending', 'Approved'].includes(c.state));
  if (target.kind === 'break')
    return (
      detail.deployment.state === 'Approved' &&
      !activeClaim &&
      canDeployment(user, 'request', employeeId)
    );
  if (!canDecideDeployment(user, employeeId, target.evidence?.requesterId)) return false;
  if (target.kind === 'deployment-decision') return detail.deployment.state === 'Pending';
  if (detail.deployment.state !== 'Approved') return false;
  if (target.kind === 'break-decision')
    return (
      !activeClaim && detail.homeBreaks.some((b) => b.id === target.id && b.state === 'Pending')
    );
  return Boolean(
    target.evidence &&
    target.evidence.id === target.id &&
    target.evidence.deploymentId === detail.deployment.id &&
    target.evidence.employeeId === employeeId &&
    detail.claims.some((c) => c.id === target.id && c.state === 'Pending'),
  );
}
export function deploymentIntent(
  employeeId: string,
  target: FormTarget,
  values: FormValues,
): OutstationIntent {
  const base = deploymentPath(employeeId, target.detail?.deployment.id);
  if (target.kind === 'request') {
    const body = deploymentRequest.parse({
      startsAtUtc: utcInput(values.start),
      endsAtUtc: utcInput(values.end),
      destination: values.destination,
      deploymentCategory: values.category,
      writtenEvidence: values.writtenEvidence,
      reason: values.reason,
    });
    return {
      path: deploymentPath(employeeId),
      body,
      schema: deployment.refine(
        (r) =>
          sameId(r.employeeId, employeeId) &&
          r.state === 'Pending' &&
          Date.parse(r.startsAtUtc) === Date.parse(body.startsAtUtc) &&
          Date.parse(r.endsAtUtc) === Date.parse(body.endsAtUtc) &&
          r.finalizedClaimId === null,
      ),
    };
  }
  const detail = target.detail;
  if (!detail) throw new Error('Reload the deployment before continuing.');
  const d = detail.deployment;
  if (target.kind === 'break') {
    const body = homeBreakRequest.parse({
      expectedDeploymentVersion: d.version,
      departedAtUtc: utcInput(values.start),
      returnedAtUtc: values.end ? utcInput(values.end) : null,
      writtenEvidence: values.writtenEvidence,
      reason: values.reason,
      supersedesBreakId: values.supersedes || null,
    });
    const start = Date.parse(body.departedAtUtc),
      end = body.returnedAtUtc ? Date.parse(body.returnedAtUtc) : null;
    if (
      start < Date.parse(d.startsAtUtc) ||
      start >= Date.parse(d.endsAtUtc) ||
      start > Date.now() ||
      (end !== null && (end > Date.parse(d.endsAtUtc) || end > Date.now()))
    )
      throw new Error('Home-break timestamps must be actual events within this deployment.');
    if (
      body.supersedesBreakId &&
      !detail.homeBreaks.some(
        (b) =>
          b.id === body.supersedesBreakId &&
          b.state === 'Approved' &&
          b.returnedAtUtc === null &&
          Date.parse(b.departedAtUtc) === start,
      )
    )
      throw new Error('Select an approved open break and keep its exact departure timestamp.');
    return {
      path: base + '/home-breaks',
      body,
      schema: homeBreak.refine(
        (r) =>
          r.state === 'Pending' &&
          Date.parse(r.departedAtUtc) === start &&
          (r.returnedAtUtc === null ? end === null : Date.parse(r.returnedAtUtc) === end),
      ),
    };
  }
  const row =
    target.kind === 'deployment-decision'
      ? d
      : target.kind === 'break-decision'
        ? detail.homeBreaks.find((b) => b.id === target.id)
        : detail.claims.find((c) => c.id === target.id);
  if (!row || row.state !== 'Pending')
    throw new Error('Reload the pending request before deciding.');
  const body = deploymentDecision.parse({
    expectedDeploymentVersion: d.version,
    expectedApprovalVersion: row.approvalVersion,
    decision: Number(values.decision),
    writtenEvidence: values.writtenEvidence,
    reason: values.reason,
  });
  if (
    target.kind === 'deployment-decision' &&
    body.decision === 1 &&
    Date.parse(d.endsAtUtc) > Date.now()
  )
    throw new Error('Deployment approval requires the actual end to have passed.');
  const expectedState = ['Pending', 'Approved', 'Returned', 'Rejected'][body.decision];
  if (target.kind === 'deployment-decision')
    return {
      path: base + '/decision',
      body,
      schema: deployment.refine(
        (r) =>
          sameId(r.id, d.id) &&
          sameId(r.employeeId, employeeId) &&
          r.state === expectedState &&
          r.revision === d.revision + 1 &&
          r.version !== d.version,
      ),
    };
  if (target.kind === 'break-decision')
    return {
      path: base + `/home-breaks/${row.id}/decision`,
      body,
      schema: homeBreak.refine(
        (r) =>
          sameId(r.id, row.id) &&
          r.state === expectedState &&
          r.approvalVersion !== row.approvalVersion,
      ),
    };
  return {
    path: base + `/claims/${row.id}/decision`,
    body,
    schema: deploymentClaim.refine(
      (r) =>
        sameId(r.id, row.id) &&
        sameId(r.deploymentId, d.id) &&
        r.state === expectedState &&
        r.approvalVersion !== row.approvalVersion,
    ),
  };
}
