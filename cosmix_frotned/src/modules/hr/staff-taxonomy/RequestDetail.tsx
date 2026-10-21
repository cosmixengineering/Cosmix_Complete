import { useState } from 'react';
import { Check, RotateCcw, X } from 'lucide-react';
import { z } from 'zod';
import { useAuth } from '@/auth/use-auth';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { canTaxonomyAction } from './access';
import {
  cancelSchema,
  decisionSchema,
  detailSchema,
  type TaxonomyPolicy,
  type TaxonomyRequest,
} from './contracts';
import { useTaxonomyQuery, type TaxonomyMutation } from './hooks';
import { CapabilityBoundary, MappingView, MutationFeedback, ReadState } from './Views';

export function RequestDetail({
  requestId,
  policy,
  mutation,
  onDirty,
  onSaved,
}: {
  requestId: string;
  policy: TaxonomyPolicy;
  mutation: TaxonomyMutation;
  onDirty: () => void;
  onSaved: () => void;
}) {
  const query = useTaxonomyQuery(`/requests/${requestId}`, detailSchema(requestId, policy.id));
  return (
    <ReadState query={query} locked={mutation.locked}>
      {query.data && (
        <>
          <h2>Taxonomy proposal</h2>
          <dl className="taxonomy-facts">
            <div>
              <dt>Request</dt>
              <dd>{query.data.request.id}</dd>
            </div>
            <div>
              <dt>State</dt>
              <dd>{query.data.request.state}</dd>
            </div>
            <div>
              <dt>Requester account</dt>
              <dd>{query.data.request.requesterId}</dd>
            </div>
            <div>
              <dt>Requested at</dt>
              <dd>{query.data.request.requestedAt}</dd>
            </div>
            <div>
              <dt>Closed at</dt>
              <dd>{query.data.request.closedAt ?? 'Open'}</dd>
            </div>
            <div>
              <dt>Authority version</dt>
              <dd>{query.data.authorityVersion}</dd>
            </div>
            <div>
              <dt>Effective from</dt>
              <dd>{query.data.effectiveFrom}</dd>
            </div>
            <div>
              <dt>Effective to (exclusive)</dt>
              <dd>{query.data.effectiveTo ?? 'Open ended'}</dd>
            </div>
            <div>
              <dt>Reason</dt>
              <dd>{query.data.reason}</dd>
            </div>
            <div>
              <dt>Written evidence</dt>
              <dd>{query.data.writtenEvidence}</dd>
            </div>
          </dl>
          {query.data.rules.categories.map((mapping) => (
            <MappingView key={mapping.code} mapping={mapping} />
          ))}
          <CapabilityBoundary />
          <DecisionForm
            request={query.data.request}
            policy={policy}
            mutation={mutation}
            onDirty={onDirty}
            onSaved={onSaved}
          />
        </>
      )}
    </ReadState>
  );
}
function DecisionForm({
  request,
  policy,
  mutation,
  onDirty,
  onSaved,
}: {
  request: TaxonomyRequest;
  policy: TaxonomyPolicy;
  mutation: TaxonomyMutation;
  onDirty: () => void;
  onSaved: () => void;
}) {
  const { user, status } = useAuth();
  const cancel = canTaxonomyAction(user, status, 'cancel', request);
  const review = canTaxonomyAction(user, status, 'decision', request);
  const [decision, setDecision] = useState('');
  const [reason, setReason] = useState('');
  const [evidence, setEvidence] = useState('');
  const [reviewed, setReviewed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [retained, setRetained] = useState<
    z.infer<typeof decisionSchema> | z.infer<typeof cancelSchema> | null
  >(null);
  if (!cancel && !review)
    return request.state === 'Pending' ? (
      <p className="taxonomy-notice">
        Independent review is unavailable under the current account and grants.
      </p>
    ) : null;
  function change(action: () => void) {
    action();
    setError(null);
    onDirty();
  }
  async function submit() {
    if (mutation.pending || mutation.reloadRequired || (!mutation.uncertain && review && !reviewed))
      return;
    try {
      const body =
        retained ??
        (cancel ? cancelSchema : decisionSchema).parse({
          expectedPolicyVersion: policy.version,
          expectedRequestVersion: request.version,
          ...(cancel ? {} : { decision: Number(decision) }),
          reason: reason.trim(),
          writtenEvidence: evidence.trim(),
        });
      setRetained(body);
      setError(null);
      const result = await mutation.run(
        body,
        cancel
          ? { action: 'cancel', policy, request }
          : { action: 'decision', policy, request, decision: decisionSchema.parse(body).decision },
      );
      if (result) onSaved();
      else if (result === null) setRetained(null);
    } catch (failure) {
      setError(
        failure instanceof z.ZodError
          ? (failure.issues[0]?.message ?? 'Review the action.')
          : 'Review the action values.',
      );
    }
  }
  return (
    <form
      className="taxonomy-form"
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
    >
      <h3>{cancel ? 'Requester cancellation' : 'Independent decision'}</h3>
      {review && (
        <p className="taxonomy-notice">
          Fixed administrator authority is verified on submission. Requester self-review is
          prohibited.
        </p>
      )}
      <fieldset className="taxonomy-form-fields" disabled={mutation.locked}>
        <div className="taxonomy-grid">
          {review && (
            <SelectInput
              label="Decision"
              value={decision}
              onChange={(e) => change(() => setDecision(e.target.value))}
              options={[
                { value: '', label: 'Select decision' },
                { value: '1', label: 'Approve' },
                { value: '2', label: 'Return' },
                { value: '3', label: 'Reject' },
              ]}
            />
          )}
          <TextInput
            label="Reason"
            value={reason}
            maxLength={500}
            onChange={(e) => change(() => setReason(e.target.value))}
          />
          <TextInput
            label="Written evidence"
            value={evidence}
            maxLength={8000}
            onChange={(e) => change(() => setEvidence(e.target.value))}
          />
        </div>
        {review && (
          <label className="taxonomy-check">
            <input
              type="checkbox"
              checked={reviewed}
              onChange={(e) => change(() => setReviewed(e.target.checked))}
            />
            Reviewed the exact proposal, references and authority version
          </label>
        )}
      </fieldset>
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
      <MutationFeedback mutation={mutation} />
      <Button
        type="submit"
        disabled={
          mutation.pending ||
          mutation.reloadRequired ||
          (!mutation.uncertain && review && !reviewed)
        }
      >
        {mutation.uncertain ? (
          <RotateCcw size={16} />
        ) : cancel ? (
          <X size={16} />
        ) : (
          <Check size={16} />
        )}
        {mutation.uncertain
          ? 'Retry unchanged action'
          : cancel
            ? 'Cancel proposal'
            : 'Record decision'}
      </Button>
    </form>
  );
}
