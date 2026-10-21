import { useState } from 'react';
import { Send, RotateCcw } from 'lucide-react';
import { z } from 'zod';
import { useAuth } from '@/auth/use-auth';
import { TextInput } from '@/components/forms/TextInput';
import { Button } from '@/components/ui/Button';
import { proposalSchema, type Rules, type TaxonomyPolicy } from './contracts';
import { draftRules, initialMappings } from './draft';
import { MappingEditor } from './MappingEditor';
import { CapabilityBoundary, MutationFeedback } from './Views';
import type { TaxonomyMutation } from './hooks';

export function ProposalForm({
  policy,
  source,
  mutation,
  onDirty,
  onSaved,
}: {
  policy: TaxonomyPolicy | null;
  source?: Rules;
  mutation: TaxonomyMutation;
  onDirty: () => void;
  onSaved: () => void;
}) {
  const { user } = useAuth();
  const [rows, setRows] = useState(() => initialMappings(source));
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [reason, setReason] = useState('');
  const [evidence, setEvidence] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [retained, setRetained] = useState<z.infer<ReturnType<typeof proposalSchema>> | null>(null);
  function change(action: () => void) {
    action();
    setError(null);
    onDirty();
  }
  async function submit() {
    if (!user?.account || mutation.pending || mutation.reloadRequired) return;
    try {
      const body =
        retained ??
        proposalSchema(policy).parse({
          expectedPolicyVersion: policy?.version ?? null,
          expectedRevision: policy?.revision ?? 0,
          effectiveFrom: from,
          effectiveTo: to || null,
          rules: draftRules(rows),
          reason: reason.trim(),
          writtenEvidence: evidence.trim(),
        });
      setError(null);
      setRetained(body);
      const result = await mutation.run(body, {
        action: 'request',
        policy,
        actorId: user.account.accountId,
      });
      if (result) onSaved();
      else if (result === null) setRetained(null);
    } catch (failure) {
      setError(
        failure instanceof z.ZodError
          ? (failure.issues[0]?.message ?? 'Review the proposal.')
          : 'Review the proposal values.',
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
      <h2>New taxonomy proposal</h2>
      <p className="taxonomy-notice">
        Approval authority status: not supplied. Policy version lookup: unavailable.
      </p>
      <fieldset className="taxonomy-form-fields" disabled={mutation.locked}>
        <div className="taxonomy-grid">
          <TextInput
            label="Effective from"
            type="date"
            value={from}
            onChange={(e) => change(() => setFrom(e.target.value))}
          />
          <TextInput
            label="Effective to (exclusive)"
            type="date"
            value={to}
            onChange={(e) => change(() => setTo(e.target.value))}
          />
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
      </fieldset>
      <MappingEditor
        rows={rows}
        disabled={mutation.locked}
        onChange={(next) => change(() => setRows(next))}
      />
      <CapabilityBoundary />
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
      <MutationFeedback mutation={mutation} />
      <div className="taxonomy-toolbar">
        <Button type="submit" disabled={mutation.pending || mutation.reloadRequired}>
          {mutation.uncertain ? <RotateCcw size={16} /> : <Send size={16} />}
          {mutation.uncertain ? 'Retry unchanged action' : 'Submit proposal'}
        </Button>
      </div>
    </form>
  );
}
