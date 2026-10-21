import { useState } from 'react';
import { RefreshCw, Send } from 'lucide-react';
import type { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { useHrAction } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { useHrAccess } from '../../hooks/use-hr-access';
import { writeResult } from '../../schemas/contracts';
import { statusRequestInputSchema } from '../../schemas/requests';
import type { StatusState } from '../../types/requests';
import { FormSection, LongField } from '../hiring/FormFields';
import { ActionMessage } from '../Feedback';
import { isFutureStatusTime, statusRequestChoices } from './lifecycle-status';

export function StatusRequestForm({
  current,
  onSaved,
  onReload,
}: {
  current: StatusState;
  onSaved: () => void;
  onReload?: () => void;
}) {
  const access = useHrAccess();
  const [newStatus, setNewStatus] = useState('');
  const [reason, setReason] = useState<string | null>(null);
  const [scheduled, setScheduled] = useState(false);
  const [effective, setEffective] = useState('');
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [payload, setPayload] = useState<z.infer<typeof statusRequestInputSchema> | null>(null);
  const action = useHrAction();
  useDirtyForm(
    action.pending || (!sent && (!!newStatus || !!reason || !!effective || scheduled || !!payload)),
  );
  const choices = statusRequestChoices(current);
  const authorized = access.can('hr.employees.lifecycle.read', 'hr.employees.lifecycle.request');
  if (!authorized || !choices.length)
    return <p className="hr-notice">A new status request is not available.</p>;
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (!authorized || action.pending || sent) return;
        setError(null);
        const effectiveAt = scheduled ? effective + ':00+05:00' : null;
        const parsed = statusRequestInputSchema.safeParse({
          expectedVersion: current.version,
          newStatus: Number(newStatus),
          effectiveAt,
          reason,
        });
        if (
          !payload &&
          (!parsed.success ||
            !choices.some((choice) => choice.value === newStatus) ||
            !isFutureStatusTime(effectiveAt))
        ) {
          setError(
            'Select a supported status, enter a reason up to 1000 characters without control characters, and use a valid future effective time.',
          );
          return;
        }
        const body = payload ?? (parsed.success ? parsed.data : null);
        if (!body) return;
        setPayload(body);
        void action
          .run(
            `employees/${current.employeeId}/lifecycle/requests`,
            body,
            'POST',
            writeResult,
            () => setPayload(null),
          )
          .then((r) => {
            if (r) {
              setSent(true);
              onSaved();
            }
          });
      }}
    >
      <fieldset disabled={action.pending || sent}>
        <fieldset disabled={!!payload}>
          <FormSection title="Proposed employment status">
            <SelectInput
              label="New employment status"
              required
              value={newStatus}
              options={[{ value: '', label: 'Select status' }, ...choices]}
              onChange={(e) => setNewStatus(e.target.value)}
            />
            <div className="field">
              <label className="hr-check">
                <input
                  type="checkbox"
                  checked={scheduled}
                  onChange={(e) => setScheduled(e.target.checked)}
                />
                Scheduled effective time
              </label>
              {!scheduled && <span className="muted">Effective on approval</span>}
            </div>
            {scheduled && (
              <TextInput
                label="Effective time (Pakistan, UTC+05:00)"
                type="datetime-local"
                step="60"
                required
                value={effective}
                onChange={(e) => setEffective(e.target.value)}
              />
            )}
            <LongField
              label="Status change reason"
              required
              max={1000}
              value={reason}
              onChange={setReason}
            />
          </FormSection>
        </fieldset>
        <div className="hr-actions">
          <Button type="submit">
            <Send size={16} />
            {payload && action.error ? 'Retry status request' : 'Submit status request'}
          </Button>
        </div>
      </fieldset>
      {error && (
        <p className="hr-message hr-message--error" role="alert">
          {error}
        </p>
      )}
      <ActionMessage
        action={{
          error: action.error,
          success: sent
            ? 'Status request submitted. Current employment status has not changed.'
            : null,
        }}
      />
      {onReload &&
        action.errorStatus !== null &&
        action.errorStatus >= 400 &&
        action.errorStatus < 500 && (
          <Button
            variant="secondary"
            type="button"
            disabled={action.pending}
            onClick={() => {
              if (window.confirm('Reload current status and discard unsaved request details?'))
                onReload();
            }}
          >
            <RefreshCw size={16} />
            Reload current status
          </Button>
        )}
    </form>
  );
}
