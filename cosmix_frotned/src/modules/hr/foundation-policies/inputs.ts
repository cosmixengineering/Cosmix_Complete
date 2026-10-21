import {
  proposalSchema,
  type FoundationPolicyCurrent,
  type FoundationPolicyKind,
  type FoundationPolicyProposal,
  type FoundationPolicyVersion,
} from './contracts';

export const foundationPolicyEndpoint = (kind: FoundationPolicyKind) =>
  `/hr/foundation-policies/${kind}`;
export type PolicyIntent = Readonly<{
  path: string;
  body: unknown;
  fingerprint: string;
  key: string;
}>;
function freeze(value: unknown): void {
  if (value && typeof value === 'object') {
    Object.values(value).forEach(freeze);
    Object.freeze(value);
  }
}
export function capturePolicyIntent(
  path: string,
  body: unknown,
  previous: PolicyIntent | null,
): PolicyIntent {
  const fingerprint = JSON.stringify([path, body]);
  if (previous) {
    if (previous.fingerprint !== fingerprint)
      throw new Error('Retry the unchanged action or reload and review the policy.');
    return previous;
  }
  const copy: unknown = structuredClone(body);
  freeze(copy);
  return Object.freeze({ path, body: copy, fingerprint, key: crypto.randomUUID() });
}
export function validatePublicationSource(
  kind: FoundationPolicyKind,
  input: FoundationPolicyProposal,
  current: FoundationPolicyCurrent,
  latest: FoundationPolicyVersion | null,
) {
  proposalSchema(kind).parse(input);
  if (
    input.expectedPolicyVersion !== (current.policy?.version ?? null) ||
    input.expectedLatestVersion !== (current.policy?.latestVersion ?? 0)
  )
    throw new Error('The policy changed. Reload and review its current revision.');
  if (current.policy && !current.policy.isActive)
    throw new Error('This policy is inactive. Publication is unavailable.');
  if (input.effectiveFrom < current.selectedDate)
    throw new Error(
      'Backdating is unavailable. Use the current server business date or a future date.',
    );
  if ((current.policy?.latestVersion ?? 0) === 0) {
    if (latest) throw new Error('Publication history changed. Reload and review.');
    return;
  }
  if (
    !latest ||
    latest.state !== 'published' ||
    latest.identity.number !== current.policy!.latestVersion ||
    latest.identity.policyId.toLowerCase() !== current.policy!.id.toLowerCase()
  )
    throw new Error('The latest publication is unavailable. Reload and review the history.');
  if (
    input.effectiveFrom <= latest.identity.effectiveFrom ||
    (latest.identity.effectiveTo !== null && latest.identity.effectiveTo > input.effectiveFrom)
  )
    throw new Error(
      'The start date must follow the latest start and must not overlap its finite interval.',
    );
  if (input.approvedVersion === latest.identity.approvedVersion)
    throw new Error('An approved version label cannot be reused.');
}
