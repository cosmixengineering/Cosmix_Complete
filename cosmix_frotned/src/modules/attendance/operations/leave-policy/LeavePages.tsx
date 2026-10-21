import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Eye, Plus, X } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { Command, Denied, PrivateWorkspace, QueryState } from '../../components/SchedulingUi';
import { queryString } from '../../api/attendance-api';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { date, id } from '../../schemas/common';
import { businessToday } from '../../utils/messages';
import {
  leaveBalance,
  leavePage,
  leaveRecord,
  leaveTypes,
  selfPolicies,
  type LeaveRecord,
} from './contracts';
import { canAll, canSelfLeave, stateNames, treatmentNames } from './inputs';
import { Choice, EvidenceRecord, Reload } from './Ui';
import {
  LeaveCancellationForm,
  LeaveDecisionForm,
  LeaveIntervalPreview,
  LeaveRequestForm,
  PrivateLeaveEvidence,
} from './LeaveForms';

export function LeaveSelfPage() {
  return (
    <PrivateWorkspace resource="leave-self">
      <LeaveList admin={false} />
    </PrivateWorkspace>
  );
}
export function LeaveAdminPage() {
  return (
    <PrivateWorkspace resource="leave-admin">
      <LeaveList admin />
    </PrivateWorkspace>
  );
}
export function LeaveSelfDetailPage() {
  const { leaveId = '' } = useParams();
  return (
    <PrivateWorkspace resource={`leave-self:${leaveId}`}>
      <LeaveDetail recordId={leaveId} admin={false} />
    </PrivateWorkspace>
  );
}
export function LeaveAdminDetailPage() {
  const { leaveId = '' } = useParams();
  return (
    <PrivateWorkspace resource={`leave-admin:${leaveId}`}>
      <LeaveDetail recordId={leaveId} admin />
    </PrivateWorkspace>
  );
}
function validRange(from: string, to: string) {
  return (
    date.safeParse(from).success &&
    date.safeParse(to).success &&
    from >= '1901-01-01' &&
    to <= '9997-12-31' &&
    to >= from &&
    Date.parse(to) - Date.parse(from) <= 365 * 86400000
  );
}
function LeaveList({ admin }: { admin: boolean }) {
  const access = useAttendanceAccess();
  const allowed =
    access.ready &&
    (admin ? canAll(access.user, 'attendance.leaves.review') : canSelfLeave(access.user));
  const navigate = useNavigate();
  const [from, setFrom] = useState(businessToday);
  const [to, setTo] = useState(businessToday);
  const [page, setPage] = useState(1);
  const [pendingOnly, setPendingOnly] = useState(false);
  const [creating, setCreating] = useState(false);
  const [submitted, setSubmitted] = useState<LeaveRecord | null>(null);
  const rangeValid = validRange(from, to);
  const query = useAttendanceQuery(
    `/leaves/${admin ? 'admin/queue' : 'mine'}` +
      queryString({ from, to, page, pageSize: 25, pendingOnly }),
    leavePage,
    allowed && rangeValid,
  );
  if (!allowed)
    return (
      <Denied>
        {admin
          ? 'Leave review requires an explicit All-scoped review grant and fixed Admin authority.'
          : 'Own leave access is unavailable.'}
      </Denied>
    );
  function reload() {
    setCreating(false);
    void query.refetch();
  }
  return (
    <>
      <div className="attendance-heading">
        <h1>{admin ? 'Admin leave review' : 'My leave'}</h1>
        <div className="attendance-commands">
          <Reload onClick={reload} disabled={creating || query.isFetching || !rangeValid} />
          {!admin && !creating && (
            <Command icon={Plus} onClick={() => setCreating(true)}>
              Request leave
            </Command>
          )}
        </div>
      </div>
      {submitted && (
        <div className="attendance-section">
          <p role="status">
            Leave {submitted.id}: {stateNames[submitted.state]}.
          </p>
          <Command
            icon={Eye}
            variant="secondary"
            disabled={creating}
            onClick={() => navigate(`/account/leaves/${submitted.id}`)}
          >
            Open submitted leave
          </Command>
        </div>
      )}
      {creating ? (
        <LeaveRequestForm
          onSaved={(result) => {
            setSubmitted(result);
            reload();
          }}
          onCancel={reload}
        />
      ) : (
        <>
          <section className="attendance-section">
            <div className="attendance-grid">
              <TextInput
                label="From (inclusive)"
                type="date"
                value={from}
                onChange={(e) => {
                  setFrom(e.target.value);
                  setPage(1);
                }}
              />
              <TextInput
                label="To (inclusive)"
                type="date"
                value={to}
                onChange={(e) => {
                  setTo(e.target.value);
                  setPage(1);
                }}
              />
            </div>
            {admin && (
              <label className="attendance-check">
                <input
                  type="checkbox"
                  checked={pendingOnly}
                  onChange={(e) => {
                    setPendingOnly(e.target.checked);
                    setPage(1);
                  }}
                />
                Pending leave or cancellation only
              </label>
            )}
            {!rangeValid ? (
              <p role="alert">Select an inclusive range of at most 366 dates.</p>
            ) : (
              <>
                <QueryState query={query} empty={query.data?.items.length === 0} />
                {!query.isError && query.data && (
                  <>
                    <div className="table-scroll">
                      <table className="attendance-table">
                        <thead>
                          <tr>
                            <th>Type</th>
                            {admin && <th>Employee ID</th>}
                            <th>Dates</th>
                            <th>Server minutes</th>
                            <th>State</th>
                            <th>Detail</th>
                          </tr>
                        </thead>
                        <tbody>
                          {query.data.items.map((row) => (
                            <tr key={row.id}>
                              <td>{row.typeName}</td>
                              {admin && <td>{row.employeeId}</td>}
                              <td>
                                {row.startDate} to {row.endDate}
                              </td>
                              <td>{row.requestedMinutes}</td>
                              <td>
                                {stateNames[row.state]}
                                {row.cancellations.some((c) => c.state === 0)
                                  ? ' / cancellation pending'
                                  : ''}
                              </td>
                              <td>
                                <Command
                                  icon={Eye}
                                  variant="secondary"
                                  onClick={() =>
                                    navigate(
                                      `${admin ? '/hr/attendance/leaves' : '/account/leaves'}/${row.id}`,
                                    )
                                  }
                                >
                                  Open
                                </Command>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <Pagination
                      page={page}
                      pageSize={25}
                      total={query.data.totalCount}
                      onChange={setPage}
                      disabled={query.isFetching}
                    />
                  </>
                )}
              </>
            )}
          </section>
          {!admin && <SelfBalance />}
        </>
      )}
    </>
  );
}
function SelfBalance() {
  const types = useAttendanceQuery('/leaves/mine/types', leaveTypes, true);
  const [type, setType] = useState('');
  const [from, setFrom] = useState(businessToday);
  const [to, setTo] = useState(businessToday);
  const [asOf, setAsOf] = useState(businessToday);
  const [selection, setSelection] = useState<{ path: string; id: string } | null>(null);
  const [days, setDays] = useState([{ key: crypto.randomUUID(), start: '', end: '' }]);
  const [treatment, setTreatment] = useState('');
  const path = '/leaves/mine/policies' + queryString({ leaveTypeId: type, from, to });
  const policies = useAttendanceQuery(
    path,
    selfPolicies,
    id.safeParse(type).success && validRange(from, to),
  );
  const policy = selection?.path === path ? selection.id : '';
  const balance = useAttendanceQuery(
    '/leaves/mine/balance' + queryString({ leaveTypeId: type, policyVersionId: policy, asOf }),
    leaveBalance,
    Boolean(policy) && date.safeParse(asOf).success,
  );
  return (
    <section className="attendance-section">
      <h2>Fiscal balance and interval preview</h2>
      <QueryState query={types} />
      <Choice
        label="Leave type"
        value={type}
        options={(types.data ?? [])
          .filter((t) => t.isActive)
          .map((t) => ({ value: t.id, label: t.name }))}
        onChange={(value) => {
          setType(value);
          setSelection(null);
        }}
      />
      <div className="attendance-grid">
        <TextInput
          label="Policy range from"
          type="date"
          value={from}
          onChange={(e) => {
            setFrom(e.target.value);
            setSelection(null);
          }}
        />
        <TextInput
          label="Policy range to (inclusive)"
          type="date"
          value={to}
          onChange={(e) => {
            setTo(e.target.value);
            setSelection(null);
          }}
        />
      </div>
      {type && validRange(from, to) && (
        <>
          <QueryState query={policies} />
          {!policies.isError && policies.data && (
            <>
              <p>
                {policies.data.status}: {policies.data.reasons.join('; ')}
              </p>
              <Choice
                label="Published policy version"
                value={policy}
                options={policies.data.policies.map((p) => ({
                  value: p.versionId,
                  label: `${p.code}: ${p.effectiveFrom} to ${p.effectiveTo ?? 'open'}`,
                }))}
                onChange={(value) => setSelection({ path, id: value })}
              />
              <EvidenceRecord value={policies.data.policies.find((p) => p.versionId === policy)} />
            </>
          )}
          <Reload
            onClick={() => {
              setSelection(null);
              void policies.refetch();
            }}
            disabled={policies.isFetching}
          />
        </>
      )}
      {policy && (
        <>
          <TextInput
            label="Balance as of"
            type="date"
            value={asOf}
            onChange={(e) => setAsOf(e.target.value)}
          />
          <QueryState query={balance} />
          {!balance.isError && balance.data && <EvidenceRecord value={balance.data} />}
          <Reload onClick={() => void balance.refetch()} disabled={balance.isFetching} />
          <h3>Exact proposed intervals (Asia/Karachi)</h3>
          {days.map((day, i) => (
            <div className="attendance-grid" key={day.key}>
              <TextInput
                label={`Start ${i + 1}`}
                type="datetime-local"
                step={60}
                value={day.start}
                onChange={(e) =>
                  setDays((rows) =>
                    rows.map((d) => (d.key === day.key ? { ...d, start: e.target.value } : d)),
                  )
                }
              />
              <TextInput
                label={`End ${i + 1}`}
                type="datetime-local"
                step={60}
                value={day.end}
                onChange={(e) =>
                  setDays((rows) =>
                    rows.map((d) => (d.key === day.key ? { ...d, end: e.target.value } : d)),
                  )
                }
              />
              <Command
                icon={X}
                variant="secondary"
                disabled={days.length === 1}
                onClick={() => setDays((rows) => rows.filter((d) => d.key !== day.key))}
              >
                Remove interval
              </Command>
            </div>
          ))}
          <Command
            icon={Plus}
            variant="secondary"
            disabled={days.length >= 366}
            onClick={() =>
              setDays((rows) => [...rows, { key: crypto.randomUUID(), start: '', end: '' }])
            }
          >
            Add interval
          </Command>
          <Choice
            label="Proposed treatment"
            value={treatment}
            options={treatmentNames.slice(1).map((label, i) => ({ value: String(i + 1), label }))}
            onChange={setTreatment}
          />
          <LeaveIntervalPreview type={type} policy={policy} treatment={treatment} days={days} />
        </>
      )}
    </section>
  );
}
function LeaveDetail({ recordId, admin }: { recordId: string; admin: boolean }) {
  const access = useAttendanceAccess();
  const allowed =
    access.ready &&
    (admin ? canAll(access.user, 'attendance.leaves.review') : canSelfLeave(access.user));
  const [form, setForm] = useState('');
  const [epoch, setEpoch] = useState(0);
  const query = useAttendanceQuery(
    `/leaves/${admin ? 'admin' : 'mine'}/${recordId}`,
    leaveRecord,
    allowed && id.safeParse(recordId).success,
  );
  function reload() {
    setForm('');
    setEpoch((e) => e + 1);
    void query.refetch();
  }
  if (!allowed) return <Denied />;
  if (!id.safeParse(recordId).success) return <p role="alert">Invalid leave ID.</p>;
  const record = query.isError || query.isFetching ? undefined : query.data;
  const canReview = admin && record?.employeeId !== access.user?.account?.employeeId;
  return (
    <>
      <div className="attendance-heading">
        <h1>{admin ? 'Leave review' : 'My leave detail'}</h1>
        <Reload onClick={reload} disabled={Boolean(form) || query.isFetching} />
      </div>
      <QueryState query={query} />
      {record && (
        <div key={`${record.id}:${record.approvalVersion}:${epoch}`}>
          <LeaveHistory record={record} admin={admin} editing={Boolean(form)} />
          {form === 'correct' ? (
            <LeaveRequestForm previous={record} onSaved={reload} onCancel={reload} />
          ) : form === 'cancel' ? (
            <LeaveCancellationForm record={record} onSaved={reload} onCancel={reload} />
          ) : form ? (
            <LeaveDecisionForm
              record={record}
              cancellationId={form === 'review' ? undefined : form}
              onSaved={reload}
              onCancel={reload}
            />
          ) : (
            <div className="attendance-commands">
              {!admin && record.state === 2 && !record.supersededByRequestId && (
                <Command icon={Plus} onClick={() => setForm('correct')}>
                  Correct returned request
                </Command>
              )}
              {!admin &&
                record.state === 1 &&
                !record.cancellations.some((c) => c.state === 0 || c.state === 1) && (
                  <Command icon={X} onClick={() => setForm('cancel')}>
                    Request cancellation
                  </Command>
                )}
              {canReview && record.state === 0 && !record.supersededByRequestId && (
                <Command icon={Eye} onClick={() => setForm('review')}>
                  Review leave
                </Command>
              )}
              {canReview &&
                record.state === 1 &&
                !record.cancellations.some((c) => c.state === 1) &&
                record.cancellations
                  .filter((c) => c.state === 0)
                  .map((c) => (
                    <Command key={c.id} icon={Eye} onClick={() => setForm(c.id)}>
                      Review cancellation {c.id}
                    </Command>
                  ))}
            </div>
          )}
        </div>
      )}
    </>
  );
}
function LeaveHistory({
  record,
  admin,
  editing,
}: {
  record: LeaveRecord;
  admin: boolean;
  editing: boolean;
}) {
  const navigate = useNavigate();
  const root = admin ? '/hr/attendance/leaves' : '/account/leaves';
  return (
    <>
      <section className="attendance-section">
        <h2>
          {record.typeName}: {stateNames[record.state]}
        </h2>
        <EvidenceRecord
          value={{
            requestId: record.id,
            employeeId: record.employeeId,
            leaveTypeId: record.leaveTypeId,
            startDate: record.startDate,
            endDate: record.endDate,
            requestedMinutes: record.requestedMinutes,
            reason: record.reason,
            contactDuringLeave: record.contactDuringLeave,
            relieverEmployeeId: record.relieverEmployeeId,
            handoverNotes: record.handoverNotes,
            approvalRequestId: record.approvalRequestId,
            approvalVersion: record.approvalVersion,
            treatment: treatmentNames[record.treatment],
            policyVersionId: record.policyVersionId,
            financialStatus: record.financialStatus,
          }}
        />
        <div className="attendance-commands">
          {record.previousRequestId && (
            <Command
              icon={Eye}
              variant="secondary"
              disabled={editing}
              onClick={() => navigate(`${root}/${record.previousRequestId}`)}
            >
              Original returned revision
            </Command>
          )}
          {record.supersededByRequestId && (
            <Command
              icon={Eye}
              variant="secondary"
              disabled={editing}
              onClick={() => navigate(`${root}/${record.supersededByRequestId}`)}
            >
              Corrected revision
            </Command>
          )}
        </div>
        <h3>Exact server intervals</h3>
        <EvidenceRecord value={record.days} />
        <h3>Decision history</h3>
        {record.decisions.length ? (
          <EvidenceRecord
            value={record.decisions.map((d) => ({ ...d, decision: stateNames[d.decision] }))}
          />
        ) : (
          <p>No decisions recorded.</p>
        )}
        <h3>Separate cancellation history</h3>
        {record.cancellations.length ? (
          <EvidenceRecord
            value={record.cancellations.map((c) => ({
              ...c,
              state: stateNames[c.state],
              decisions: c.decisions.map((d) => ({ ...d, decision: stateNames[d.decision] })),
            }))}
          />
        ) : (
          <p>No cancellations recorded.</p>
        )}
      </section>
      <PrivateLeaveEvidence record={record} admin={admin} />
    </>
  );
}
