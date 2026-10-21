import { useState } from 'react';
import { Plus, RefreshCw, Check, X, RotateCcw } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import {
  Command,
  Denied,
  MutationForm,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../../components/SchedulingUi';
import { queryString } from '../../api/attendance-api';
import { id } from '../../schemas/common';
import { canEmployee, canScope, canSelf, queueScopes, type Scope } from './access';
import { cancelInput, decisionInput, overtimeFinancial } from './contracts';
import {
  CursorControls,
  EmployeeWorkspace,
  EvidenceField,
  NumberedControls,
  RangeFilter,
  ReadResult,
  RecordFacts,
  ScopeSelect,
  SourceFacts,
  type Range,
} from './ReviewUi';
import { ProposalForm } from './ProposalForm';
import { overtimeReady } from './helpers';
import { historySchema, reviewPath, workflowPaths, workflows, type Workflow } from './workflows';

export function AttendanceCorrectionsPage() {
  return <RequestsPage workflow="correction" />;
}
export function MyAttendanceCorrectionsPage() {
  return <RequestsPage workflow="correction" self />;
}
export function AttendanceCorrectionReviewPage() {
  return <ReviewPage workflow="correction" />;
}
export function AttendanceCarryForwardPage() {
  return <RequestsPage workflow="carry" />;
}
export function MyAttendanceCarryForwardPage() {
  return <RequestsPage workflow="carry" self />;
}
export function AttendanceCarryForwardReviewPage() {
  return <ReviewPage workflow="carry" />;
}
export function AttendanceOvertimePage() {
  return <RequestsPage workflow="overtime" />;
}
export function MyAttendanceOvertimePage() {
  return <RequestsPage workflow="overtime" self />;
}
export function AttendanceOvertimeReviewPage() {
  return <ReviewPage workflow="overtime" />;
}

function RequestsPage({ workflow, self = false }: { workflow: Workflow; self?: boolean }) {
  return (
    <EmployeeWorkspace
      title={`${self ? 'My ' : ''}${workflows[workflow].title}`}
      permission={workflows[workflow].permission + '.submit'}
      self={self}
    >
      {(employeeId, scope, onEditing) => (
        <RequestSelection
          workflow={workflow}
          self={self}
          employeeId={employeeId}
          scope={scope}
          onEditing={onEditing}
        />
      )}
    </EmployeeWorkspace>
  );
}
function RequestSelection({
  workflow,
  self,
  employeeId,
  scope,
  onEditing,
}: {
  workflow: Workflow;
  self: boolean;
  employeeId: string;
  scope?: Scope;
  onEditing?: (value: boolean) => void;
}) {
  const access = useAttendanceAccess();
  const [range, setRange] = useState<Range>();
  const [proposal, setProposal] = useState<{ id: string; date: string } | null | undefined>();
  const [epoch, setEpoch] = useState(0);
  const [notice, setNotice] = useState('');
  const [editing, setEditing] = useState(false);
  const permission = workflows[workflow].permission;
  const allowed = self
    ? canSelf(access.user, permission + '.self')
    : canEmployee(access.user, permission + '.submit', employeeId, scope);
  function close() {
    setProposal(undefined);
    setEpoch((n) => n + 1);
    changeEditing(false);
  }
  function changeEditing(value: boolean) {
    setEditing(value);
    onEditing?.(value);
  }
  function openProposal(value: { id: string; date: string } | null) {
    changeEditing(true);
    setProposal(value);
  }
  if (!allowed) return <Denied>Matching request permission is unavailable.</Denied>;
  if (proposal !== undefined)
    return (
      <ProposalForm
        key={`${epoch}:${proposal?.id ?? 'new'}`}
        workflow={workflow}
        self={self}
        employeeId={employeeId}
        allowed={allowed}
        resubmission={proposal ?? undefined}
        onClose={close}
        onSaved={(result) => {
          const row = workflows[workflow].response.parse(result);
          close();
          setNotice(`Request ${row.id}: ${row.state}.`);
        }}
      />
    );
  return (
    <>
      <SavedNotice message={notice} />
      <Command
        icon={Plus}
        disabled={editing}
        onClick={() => {
          setNotice('');
          openProposal(null);
        }}
      >
        New request
      </Command>
      <RangeFilter
        states={!(self && workflow === 'correction')}
        onChange={setRange}
        disabled={editing}
      />
      {range && (
        <RequestHistory
          key={`${epoch}:${JSON.stringify(range)}`}
          workflow={workflow}
          self={self}
          employeeId={employeeId}
          scope={scope}
          range={range}
          onResubmit={openProposal}
          onEditing={changeEditing}
        />
      )}
    </>
  );
}
function ReviewPage({ workflow }: { workflow: Workflow }) {
  return (
    <PrivateWorkspace resource={`review:${workflow}`}>
      <ReviewSelection workflow={workflow} />
    </PrivateWorkspace>
  );
}
function ReviewSelection({ workflow }: { workflow: Workflow }) {
  const [scope, setScope] = useState<Scope>();
  const [range, setRange] = useState<Range>();
  const [editing, setEditing] = useState(false);
  return (
    <>
      <h1>{workflows[workflow].title} review</h1>
      <ScopeSelect
        permission={workflows[workflow].permission + '.review'}
        value={scope}
        disabled={editing}
        onChange={(s) => {
          setScope(s);
          setRange(undefined);
        }}
      />
      <RangeFilter states onChange={setRange} disabled={editing} />
      {scope && range && (
        <RequestHistory
          key={`${JSON.stringify(scope)}:${JSON.stringify(range)}`}
          workflow={workflow}
          self={false}
          employeeId=""
          scope={scope}
          range={range}
          review
          onEditing={setEditing}
        />
      )}
    </>
  );
}
type RequestHistoryProps = {
  workflow: Workflow;
  self: boolean;
  employeeId: string;
  scope?: Scope;
  range: Range;
  review?: boolean;
  onResubmit?: (value: { id: string; date: string }) => void;
  onEditing?: (value: boolean) => void;
};
function RequestHistory({
  workflow,
  self,
  employeeId,
  scope,
  range,
  review = false,
  onResubmit,
  onEditing,
}: RequestHistoryProps) {
  const access = useAttendanceAccess();
  const [page, setPage] = useState(1);
  const [cursors, setCursors] = useState<(string | undefined)[]>([undefined]);
  const [selected, setSelected] = useState<{ id: string; employeeId: string }>();
  const [editingDetail, setEditingDetail] = useState(false);
  const [epoch, setEpoch] = useState(0);
  const cursor = cursors[cursors.length - 1];
  const config = workflows[workflow];
  const allowed = review
    ? Boolean(scope && canScope(access.user, config.permission + '.review', scope))
    : self
      ? canSelf(access.user, config.permission + '.self')
      : canEmployee(access.user, config.permission + '.submit', employeeId, scope);
  const paths = workflowPaths(workflow, employeeId, self);
  const path =
    (review ? config.queue : paths.requests) +
    queryString({
      from: range.from,
      to: range.to,
      pageSize: range.pageSize,
      ...(self && workflow === 'correction' && !review ? { page } : { cursor, state: range.state }),
      ...(review
        ? {
            scope: scope ? queueScopes[scope.kind] : undefined,
            targetId: scope?.targetId ?? undefined,
          }
        : !self && workflow === 'correction'
          ? { scope: 2, targetId: employeeId }
          : {}),
    });
  const query = useAttendanceQuery(path, historySchema(workflow, review ? false : self), allowed);
  const data = !query.isError ? query.data : undefined;
  function reload() {
    setEditingDetail(false);
    onEditing?.(false);
    setSelected(undefined);
    setEpoch((n) => n + 1);
    void query.refetch();
  }
  if (!allowed) return <Denied>Matching request history/review permission is unavailable.</Denied>;
  return (
    <section className="attendance-section">
      <QueryState query={query} empty={data?.items.length === 0} />
      {!editingDetail && (
        <Command icon={RefreshCw} variant="secondary" disabled={query.isFetching} onClick={reload}>
          Reload requests
        </Command>
      )}
      {!editingDetail && data && (
        <>
          <div style={{ overflowX: 'auto' }}>
            <table className="attendance-table">
              <thead>
                <tr>
                  <th>Business date / employee</th>
                  <th>Kind / state</th>
                  <th>Retained result</th>
                  <th>Review</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((row) => (
                  <tr key={row.id}>
                    <td>
                      {'businessDate' in row ? row.businessDate : row.sourceBusinessDate}
                      <br />
                      {row.employeeId}
                    </td>
                    <td>
                      {'kind' in row ? row.kind : 'Overtime'} / {row.state}
                    </td>
                    <td>
                      {'routingStatus' in row ? row.routingStatus : row.durationState}
                      <br />
                      {'financialState' in row
                        ? row.financialState
                        : (row.interpretation ?? 'No applied interpretation returned')}
                    </td>
                    <td>
                      <button
                        type="button"
                        disabled={query.isFetching}
                        onClick={() => {
                          setSelected({ id: row.id, employeeId: row.employeeId });
                          setEpoch((n) => n + 1);
                        }}
                      >
                        Open {row.id}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {'totalCount' in data ? (
            <NumberedControls
              page={page}
              data={data}
              onChange={(p) => {
                setSelected(undefined);
                setPage(p);
              }}
              disabled={query.isFetching}
            />
          ) : (
            <CursorControls
              cursor={cursor}
              next={data.nextCursor}
              back={cursors.length > 1}
              disabled={query.isFetching}
              onBack={() => {
                setSelected(undefined);
                setCursors((old) => old.slice(0, -1));
              }}
              onNext={(next) => {
                setSelected(undefined);
                setCursors((old) => [...old, next]);
              }}
            />
          )}
        </>
      )}
      {selected && (
        <RequestDetail
          key={`${selected.id}:${epoch}`}
          workflow={workflow}
          self={self}
          review={review}
          employeeId={selected.employeeId}
          requestId={selected.id}
          scope={scope}
          allowed={allowed}
          onReload={reload}
          onEditing={(value) => {
            setEditingDetail(value);
            onEditing?.(value);
          }}
          onResubmit={onResubmit}
        />
      )}
    </section>
  );
}
function RequestDetail({
  workflow,
  self,
  review,
  employeeId,
  requestId,
  scope,
  allowed,
  onReload,
  onEditing,
  onResubmit,
}: {
  workflow: Workflow;
  self: boolean;
  review: boolean;
  employeeId: string;
  requestId: string;
  scope?: Scope;
  allowed: boolean;
  onReload: () => void;
  onEditing: (value: boolean) => void;
  onResubmit?: (value: { id: string; date: string }) => void;
}) {
  const access = useAttendanceAccess();
  const config = workflows[workflow];
  const base = review
    ? reviewPath(workflow, employeeId, requestId)
    : workflowPaths(workflow, employeeId, self).requests + '/' + requestId;
  const query = useAttendanceQuery(
    base + (review && workflow === 'carry' ? '/review' : ''),
    config.detail,
    allowed && id.safeParse(requestId).success,
  );
  const value = !query.isError ? query.data : undefined;
  const [editing, setEditing] = useState(false);
  const [reason, setReason] = useState('');
  const [decision, setDecision] = useState(1);
  const [award, setAward] = useState(false);
  const [notice, setNotice] = useState('');
  const retainedSource = value && ('source' in value ? value.source : value.capturedSource);
  const sourceSite =
    retainedSource &&
    ('siteId' in retainedSource ? retainedSource.siteId : retainedSource.historicalRoster?.siteId);
  const sourceScope = sourceSite ? { kind: 5, targetId: sourceSite } : undefined;
  const canDecide =
    review &&
    employeeId !== access.user?.account?.employeeId &&
    (canEmployee(access.user, config.permission + '.decide', employeeId, scope) ||
      canEmployee(access.user, config.permission + '.decide', employeeId, sourceScope));
  const ownEvidence =
    value && (!('evidenceVisibility' in value) || value.evidenceVisibility === 'authorized');
  const canCancel =
    !review &&
    ownEvidence &&
    (self
      ? canSelf(access.user, config.permission + '.self')
      : canEmployee(access.user, config.permission + '.submit', employeeId, scope));
  const canFinancial =
    workflow === 'overtime' &&
    (self
      ? canSelf(access.user, 'attendance.overtime.financial.self')
      : canEmployee(access.user, 'attendance.overtime.financial.read', employeeId, scope) ||
        canEmployee(access.user, 'attendance.overtime.financial.read', employeeId, sourceScope));
  function stop() {
    setEditing(false);
    onEditing(false);
    onReload();
  }
  const approveSupported =
    value &&
    ('durationPolicy' in ('source' in value ? value.source : value.capturedSource)
      ? 'source' in value && 'staffCategory' in value.source && overtimeReady(value.source)
      : 'capturedSource' in value
        ? !value.capturedSource.periodLocked && value.request.kind !== 'DayStatus'
        : true);
  return (
    <section className="attendance-section">
      <h2>Request {requestId}</h2>
      <QueryState query={query} />
      <SavedNotice message={notice} />
      {value && (
        <>
          <RecordFacts value={value.request} />
          <SourceFacts title="Frozen proposal and protected source" value={value} />
          {!editing && value.request.state === 'Pending' && (canDecide || canCancel) && (
            <Command
              icon={review ? Check : X}
              disabled={query.isFetching}
              onClick={() => {
                setEditing(true);
                onEditing(true);
              }}
            >
              {review ? 'Review decision' : 'Review cancellation'}
            </Command>
          )}
          {review && !canDecide && (
            <p>
              Decision authority for this resource is unavailable, or this request affects your own
              employee identity.
            </p>
          )}
          {self && !ownEvidence && (
            <p>
              This record was submitted by another requester. Cancellation and protected written
              evidence are unavailable through your self identity.
            </p>
          )}
          {!editing && value.request.state === 'Returned' && onResubmit && ownEvidence && (
            <Command
              icon={RotateCcw}
              disabled={query.isFetching}
              onClick={() =>
                onResubmit({
                  id: requestId,
                  date:
                    'businessDate' in value.request
                      ? value.request.businessDate
                      : value.request.sourceBusinessDate,
                })
              }
            >
              Resubmit with fresh source
            </Command>
          )}
          {editing && (
            <MutationForm
              title={review ? 'HR decision' : 'Cancel pending proposal'}
              disabled={
                query.isFetching ||
                value.request.state !== 'Pending' ||
                !(review ? canDecide : canCancel) ||
                (review && decision === 1 && !approveSupported)
              }
              build={() => ({
                path:
                  base + (review ? (workflow === 'carry' ? '/decision' : '/decide') : '/cancel'),
                method: 'POST',
                schema: review ? config.decision : config.response,
                body: review
                  ? decisionInput.parse({
                      expectedVersion: value.request.version,
                      decision,
                      reason,
                    })
                  : cancelInput.parse({ expectedVersion: value.request.version, reason }),
              })}
              onCancel={stop}
              onSaved={() => {
                setNotice(
                  'Server returned the completed request action. Reloading current review state.',
                );
                stop();
              }}
              submitLabel={review ? 'Submit decision' : 'Cancel request'}
            >
              <p>Expected version: {value.request.version}</p>
              {review && (
                <>
                  <SelectInput
                    label="Decision"
                    value={decision}
                    options={[
                      { value: '1', label: 'Approve' },
                      { value: '2', label: 'Return' },
                      { value: '3', label: 'Reject' },
                    ]}
                    onChange={(e) => setDecision(Number(e.target.value))}
                  />
                  <p>
                    One separately authorized HR reviewer decides. The server prohibits requester
                    self-review and rechecks source evidence. Approval does not settle payroll.
                  </p>
                  {!approveSupported && (
                    <p role="status">
                      Approval readiness is unavailable for this captured source. Return or reject
                      remains subject to server authority.
                    </p>
                  )}
                </>
              )}
              <EvidenceField label="Reason" max={1000} value={reason} onChange={setReason} />
            </MutationForm>
          )}
          {!editing && canFinancial && (
            <Command
              icon={RefreshCw}
              disabled={query.isFetching}
              variant="secondary"
              onClick={() => setAward(true)}
            >
              Read authorized award
            </Command>
          )}
          {award && canFinancial && (
            <ReadResult
              path={
                (self
                  ? `/me/overtime/requests/${requestId}`
                  : `/attendance/overtime/employees/${employeeId}/requests/${requestId}`) +
                '/financial'
              }
              schema={overtimeFinancial}
              allowed={canFinancial}
            >
              {(financial) => (
                <>
                  <h3>Server award</h3>
                  <RecordFacts value={financial} />
                  <p>A calculated award is pending payroll settlement.</p>
                </>
              )}
            </ReadResult>
          )}
          {!editing && (
            <Command
              icon={RefreshCw}
              variant="secondary"
              disabled={query.isFetching}
              onClick={onReload}
            >
              Reload current record
            </Command>
          )}
        </>
      )}
      {!value && query.isError && (
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={query.isFetching}
          onClick={onReload}
        >
          Reload review
        </Command>
      )}
      {!review && (
        <TextInput
          label="Record employee"
          value={self ? 'Authenticated employee (/me)' : employeeId}
          readOnly
        />
      )}
    </section>
  );
}
