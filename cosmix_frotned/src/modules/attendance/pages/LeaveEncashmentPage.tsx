import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useState } from 'react';
import { z } from 'zod';
import { ClipboardCheck, RefreshCw } from 'lucide-react';
import { SelectInput } from '@/components/forms/SelectInput';
import { isSystemAdmin } from '@/auth/contracts';
import {
  Command,
  Denied,
  MutationForm,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../components/SchedulingUi';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { reason as decisionReason, rowVersion, timestamp } from '../schemas/common';

const reviewPermission = 'attendance.leaves.review';
const decisionOptions = [
  { value: '1', label: 'Approve' },
  { value: '2', label: 'Return' },
  { value: '3', label: 'Reject' },
];
const stateLabels = ['Pending', 'Approved', 'Returned', 'Rejected', 'Cancelled'];
const approvalState = z.number().int().min(0).max(4);
const leaveDecision = z.object({
  id: z.uuid(),
  approvalRequestId: z.uuid(),
  decision: approvalState,
  actorRole: z.string(),
  reason: z.string().nullable(),
  atUtc: timestamp,
});
const encashment = z
  .object({
    id: z.uuid(),
    employeeId: z.uuid(),
    leaveYearStart: z.iso.date(),
    leaveYearEnd: z.iso.date(),
    requestedMinutes: z.number().int().positive(),
    assessedKpiPercent: z.number(),
    proposedAmount: z.number().nonnegative(),
    currency: z.string().min(1),
    policyVersionId: z.uuid(),
    approvalRequestId: z.uuid(),
    state: approvalState,
    approvalVersion: z.string(),
    createdAt: timestamp,
    decisions: z.array(leaveDecision),
  })
  .refine((item) =>
    item.decisions.every((entry) => entry.approvalRequestId === item.approvalRequestId),
  );
const encashmentList = z.array(encashment);
const decisionBody = z.object({
  decision: z.number().int().min(1).max(3),
  expectedApprovalVersion: rowVersion,
  reason: decisionReason(),
});
type Encashment = z.infer<typeof encashment>;

function canDecide(item: Encashment, employeeId: string | null | undefined) {
  return (
    item.state === 0 &&
    item.employeeId !== employeeId &&
    item.approvalRequestId !== '00000000-0000-0000-0000-000000000000' &&
    rowVersion.safeParse(item.approvalVersion).success
  );
}

export function LeaveEncashmentPage() {
  return (
    <PrivateWorkspace resource="leave-encashment-review">
      <EncashmentQueue />
    </PrivateWorkspace>
  );
}

function EncashmentQueue() {
  const access = useAttendanceAccess();
  const allowed =
    access.ready &&
    Boolean(access.user?.account) &&
    access.can(reviewPermission) &&
    isSystemAdmin(access.user);
  const list = useAttendanceQuery('/leaves/admin/encashment/queue', encashmentList, allowed);
  const [selected, setSelected] = useState<Encashment | null>(null);
  const [saved, setSaved] = useState('');
  if (!allowed)
    return (
      <Denied>
        Encashment review requires an All-scoped leave review grant and fixed Admin authority.
      </Denied>
    );

  function reload() {
    setSelected(null);
    void list.refetch();
  }

  return (
    <section aria-label="Leave encashment review">
      <div className="attendance-heading">
        <h1>Leave encashment review</h1>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={Boolean(selected) || list.isFetching}
          onClick={reload}
        >
          {list.isError ? 'Retry queue' : 'Refresh queue'}
        </Command>
      </div>
      <SavedNotice message={saved} />
      <QueryState
        query={list}
        empty={!selected && list.data?.length === 0}
        emptyText="No encashment applications in the review queue."
      />
      {selected ? (
        <>
          <EncashmentCard item={selected} />
          <EncashmentDecisionForm
            key={selected.id + ':' + selected.approvalVersion}
            item={selected}
            onCancel={reload}
            onSaved={(result) => {
              setSaved(
                `Encashment ${result.id}: ${stateLabels[result.state]}. Decision recorded; no payment released.`,
              );
              reload();
            }}
          />
        </>
      ) : (
        !list.isError &&
        !list.isPending &&
        !list.isFetching &&
        list.data?.map((item) => (
          <article className="hr-repeat" key={item.id}>
            <EncashmentCard item={item} />
            {canDecide(item, access.user?.account?.employeeId) ? (
              <div className="hr-actions">
                <Command
                  icon={ClipboardCheck}
                  variant="secondary"
                  onClick={() => {
                    setSaved('');
                    setSelected(item);
                  }}
                >
                  Review decision
                </Command>
              </div>
            ) : item.state === 0 ? (
              <p role="status">
                {item.employeeId === access.user?.account?.employeeId
                  ? 'Your own encashment requires an independent Admin reviewer.'
                  : 'Approval evidence is unavailable. Refresh the queue before reviewing.'}
              </p>
            ) : null}
          </article>
        ))
      )}
    </section>
  );
}

function EncashmentDecisionForm({
  item,
  onSaved,
  onCancel,
}: {
  item: Encashment;
  onSaved: (result: Encashment) => void;
  onCancel: () => void;
}) {
  const access = useAttendanceAccess();
  const [decision, setDecision] = useState('');
  const [reason, setReason] = useState('');
  const allowed =
    access.ready &&
    Boolean(access.user?.account) &&
    access.can(reviewPermission) &&
    canDecide(item, access.user?.account?.employeeId);
  return (
    <MutationForm
      title="Encashment decision"
      submitLabel="Record decision"
      disabled={!allowed}
      onCancel={onCancel}
      onSaved={(result) => onSaved(encashment.parse(result))}
      build={() => {
        if (!allowed) throw new Error('Encashment review is unavailable.');
        const body = decisionBody.parse({
          decision: Number(decision),
          expectedApprovalVersion: item.approvalVersion,
          reason,
        });
        return {
          path: `/leaves/admin/encashment/${item.id}/decisions`,
          method: 'POST',
          body,
          schema: encashment.refine(
            (result) =>
              result.id === item.id &&
              result.employeeId === item.employeeId &&
              result.approvalRequestId === item.approvalRequestId &&
              result.state === body.decision &&
              result.proposedAmount === item.proposedAmount &&
              result.decisions.some(
                (entry) => entry.decision === body.decision && entry.reason === body.reason,
              ),
            'The saved decision could not be confirmed for this encashment.',
          ),
        };
      }}
    >
      <SelectInput
        label="Decision"
        required
        value={decision}
        onChange={(event) => setDecision(event.target.value)}
        options={[{ value: '', label: 'Select a decision' }, ...decisionOptions]}
      />
      <label className="hr-field">
        <span>Reason</span>
        <textarea
          rows={3}
          required
          maxLength={1000}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />
      </label>
    </MutationForm>
  );
}

function EncashmentCard({ item }: { item: Encashment }) {
  return (
    <div>
      <div className="hr-repeat-heading">
        <h2>
          {item.requestedMinutes} minutes &middot; {item.currency} {item.proposedAmount.toFixed(2)}
        </h2>
        <span
          className={`hr-status ${
            item.state === 1
              ? 'hr-status--ok'
              : item.state === 0
                ? 'hr-status--neutral'
                : 'hr-status--error'
          }`}
        >
          {stateLabels[item.state]}
        </span>
      </div>
      <dl className="hr-detail-grid">
        <div>
          <dt>Employee</dt>
          <dd>
            <EmployeeReference employeeId={item.employeeId} />
          </dd>
        </div>
        <div>
          <dt>Application ID</dt>
          <dd>{item.id}</dd>
        </div>
        <div>
          <dt>Leave year</dt>
          <dd>
            {item.leaveYearStart} to {item.leaveYearEnd}
          </dd>
        </div>
        <div>
          <dt>Assessed KPI</dt>
          <dd>{item.assessedKpiPercent.toFixed(1)}%</dd>
        </div>
        <div>
          <dt>Policy version</dt>
          <dd>{item.policyVersionId}</dd>
        </div>
        <div>
          <dt>Submitted</dt>
          <dd>{new Date(item.createdAt).toLocaleString()}</dd>
        </div>
      </dl>
      {item.decisions.length > 0 && (
        <>
          <h3>Recorded decisions</h3>
          <ul>
            {item.decisions.map((entry) => (
              <li key={entry.id}>
                {stateLabels[entry.decision]} by {entry.actorRole} at{' '}
                {new Date(entry.atUtc).toLocaleString()}
                {entry.reason ? `: ${entry.reason}` : ''}
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
