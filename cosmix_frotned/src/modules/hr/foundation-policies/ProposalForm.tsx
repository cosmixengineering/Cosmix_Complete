import { useEffect, useState } from 'react';
import { Copy, Send, X } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Button } from '@/components/ui/Button';
import { useAuth } from '@/auth/use-auth';
import { canFoundationPolicy } from './access';
import {
  currentSchema,
  historySchema,
  proposalSchema,
  requestsSchema,
  type FoundationPolicyCurrent,
  type FoundationPolicyKind,
  type FoundationPolicyVersion,
} from './contracts';
import { blankPolicyDraft, draftFromRules, rulesFromDraft } from './draft';
import { foundationPolicyEndpoint, validatePublicationSource } from './inputs';
import { policyError, policyGet, type PolicyLifetime, type PolicyMutation } from './hooks';
import { RulesEditor } from './RulesEditor';
import { PolicyFacts } from './PolicyView';

export function ProposalForm({
  kind,
  current,
  latest,
  lifetime,
  mutation,
  onDirty,
  onSaved,
  onClose,
}: {
  kind: FoundationPolicyKind;
  current: FoundationPolicyCurrent;
  latest: FoundationPolicyVersion | null;
  lifetime: PolicyLifetime;
  mutation: PolicyMutation;
  onDirty: (dirty: boolean) => void;
  onSaved: () => void;
  onClose: () => void;
}) {
  const { user, status } = useAuth();
  const [reviewed] = useState({ current, latest });
  const [draft, setDraft] = useState(blankPolicyDraft);
  const [approvedVersion, setApprovedVersion] = useState('');
  const [effectiveFrom, setEffectiveFrom] = useState('');
  const [effectiveTo, setEffectiveTo] = useState('');
  const [reason, setReason] = useState('');
  const [frozen, setFrozen] = useState<ReturnType<typeof proposalSchema>['_output'] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const allowed = canFoundationPolicy(user, status, 'request');
  const endpoint = foundationPolicyEndpoint(kind);
  useEffect(() => {
    onDirty(
      Boolean(
        approvedVersion ||
        effectiveFrom ||
        effectiveTo ||
        reason ||
        frozen ||
        JSON.stringify(draft) !== JSON.stringify(blankPolicyDraft()),
      ),
    );
    return () => onDirty(false);
  }, [approvedVersion, effectiveFrom, effectiveTo, reason, draft, frozen, onDirty]);
  async function submit() {
    if (!allowed || mutation.pending || mutation.reloadRequired) return;
    setError(null);
    try {
      const body =
        frozen ??
        proposalSchema(kind).parse({
          expectedPolicyVersion: reviewed.current.policy?.version ?? null,
          expectedLatestVersion: reviewed.current.policy?.latestVersion ?? 0,
          approvedVersion: approvedVersion.trim(),
          effectiveFrom,
          effectiveTo: effectiveTo || null,
          rules: rulesFromDraft(kind, draft),
          reason: reason.trim(),
        });
      validatePublicationSource(kind, body, reviewed.current, reviewed.latest);
      setFrozen(body);
      const result = await mutation.run(
        {
          path: `${endpoint}/requests`,
          body,
          operation: 'request',
          policyId: reviewed.current.policy?.id,
        },
        async () => {
          const signal = lifetime.signal();
          const fresh = await policyGet(`${endpoint}/current`, currentSchema(kind), signal);
          if (fresh.selectedDate !== reviewed.current.selectedDate)
            throw new Error('The server business date changed. Reload and review the policy.');
          const history = await policyGet(
            `${endpoint}/history?page=1&pageSize=25`,
            historySchema(kind, 1),
            signal,
          );
          validatePublicationSource(kind, body, fresh, history.items[0] ?? null);
          const pending = await policyGet(
            `${endpoint}/requests?page=1&pageSize=25&state=Pending`,
            requestsSchema(kind, 1, 0),
            signal,
          );
          if (pending.totalCount > 0)
            throw new Error('Another proposal is pending. Reload and review it.');
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
  return (
    <section className="hr-form-section">
      <div className="foundation-heading">
        <h2>New {kind.toLowerCase()} policy proposal</h2>
        <Button
          variant="ghost"
          title="Close proposal"
          aria-label="Close proposal"
          disabled={mutation.locked}
          onClick={onClose}
        >
          <X size={16} />
        </Button>
      </div>
      <PolicyFacts
        rows={[
          ['Reviewed publication count', reviewed.current.policy?.latestVersion ?? 0],
          ['Reviewed business date', reviewed.current.selectedDate],
          [
            'Latest published start',
            reviewed.latest?.identity.effectiveFrom ?? 'No published version',
          ],
          [
            'Latest published end (exclusive)',
            reviewed.latest?.identity.effectiveTo ?? 'Open-ended',
          ],
        ]}
      />
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
      >
        <fieldset disabled={!allowed || mutation.locked}>
          <div className="foundation-grid">
            <TextInput
              label="Approved version label"
              required
              maxLength={100}
              value={approvedVersion}
              onChange={(event) => setApprovedVersion(event.target.value)}
            />
            <TextInput
              label="Effective from (inclusive)"
              type="date"
              required
              min={reviewed.current.selectedDate}
              value={effectiveFrom}
              onChange={(event) => setEffectiveFrom(event.target.value)}
            />
            <TextInput
              label="Effective to (exclusive)"
              type="date"
              value={effectiveTo}
              onChange={(event) => setEffectiveTo(event.target.value)}
            />
          </div>
          {reviewed.latest?.rules && reviewed.latest.state === 'published' && (
            <Button
              variant="secondary"
              onClick={() => setDraft(draftFromRules(reviewed.latest!.rules!))}
            >
              <Copy size={16} />
              Use latest published rules
            </Button>
          )}
          <RulesEditor kind={kind} draft={draft} onChange={setDraft} />
          <TextInput
            label="Proposal reason"
            required
            maxLength={2000}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
        </fieldset>
        <div className="hr-actions">
          <Button type="submit" disabled={!allowed || mutation.pending || mutation.reloadRequired}>
            <Send size={16} />
            {mutation.uncertain
              ? 'Retry unchanged proposal'
              : mutation.pending
                ? 'Submitting...'
                : 'Request publication'}
          </Button>
        </div>
      </form>
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
    </section>
  );
}
