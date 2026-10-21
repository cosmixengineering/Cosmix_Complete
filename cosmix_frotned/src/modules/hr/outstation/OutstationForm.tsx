import { useState, type FormEvent } from 'react';
import { z } from 'zod';
import { Check, RefreshCw, Send, X } from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { TextAreaField } from '../components/finance/FinanceChrome';
import { RecordView } from '../components/RecordView';
import { canRunDeployment, deploymentIntent, type FormTarget, type FormValues } from './commands';
import { useOutstationMutation } from './session';

export function OutstationForm({
  employeeId,
  target,
  onSaved,
  onClose,
}: {
  employeeId: string;
  target: FormTarget;
  onSaved: () => void;
  onClose: () => void;
}) {
  const { user, status } = useAuth();
  const action = useOutstationMutation();
  const [values, setValues] = useState<FormValues>({
    start: '',
    end: '',
    destination: '',
    category: '',
    writtenEvidence: '',
    reason: '',
    decision: '',
    supersedes: '',
  });
  const [error, setError] = useState<string | null>(null);
  const request = target.kind === 'request';
  const homeBreak = target.kind === 'break';
  const decision = !request && !homeBreak;
  const allowed = status === 'authenticated' && canRunDeployment(user, employeeId, target);
  const title = request
    ? 'Request deployment'
    : homeBreak
      ? 'Request home break / return'
      : target.kind === 'deployment-decision'
        ? 'Admin deployment decision'
        : target.kind === 'break-decision'
          ? 'Admin home-break review'
          : 'Admin retained-claim review';
  const row =
    target.kind === 'deployment-decision'
      ? target.detail?.deployment
      : target.kind === 'break-decision'
        ? target.detail?.homeBreaks.find((b) => b.id === target.id)
        : target.detail?.claims.find((c) => c.id === target.id);
  function field(name: keyof FormValues, value: string) {
    setValues((v) => ({ ...v, [name]: value }));
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!allowed || action.locked) return;
    setError(null);
    try {
      const intent = deploymentIntent(employeeId, target, values);
      if (!window.confirm(`${title}? This records a request or decision, not a payment.`)) return;
      const result = await action.run(intent);
      if (result) onSaved();
    } catch (failure) {
      setError(
        failure instanceof z.ZodError
          ? (failure.issues[0]?.message ?? 'Check the request values.')
          : failure instanceof Error
            ? failure.message
            : 'Check the request values.',
      );
    }
  }
  async function retry() {
    if (!allowed || action.pending) return;
    if (await action.run()) onSaved();
  }
  return (
    <section className="hr-form-section">
      <h3>{title}</h3>
      <RecordView
        value={{
          employeeId,
          deploymentId: target.detail?.deployment.id,
          deploymentVersion: target.detail?.deployment.version,
          approvalVersion: row?.approvalVersion,
          requestId: row?.id,
        }}
      />
      {decision && (
        <p className="hr-notice">
          Independent Admin/SuperAdmin review is required. The server checks role, requester
          separation, current versions and retained evidence.
        </p>
      )}
      <form onSubmit={(event) => void submit(event)}>
        <fieldset disabled={!allowed || action.locked} className="hr-form-grid">
          {(request || homeBreak) && (
            <>
              {homeBreak && (
                <SelectInput
                  label="Approved open break to replace"
                  value={values.supersedes}
                  options={[
                    { value: '', label: 'New home break' },
                    ...(target.detail?.homeBreaks
                      .filter((b) => b.state === 'Approved' && b.returnedAtUtc === null)
                      .map((b) => ({ value: b.id, label: `${b.departedAtUtc} / ${b.id}` })) ?? []),
                  ]}
                  onChange={(event) => {
                    const id = event.target.value;
                    const prior = target.detail?.homeBreaks.find((b) => b.id === id);
                    setValues((v) => ({
                      ...v,
                      supersedes: id,
                      start: prior ? prior.departedAtUtc.replace(/(?:Z|\+00:00)$/, '') : '',
                      end: '',
                    }));
                  }}
                />
              )}
              <TextInput
                label={request ? 'Deployment start (UTC)' : 'Departed home (UTC)'}
                type="datetime-local"
                step="any"
                min="1901-01-01T00:00"
                max="9997-12-31T23:59"
                required
                value={values.start}
                disabled={homeBreak && !!values.supersedes}
                onChange={(e) => field('start', e.target.value)}
              />
              <TextInput
                label={
                  request
                    ? 'Deployment end (UTC)'
                    : 'Returned to duty (UTC, optional for a new break)'
                }
                type="datetime-local"
                step="any"
                min="1901-01-01T00:00"
                max="9997-12-31T23:59"
                required={request || !!values.supersedes}
                value={values.end}
                onChange={(e) => field('end', e.target.value)}
              />
            </>
          )}
          {request && (
            <>
              <TextInput
                label="Destination"
                required
                maxLength={500}
                value={values.destination}
                onChange={(e) => field('destination', e.target.value)}
              />
              <TextInput
                label="Deployment category code"
                required
                maxLength={60}
                pattern="[A-Z0-9_-]+"
                value={values.category}
                onChange={(e) => field('category', e.target.value)}
              />
            </>
          )}
          {decision && (
            <SelectInput
              label="Decision"
              required
              value={values.decision}
              options={[
                { value: '', label: 'Select decision' },
                { value: '1', label: 'Approve' },
                { value: '2', label: 'Return' },
                { value: '3', label: 'Reject' },
              ]}
              onChange={(e) => field('decision', e.target.value)}
            />
          )}
          <TextAreaField
            label="Written evidence (1-8000 characters, no line breaks)"
            required
            value={values.writtenEvidence}
            onChange={(v) => field('writtenEvidence', v)}
          />
          <TextAreaField
            label="Reason (1-1000 characters, no line breaks)"
            required
            value={values.reason}
            onChange={(v) => field('reason', v)}
          />
          <div className="hr-actions wide">
            <Button type="submit">
              {decision ? <Check size={16} /> : <Send size={16} />}
              {action.pending ? 'Recording...' : title}
            </Button>
          </div>
        </fieldset>
      </form>
      {!allowed && (
        <p role="alert">
          This action is not available for the current permission, employee or request state.
        </p>
      )}
      {(error || action.error) && (
        <p className="hr-message hr-message--error" role="alert">
          {error || action.error}
        </p>
      )}
      {action.uncertain && (
        <p role="status">
          Outcome unconfirmed. The exact request and key are retained for retry; edits are locked.
        </p>
      )}
      {action.reloadRequired && (
        <p role="status">Reload and review current history before starting another action.</p>
      )}
      <div className="hr-actions">
        {action.uncertain && (
          <Button
            variant="secondary"
            disabled={action.pending || !allowed}
            onClick={() => void retry()}
          >
            <RefreshCw size={16} />
            Retry unchanged action
          </Button>
        )}
        <Button
          variant="secondary"
          disabled={action.pending}
          onClick={() => {
            if (
              window.confirm(
                action.uncertain
                  ? 'Discard the retained retry and reload history to check whether it completed?'
                  : 'Close this form and reload current records? Unsaved evidence will be discarded.',
              )
            )
              onClose();
          }}
        >
          {action.locked ? <RefreshCw size={16} /> : <X size={16} />}
          {action.locked ? 'Reload current records' : 'Cancel'}
        </Button>
      </div>
    </section>
  );
}
