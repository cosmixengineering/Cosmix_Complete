import { useEffect, useState } from 'react';
import { Check, X, Ban, CornerUpLeft } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { QueryState } from '../components/Feedback';
import { RecordView } from '../components/RecordView';
import { useHrAccess } from '../hooks/use-hr-access';
import { labelDate } from '../utils/messages';
import { canRecordAction, recordPermissions } from './access';
import {
  cancelSchema,
  decisionSchema,
  employeeVersionSchema,
  requestDetailSchema,
  type RecordKind,
  type RecordRequestDetail,
} from './contracts';
import { recordError, useRecordQuery, type RecordMutation } from './hooks';
import { actionLabels, requestStates } from './validation';

export function RecordRequestDetailView({
  instance,
  employeeId,
  kind,
  requestId,
  mutation,
  onDirty,
  onSaved,
  onClose,
}: {
  instance: string;
  employeeId: string;
  kind: RecordKind;
  requestId: string;
  mutation: RecordMutation;
  onDirty: (dirty: boolean) => void;
  onSaved: () => void;
  onClose: () => void;
}) {
  const path = `employees/${employeeId}/records/${kind}/requests/${requestId}`;
  const query = useRecordQuery(
    instance,
    path,
    requestDetailSchema(employeeId, kind, requestId),
    recordPermissions(kind),
  );
  return (
    <section className="hr-form-section">
      <div className="hr-repeat-heading">
        <h3>Request detail</h3>
        <Button
          variant="ghost"
          disabled={mutation.locked}
          onClick={onClose}
          title="Close request detail"
          aria-label="Close request detail"
        >
          <X size={16} />
        </Button>
      </div>
      <QueryState query={query}>
        {query.data && (
          <>
            <RecordView
              value={{
                state: requestStates[query.data.request.state],
                action: actionLabels[query.data.command.action],
                requestReference: query.data.request.id,
                requester: query.data.request.requesterId,
                requestedAt: labelDate(query.data.request.requestedAt),
                closedAt: labelDate(query.data.request.closedAt),
                approvedPolicyVersion: query.data.approvedPolicyVersion,
                effectiveAt: labelDate(query.data.command.effectiveAt),
                reason: query.data.command.reason,
                currentRecordReference: query.data.command.expectedCurrentRecordId,
              }}
            />
            <h4>Frozen proposal</h4>
            <RecordView
              value={
                query.data.command.asset ??
                query.data.command.sim ??
                query.data.command.certification ??
                {}
              }
            />
            <h4>Decisions</h4>
            {!query.data.decisions.length && <p className="muted">No decisions recorded.</p>}
            {query.data.decisions.map((decision) => (
              <RecordView
                key={decision.id}
                value={{
                  decision: requestStates[decision.decision],
                  actor: decision.actorId,
                  role: decision.roleCode,
                  reason: decision.reason,
                  decidedAt: labelDate(decision.decidedAt),
                }}
              />
            ))}
            <RequestActions
              key={query.data.request.version}
              instance={instance}
              employeeId={employeeId}
              kind={kind}
              path={path}
              detail={query.data}
              mutation={mutation}
              onDirty={onDirty}
              onSaved={onSaved}
            />
          </>
        )}
      </QueryState>
    </section>
  );
}
function RequestActions({
  instance,
  employeeId,
  kind,
  path,
  detail,
  mutation,
  onDirty,
  onSaved,
}: {
  instance: string;
  employeeId: string;
  kind: RecordKind;
  path: string;
  detail: RecordRequestDetail;
  mutation: RecordMutation;
  onDirty: (dirty: boolean) => void;
  onSaved: () => void;
}) {
  const access = useHrAccess();
  const canCancel = canRecordAction(access.user, kind, 'cancel', detail.request);
  const canReview = canRecordAction(access.user, kind, 'decision', detail.request);
  const current = useRecordQuery(
    instance,
    `employees/${employeeId}/current/basic`,
    employeeVersionSchema(employeeId),
    recordPermissions(kind, 'review'),
    canReview,
  );
  const [reason, setReason] = useState('');
  const [decision, setDecision] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [frozen, setFrozen] = useState<{
    body: unknown;
    operation: 'decision' | 'cancel';
    decision?: number;
  } | null>(null);
  useEffect(() => {
    onDirty(Boolean(reason || decision || frozen));
    return () => onDirty(false);
  }, [reason, decision, frozen, onDirty]);
  async function send(operation: 'decision' | 'cancel') {
    if (
      mutation.pending ||
      mutation.reloadRequired ||
      (operation === 'cancel' ? !canCancel : !canReview)
    )
      return;
    if (
      operation === 'decision' &&
      !frozen &&
      (current.isFetching || current.isError || !current.data)
    )
      return;
    setError(null);
    try {
      const attempt = frozen ?? {
        operation,
        decision: operation === 'decision' ? Number(decision) : undefined,
        body:
          operation === 'cancel'
            ? cancelSchema.parse({ expectedRequestVersion: detail.request.version })
            : decisionSchema.parse({
                expectedEmployeeVersion: current.data!.version,
                expectedRequestVersion: detail.request.version,
                decision: Number(decision),
                reason,
              }),
      };
      if (attempt.operation !== operation) return;
      if (
        !frozen &&
        !window.confirm(
          operation === 'cancel'
            ? 'Cancel this pending request?'
            : `${requestStates[attempt.decision!]}: record this decision?`,
        )
      )
        return;
      setFrozen(attempt);
      const result = await mutation.run(
        `${path}/${attempt.operation}`,
        attempt.body,
        detail.request.id,
        attempt.decision,
      );
      if (result) onSaved();
      else if (result === null) setFrozen(null);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : recordError(failure));
    }
  }
  if (!canCancel && !canReview) return null;
  return (
    <>
      {canCancel && (
        <div className="hr-actions">
          <Button
            variant="secondary"
            disabled={mutation.pending || mutation.reloadRequired}
            onClick={() => void send('cancel')}
          >
            <Ban size={16} />
            {mutation.uncertain ? 'Retry unchanged cancellation' : 'Cancel request'}
          </Button>
        </div>
      )}
      {canReview && (
        <QueryState query={current}>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void send('decision');
            }}
          >
            <h4>Admin decision</h4>
            <fieldset
              disabled={mutation.locked || current.isFetching || current.isError || !current.data}
            >
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
                onChange={(e) => setDecision(e.target.value)}
              />
              <TextInput
                label="Decision reason"
                required
                maxLength={1000}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </fieldset>
            <div className="hr-actions">
              <Button
                type="submit"
                disabled={
                  mutation.pending ||
                  mutation.reloadRequired ||
                  (!mutation.uncertain && (current.isFetching || current.isError || !current.data))
                }
              >
                {decision === '1' ? (
                  <Check size={16} />
                ) : decision === '2' ? (
                  <CornerUpLeft size={16} />
                ) : (
                  <X size={16} />
                )}
                {mutation.uncertain ? 'Retry unchanged decision' : 'Record decision'}
              </Button>
            </div>
          </form>
        </QueryState>
      )}
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
    </>
  );
}
