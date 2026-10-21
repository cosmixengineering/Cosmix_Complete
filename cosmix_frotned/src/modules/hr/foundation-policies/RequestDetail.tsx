import { useEffect, useState } from 'react';
import { Ban, Check, CornerUpLeft, X } from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { canFoundationPolicyAction } from './access';
import {
  cancelSchema,
  currentSchema,
  decisionSchema,
  detailSchema,
  historySchema,
  requestStateLabels,
  sameId,
  type FoundationPolicyDetail,
  type FoundationPolicyKind,
  type PolicyDecision,
} from './contracts';
import { foundationPolicyEndpoint, validatePublicationSource } from './inputs';
import {
  policyError,
  policyGet,
  usePolicyQuery,
  type PolicyLifetime,
  type PolicyMutation,
} from './hooks';
import { PolicyFacts, PolicyQueryState, PolicyRulesView } from './PolicyView';

export function FoundationPolicyRequestDetail({
  kind,
  requestId,
  expectedPolicyId,
  lifetime,
  mutation,
  onDirty,
  onSaved,
  onClose,
}: {
  kind: FoundationPolicyKind;
  requestId: string;
  expectedPolicyId: string;
  lifetime: PolicyLifetime;
  mutation: PolicyMutation;
  onDirty: (dirty: boolean) => void;
  onSaved: () => void;
  onClose: () => void;
}) {
  const query = usePolicyQuery(
    lifetime,
    `${foundationPolicyEndpoint(kind)}/requests/${requestId}`,
    detailSchema(kind, requestId, expectedPolicyId),
  );
  return (
    <section className="hr-form-section">
      <div className="foundation-heading">
        <h2>Publication proposal</h2>
        <Button
          variant="ghost"
          title="Close proposal detail"
          aria-label="Close proposal detail"
          disabled={mutation.locked}
          onClick={onClose}
        >
          <X size={16} />
        </Button>
      </div>
      <PolicyQueryState query={query}>
        {query.data && (
          <>
            <PolicyFacts
              rows={[
                ['Request reference', query.data.request.id],
                ['State', requestStateLabels[query.data.request.state]],
                ['Requester', query.data.request.requesterId],
                ['Requested at (UTC)', new Date(query.data.request.requestedAt).toISOString()],
                [
                  'Closed at (UTC)',
                  query.data.request.closedAt
                    ? new Date(query.data.request.closedAt).toISOString()
                    : null,
                ],
                ['Approved version', query.data.proposal.approvedVersion],
                ['Frozen authority version', query.data.approvedAuthorityVersion],
                ['Expected publication count', query.data.proposal.expectedLatestVersion],
                ['Effective from (inclusive)', query.data.proposal.effectiveFrom],
                ['Effective to (exclusive)', query.data.proposal.effectiveTo ?? 'Open-ended'],
                ['Proposal reason', query.data.proposal.reason],
              ]}
            />
            <h3>Frozen rules</h3>
            <PolicyRulesView rules={query.data.proposal.rules} />
            <h3>Decisions</h3>
            {!query.data.decisions.length && <p className="muted">No decision recorded</p>}
            {query.data.decisions.map((decision) => (
              <PolicyFacts
                key={decision.id}
                rows={[
                  ['Decision', requestStateLabels[decision.decision]],
                  ['Actor', decision.actorId],
                  ['Role', decision.roleCode],
                  ['Reason', decision.reason],
                  ['Decided at (UTC)', new Date(decision.decidedAt).toISOString()],
                ]}
              />
            ))}
            <RequestActions
              key={query.data.request.version}
              detail={query.data}
              kind={kind}
              lifetime={lifetime}
              mutation={mutation}
              onDirty={onDirty}
              onSaved={onSaved}
            />
          </>
        )}
      </PolicyQueryState>
    </section>
  );
}
function RequestActions({
  detail,
  kind,
  lifetime,
  mutation,
  onDirty,
  onSaved,
}: {
  detail: FoundationPolicyDetail;
  kind: FoundationPolicyKind;
  lifetime: PolicyLifetime;
  mutation: PolicyMutation;
  onDirty: (dirty: boolean) => void;
  onSaved: () => void;
}) {
  const { user, status } = useAuth();
  const canCancel = canFoundationPolicyAction(user, status, 'cancel', detail.request);
  const canReview = canFoundationPolicyAction(user, status, 'decision', detail.request);
  const endpoint = foundationPolicyEndpoint(kind);
  const current = usePolicyQuery(
    lifetime,
    `${endpoint}/current`,
    currentSchema(kind),
    canCancel || canReview,
  );
  const history = usePolicyQuery(
    lifetime,
    `${endpoint}/history?page=1&pageSize=25`,
    historySchema(kind, 1),
    canReview,
  );
  const [reason, setReason] = useState('');
  const [decision, setDecision] = useState<'' | PolicyDecision>('');
  const [frozen, setFrozen] = useState<{
    operation: 'decision' | 'cancel';
    body: unknown;
    decision?: PolicyDecision;
    policyId: string;
    sourceDate: string;
  } | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    onDirty(Boolean(reason || decision || frozen));
    return () => onDirty(false);
  }, [reason, decision, frozen, onDirty]);
  const sourceReady =
    !current.isFetching &&
    !current.isError &&
    current.data?.policy &&
    sameId(current.data.policy.id, detail.request.policyId);
  const historyReady = !history.isFetching && !history.isError && Boolean(history.data);
  const revisionMatches =
    current.data?.policy?.latestVersion === detail.proposal.expectedLatestVersion;
  const approvalAvailable = Boolean(
    sourceReady &&
    revisionMatches &&
    current.data?.policy?.isActive &&
    historyReady &&
    detail.proposal.effectiveFrom >= current.data!.selectedDate &&
    (current.data?.policy?.latestVersion === 0 || history.data?.items[0]?.state === 'published'),
  );
  async function send(operation: 'decision' | 'cancel') {
    if (
      mutation.pending ||
      mutation.reloadRequired ||
      (operation === 'cancel' ? !canCancel : !canReview)
    )
      return;
    if (
      !frozen &&
      (!sourceReady ||
        (operation === 'decision' &&
          (!revisionMatches || decision === '' || (decision === 'Approved' && !approvalAvailable))))
    )
      return;
    setError(null);
    try {
      const attempt = frozen ?? {
        operation,
        decision: operation === 'decision' ? (decision as PolicyDecision) : undefined,
        policyId: current.data!.policy!.id,
        sourceDate: current.data!.selectedDate,
        body:
          operation === 'cancel'
            ? cancelSchema.parse({
                expectedPolicyVersion: current.data!.policy!.version,
                expectedRequestVersion: detail.request.version,
                reason: reason.trim(),
              })
            : decisionSchema.parse({
                expectedPolicyVersion: current.data!.policy!.version,
                expectedRequestVersion: detail.request.version,
                expectedLatestVersion: detail.proposal.expectedLatestVersion,
                decision,
                reason: reason.trim(),
              }),
      };
      if (attempt.operation !== operation) return;
      setFrozen(attempt);
      const result = await mutation.run(
        {
          path: `${endpoint}/requests/${detail.request.id}/${attempt.operation}`,
          body: attempt.body,
          operation: attempt.operation,
          policyId: attempt.policyId,
          requestId: detail.request.id,
          decision: attempt.decision,
        },
        async () => {
          const signal = lifetime.signal();
          const fresh = await policyGet(`${endpoint}/current`, currentSchema(kind), signal);
          const latestDetail = await policyGet(
            `${endpoint}/requests/${detail.request.id}`,
            detailSchema(kind, detail.request.id, attempt.policyId),
            signal,
          );
          if (
            !fresh.policy ||
            !sameId(fresh.policy.id, attempt.policyId) ||
            fresh.policy.version !== current.data!.policy!.version ||
            latestDetail.request.version !== detail.request.version ||
            latestDetail.request.state !== 0
          )
            throw new Error('The policy or proposal changed. Reload and review current records.');
          if (!canFoundationPolicyAction(user, status, operation, latestDetail.request))
            throw new Error('Current permission or independent review eligibility is unavailable.');
          if (operation === 'decision') {
            if (
              fresh.selectedDate !== attempt.sourceDate ||
              fresh.policy.latestVersion !== detail.proposal.expectedLatestVersion
            )
              throw new Error(
                'The publication timeline or business date changed. Reload and review.',
              );
            if (attempt.decision === 'Approved') {
              const publications = await policyGet(
                `${endpoint}/history?page=1&pageSize=25`,
                historySchema(kind, 1),
                signal,
              );
              validatePublicationSource(
                kind,
                { ...latestDetail.proposal, expectedPolicyVersion: fresh.policy.version },
                fresh,
                publications.items[0] ?? null,
              );
            }
          }
        },
      );
      if (lifetime.signal().aborted) return;
      if (result) onSaved();
      else if (result === null) setFrozen(null);
    } catch (failure) {
      if (!lifetime.signal().aborted) {
        setFrozen(null);
        setError(policyError(failure));
      }
    }
  }
  if (!canCancel && !canReview) return null;
  return (
    <div className="foundation-request-actions">
      <h3>{canCancel ? 'Requester cancellation' : 'Independent Admin decision'}</h3>
      <PolicyQueryState query={current}>
        {canReview && (
          <>
            {!revisionMatches && current.data && (
              <p role="alert">
                The publication revision changed. Reload and review this proposal before deciding.
              </p>
            )}
            {history.isError && (
              <p role="alert">Publication history is unavailable. Approval is disabled.</p>
            )}
            {current.data && !approvalAvailable && (
              <p className="hr-notice">
                Publication approval is unavailable for the current date, policy state or history.
              </p>
            )}
          </>
        )}
      </PolicyQueryState>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void send(canCancel ? 'cancel' : 'decision');
        }}
      >
        <fieldset disabled={mutation.locked || !sourceReady}>
          {canReview && (
            <SelectInput
              label="Decision"
              required
              value={decision}
              options={[
                { value: '', label: 'Select decision' },
                ...(approvalAvailable ? [{ value: 'Approved', label: 'Approve and publish' }] : []),
                { value: 'Returned', label: 'Return' },
                { value: 'Rejected', label: 'Reject' },
              ]}
              onChange={(event) => setDecision(event.target.value as PolicyDecision)}
            />
          )}
          <TextInput
            label={canCancel ? 'Cancellation reason' : 'Decision reason'}
            required
            maxLength={2000}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
        </fieldset>
        <div className="hr-actions">
          <Button
            type="submit"
            disabled={
              mutation.pending ||
              mutation.reloadRequired ||
              (!mutation.uncertain &&
                (!sourceReady ||
                  (canReview &&
                    (!revisionMatches ||
                      decision === '' ||
                      (decision === 'Approved' && !approvalAvailable)))))
            }
          >
            {canCancel ? (
              <Ban size={16} />
            ) : decision === 'Approved' ? (
              <Check size={16} />
            ) : decision === 'Returned' ? (
              <CornerUpLeft size={16} />
            ) : (
              <X size={16} />
            )}
            {mutation.uncertain
              ? 'Retry unchanged action'
              : mutation.pending
                ? 'Recording...'
                : canCancel
                  ? 'Cancel proposal'
                  : 'Record decision'}
          </Button>
        </div>
      </form>
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
    </div>
  );
}
