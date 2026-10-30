import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useEffect, useState } from 'react';
import { ArrowLeft, Eye, Search, ClipboardCheck } from 'lucide-react';
import { isSystemAdmin } from '@/auth/contracts';
import { TextInput } from '@/components/forms/TextInput';
import {
  Command,
  Denied,
  MutationForm,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { id } from '../../schemas/common';
import { canAll, stateNames } from '../leave-policy/inputs';
import { Choice, EvidenceRecord, JsonEvidence, Reload } from '../leave-policy/Ui';
import { EmployeeWorkspace } from '../review/ReviewUi';
import {
  permissions,
  recoveryClaim,
  recoveryDecisionIntent,
  recoveryDetail,
  recoveryDetailPath,
  recoveryProgress,
  recoveryProgressPath,
  recoveryQueue,
  recoveryQueuePath,
  type RecoveryClaim,
} from './api';

type Selection = { employeeId: string; id: string };
export function LeaveRecoveryReviewPage() {
  return (
    <PrivateWorkspace resource="leave-recovery-review">
      <RecoveryWorkspace />
    </PrivateWorkspace>
  );
}
function RecoveryWorkspace() {
  const access = useAttendanceAccess();
  const allowed =
    access.ready && canAll(access.user, permissions.recovery) && isSystemAdmin(access.user);
  const queue = useAttendanceQuery(recoveryQueuePath, recoveryQueue, allowed);
  const [selection, setSelection] = useState<Selection | null>(null);
  if (!allowed)
    return <Denied>Recovery review requires All-scoped leave review permission.</Denied>;
  if (selection)
    return (
      <RecoveryDetail
        key={`${selection.employeeId}:${selection.id}`}
        selection={selection}
        onSelect={setSelection}
        onBack={() => {
          setSelection(null);
          void queue.refetch();
        }}
        onSaved={() => void queue.refetch()}
      />
    );
  return (
    <>
      <div className="attendance-heading">
        <h1>Leave recovery work review</h1>
        <Reload disabled={queue.isFetching} onClick={() => void queue.refetch()} />
      </div>
      <QueryState
        query={queue}
        empty={queue.data?.length === 0}
        emptyText="No pending recovery work claims returned."
      />
      {!queue.isError && queue.data && (
        <ClaimTable claims={queue.data} disabled={queue.isFetching} onOpen={setSelection} />
      )}
      <section className="attendance-section">
        <EmployeeWorkspace title="Retained recovery claim" permission={permissions.recovery}>
          {(employeeId) => (
            <ClaimLookup key={employeeId} employeeId={employeeId} onOpen={setSelection} />
          )}
        </EmployeeWorkspace>
      </section>
    </>
  );
}
function ClaimLookup({
  employeeId,
  onOpen,
}: {
  employeeId: string;
  onOpen: (selection: Selection) => void;
}) {
  const [claimId, setClaimId] = useState('');
  const valid = id.safeParse(claimId).success;
  return (
    <form
      className="attendance-grid"
      onSubmit={(event) => {
        event.preventDefault();
        if (valid) onOpen({ employeeId, id: claimId });
      }}
    >
      <TextInput
        label="Recovery work claim ID"
        required
        value={claimId}
        onChange={(event) => setClaimId(event.target.value.trim())}
      />
      <Command icon={Search} type="submit" disabled={!valid}>
        Open retained claim
      </Command>
    </form>
  );
}
function ClaimTable({
  claims,
  disabled,
  onOpen,
}: {
  claims: RecoveryClaim[];
  disabled: boolean;
  onOpen: (selection: Selection) => void;
}) {
  return (
    <div className="hr-table-scroll">
      <table className="attendance-table">
        <thead>
          <tr>
            <th>Claim / employee</th>
            <th>Commitment</th>
            <th>Requested minutes</th>
            <th>Approved minutes</th>
            <th>Status</th>
            <th>Detail</th>
          </tr>
        </thead>
        <tbody>
          {claims.map((claim) => (
            <tr key={claim.id}>
              <td>
                {claim.id}
                <br />
                <EmployeeReference employeeId={claim.employeeId} />
              </td>
              <td>{claim.commitmentId}</td>
              <td>{claim.requestedMinutes}</td>
              <td>{claim.approvedMinutes ?? 'Not decided'}</td>
              <td>{stateNames[claim.state]}</td>
              <td>
                <Command
                  icon={Eye}
                  variant="ghost"
                  disabled={disabled}
                  onClick={() => onOpen({ employeeId: claim.employeeId, id: claim.id })}
                >
                  Open
                </Command>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
function RecoveryDetail({
  selection,
  onBack,
  onSelect,
  onSaved,
}: {
  selection: Selection;
  onBack: () => void;
  onSelect: (selection: Selection) => void;
  onSaved: () => void;
}) {
  const access = useAttendanceAccess();
  const allowed = access.ready && canAll(access.user, permissions.recovery);
  const query = useAttendanceQuery(
    recoveryDetailPath(selection.employeeId, selection.id),
    recoveryDetail,
    allowed,
  );
  const detail = !query.isError ? query.data : undefined;
  const claim = detail?.claim;
  const progress = useAttendanceQuery(
    claim ? recoveryProgressPath(claim) : '',
    recoveryProgress,
    allowed && Boolean(claim),
  );
  const [review, setReview] = useState<RecoveryClaim | null>(null);
  const [saved, setSaved] = useState<RecoveryClaim | null>(null);
  const savedReadFailed = query.isError || progress.isError;
  const visibleSaved = savedReadFailed ? null : saved;
  useEffect(() => {
    if (savedReadFailed && saved) setSaved(null);
  }, [savedReadFailed, saved]);
  const canReview = Boolean(
    claim &&
    claim.state === 0 &&
    access.user?.account?.employeeId &&
    access.user.account.employeeId !== claim.employeeId,
  );
  function reload() {
    void query.refetch();
    if (claim) void progress.refetch();
  }
  function close() {
    setReview(null);
    reload();
  }
  if (!allowed) return <Denied />;
  return (
    <>
      <div className="attendance-heading">
        <h1>Recovery work claim</h1>
        <div className="attendance-commands">
          <Command icon={ArrowLeft} variant="secondary" disabled={Boolean(review)} onClick={onBack}>
            Back to queue
          </Command>
          <Reload
            disabled={Boolean(review) || query.isFetching || progress.isFetching}
            onClick={reload}
          />
        </div>
      </div>
      <SavedNotice
        message={visibleSaved ? `Claim ${visibleSaved.id}: ${stateNames[visibleSaved.state]}.` : ''}
      />
      {visibleSaved && (
        <details>
          <summary>Saved decision response</summary>
          <EvidenceRecord value={visibleSaved} />
        </details>
      )}
      <QueryState query={query} />
      {detail && claim && (
        <>
          <EvidenceRecord value={claim} />
          {claim.employeeId === access.user?.account?.employeeId && (
            <p role="status">Your own recovery claim is read-only in management review.</p>
          )}
          <section className="attendance-section">
            <h2>Submitted work intervals</h2>
            <EvidenceRecord value={detail.request} />
          </section>
          <details>
            <summary>Retained employment, roster and verified work evidence</summary>
            <EvidenceRecord value={detail.retainedSource} />
          </details>
          <details>
            <summary>Retained recovery policy</summary>
            <JsonEvidence json={detail.retainedSource.policy.rulesJson} />
            <EvidenceRecord value={detail.retainedSource.policy.settings} />
          </details>
          {!review && canReview && (
            <Command
              icon={ClipboardCheck}
              disabled={query.isFetching}
              onClick={() => {
                setSaved(null);
                setReview(structuredClone(claim));
              }}
            >
              Review decision
            </Command>
          )}
          {review && (
            <RecoveryDecisionForm
              claim={review}
              onCancel={close}
              onSaved={(result) => {
                setSaved(recoveryClaim.parse(result));
                setReview(null);
                reload();
                onSaved();
              }}
            />
          )}
          <section className="attendance-section">
            <h2>Commitment progress</h2>
            <QueryState query={progress} />
            {!progress.isError && progress.data && (
              <>
                <EvidenceRecord
                  value={{
                    leaveRequestId: progress.data.leaveRequestId,
                    commitmentId: progress.data.commitmentId,
                    committedMinutes: progress.data.committedMinutes,
                    fulfilledMinutes: progress.data.fulfilledMinutes,
                    remainingMinutes: progress.data.remainingMinutes,
                    status: progress.data.status,
                  }}
                />
                <ClaimTable
                  claims={progress.data.claims}
                  disabled={Boolean(review) || progress.isFetching}
                  onOpen={onSelect}
                />
              </>
            )}
          </section>
        </>
      )}
    </>
  );
}
function RecoveryDecisionForm({
  claim,
  onSaved,
  onCancel,
}: {
  claim: RecoveryClaim;
  onSaved: (result: unknown) => void;
  onCancel: () => void;
}) {
  const access = useAttendanceAccess();
  const [decision, setDecision] = useState('');
  const [inputReason, setReason] = useState('');
  const allowed =
    access.ready &&
    canAll(access.user, permissions.recovery) &&
    Boolean(access.user?.account?.employeeId) &&
    access.user?.account?.employeeId !== claim.employeeId;
  return (
    <MutationForm
      title="Record recovery work decision"
      onSaved={onSaved}
      onCancel={onCancel}
      disabled={!allowed}
      submitLabel="Record decision"
      build={() => {
        if (!allowed) throw new Error('Independent recovery review permission is required.');
        return recoveryDecisionIntent(claim, decision, inputReason);
      }}
    >
      <Choice
        label="Decision"
        value={decision}
        onChange={setDecision}
        options={[
          { value: '1', label: 'Approve' },
          { value: '2', label: 'Return' },
          { value: '3', label: 'Reject' },
        ]}
      />
      <TextInput
        label="Decision reason"
        required
        maxLength={1000}
        value={inputReason}
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}
