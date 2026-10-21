import { useEffect, useState } from 'react';
import { Check, CornerUpLeft, X, RefreshCw } from 'lucide-react';
import type { z } from 'zod';
import type { SessionUser } from '@/auth/types';
import { Button } from '@/components/ui/Button';
import { SelectInput } from '@/components/forms/SelectInput';
import { hasHrPermission, useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { writeResult } from '../../schemas/contracts';
import { LongField } from '../hiring/FormFields';
import { ActionMessage, QueryState } from '../Feedback';

type ReviewRequest = { employeeId: string; requesterId: string; state: number; version: string };
export type DecisionGuard = { dirty: boolean; pending: boolean };

function canReviewRequest(user: SessionUser | null, request: ReviewRequest, permissions: string[]) {
  const account = user?.account;
  return Boolean(
    account &&
    request.state === 0 &&
    account.accountId.toLowerCase() !== request.requesterId.toLowerCase() &&
    account.employeeId.toLowerCase() !== request.employeeId.toLowerCase() &&
    permissions.every((permission) => hasHrPermission(user, permission)),
  );
}

export function RequestDecisionForm({
  request,
  path,
  currentPath,
  currentSchema,
  permissions,
  onSaved,
  onGuardChange,
}: {
  request: ReviewRequest;
  path: string;
  currentPath: string;
  currentSchema: z.ZodType<
    { employeeId: string } & ({ version: string } | { employeeVersion: string })
  >;
  permissions: string[];
  onSaved: () => void;
  onGuardChange: (guard: DecisionGuard) => void;
}) {
  const access = useHrAccess();
  const authorized = access.ready && canReviewRequest(access.user, request, permissions);
  const current = useHrQuery(currentPath, currentSchema, permissions, authorized);
  const action = useHrAction();
  const [decision, setDecision] = useState('');
  const [reason, setReason] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);
  const [payload, setPayload] = useState<{
    expectedEmployeeVersion: string;
    expectedRequestVersion: string;
    decision: number;
    reason: string | null;
  } | null>(null);
  const dirty = !sent && (!!decision || !!reason || !!payload);
  useDirtyForm(dirty || action.pending);
  useEffect(() => {
    onGuardChange({ dirty, pending: action.pending });
  }, [dirty, action.pending, onGuardChange]);
  useEffect(() => () => onGuardChange({ dirty: false, pending: false }), [onGuardChange]);
  const { refetch } = current;
  useEffect(() => {
    if (authorized) void refetch();
  }, [authorized, refetch]);

  if (!authorized) return null;
  const label = decision === '1' ? 'Approve' : decision === '2' ? 'Return' : 'Reject';
  const Icon = decision === '1' ? Check : decision === '2' ? CornerUpLeft : X;
  const ready =
    current.isFetchedAfterMount &&
    !current.isFetching &&
    !current.isError &&
    current.data?.employeeId === request.employeeId;
  return (
    <section className="hr-form-section">
      <h3>Admin decision</h3>
      <QueryState query={current}>
        {!ready && (
          <p className="hr-notice" role="status">
            Loading current employee version...
          </p>
        )}
        <form
          onSubmit={(event) => {
            event.preventDefault();
            setError(null);
            if (!authorized || !ready || !current.data || action.pending || sent) return;
            if (
              !['1', '2', '3'].includes(decision) ||
              (decision !== '1' && !reason?.trim()) ||
              (reason?.length ?? 0) > 2000 ||
              [...(reason ?? '')].some((character) => {
                const code = character.charCodeAt(0);
                return code < 32 || (code >= 127 && code <= 159);
              })
            ) {
              setError(
                'Select a decision. Return and Reject require a reason, up to 2000 characters without control characters.',
              );
              return;
            }
            if (!window.confirm(`${label} this pending request? This decision will be recorded.`))
              return;
            // Freeze versions and reason so an uncertain retry keeps the exact body and key.
            const body = payload ?? {
              expectedEmployeeVersion:
                'employeeVersion' in current.data
                  ? current.data.employeeVersion
                  : current.data.version,
              expectedRequestVersion: request.version,
              decision: Number(decision),
              reason: reason?.trim() || null,
            };
            setPayload(body);
            void action
              .run(path + '/decision', body, 'POST', writeResult, () => setPayload(null))
              .then((result) => {
                if (result) {
                  setSent(true);
                  onSaved();
                }
              });
          }}
        >
          <fieldset disabled={!ready || action.pending || sent}>
            <fieldset disabled={!!payload}>
              <SelectInput
                label="Decision"
                required
                value={decision}
                options={[
                  { value: '', label: 'Select decision' },
                  { value: '1', label: 'Approve' },
                  { value: '2', label: 'Return' },
                  { value: '3', label: 'Reject' },
                ]}
                onChange={(event) => setDecision(event.target.value)}
              />
              <LongField
                label="Decision reason"
                required={decision === '2' || decision === '3'}
                max={2000}
                value={reason}
                onChange={setReason}
              />
            </fieldset>
            <div className="hr-actions">
              <Button type="submit" disabled={!decision}>
                <Icon size={16} />
                {payload && action.error
                  ? `Retry ${label.toLowerCase()}`
                  : decision
                    ? label + ' request'
                    : 'Record decision'}
              </Button>
            </div>
          </fieldset>
          {error && (
            <p className="hr-message hr-message--error" role="alert">
              {error}
            </p>
          )}
          <ActionMessage action={action} />
          {action.errorStatus !== null && action.errorStatus >= 400 && action.errorStatus < 500 && (
            <Button
              variant="secondary"
              type="button"
              disabled={action.pending}
              onClick={() => {
                if (
                  dirty &&
                  !window.confirm(
                    'Reload this request and its versions? Unsaved decision details may be discarded.',
                  )
                )
                  return;
                setPayload(null);
                void current.refetch();
                onSaved();
              }}
            >
              <RefreshCw size={16} />
              Reload request and versions
            </Button>
          )}
        </form>
      </QueryState>
    </section>
  );
}
