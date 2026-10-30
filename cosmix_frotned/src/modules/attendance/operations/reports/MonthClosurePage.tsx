import { useState } from 'react';
import { z } from 'zod';
import { Filter, LockKeyhole, RefreshCw } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
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
import { reason } from '../../schemas/common';
import { businessToday } from '../../utils/messages';
import { monthDetailFor, monthLockFor, monthPreview, monthPreviewFor } from './schemas';

const setupIssues: Readonly<Record<string, string>> = {
  staff_taxonomy_category_not_configured:
    'Staff category mapping is missing. HR must publish an approved mapping covering this date.',
  staff_taxonomy_taxonomy_not_effective:
    'No staff category policy is effective on this date. Review the approved policy dates.',
  employee_assignment_unavailable:
    'Employment assignment is missing for this date. Review the employee assignment and roster.',
  attendance_month_not_finished: 'This month has not ended yet; it cannot be finalized.',
};

function FinalizeForm({
  preview,
  path,
  onSaved,
  onCancel,
}: {
  preview: z.infer<typeof monthPreview>;
  path: string;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [source] = useState(preview);
  const [inputReason, setReason] = useState('');
  return (
    <MutationForm
      title="Finalize attendance month"
      submitLabel="Finalize month"
      disabled={!source.ready}
      onSaved={onSaved}
      onCancel={onCancel}
      build={() => {
        if (!source.ready) throw new Error('The server has not marked this month ready.');
        return {
          path: path + '/finalize',
          method: 'POST',
          schema: monthLockFor(source.monthStart.slice(0, 7)),
          body: {
            expectedPreviewFingerprint: source.previewFingerprint,
            expectedVersion: source.version,
            reason: reason().parse(inputReason),
          },
        };
      }}
    >
      <dl className="attendance-record">
        <dt>Month</dt>
        <dd>{source.monthStart}</dd>
        <dt>Required days</dt>
        <dd>{source.requiredDayCount}</dd>
        <dt>Ready summaries</dt>
        <dd>{source.readySummaryCount}</dd>
        <dt>Employees</dt>
        <dd>{source.employeeCount}</dd>
      </dl>
      <TextInput
        label="Reason"
        value={inputReason}
        maxLength={1000}
        required
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}
function MonthRecord({
  period,
  onEditing,
}: {
  period: string;
  onEditing: (value: boolean) => void;
}) {
  const access = useAttendanceAccess();
  const path = `/attendance/month-locks/${Number(period.slice(0, 4))}/${Number(period.slice(5))}`;
  const query = useAttendanceQuery(
    path + '/preview',
    monthPreviewFor(period),
    access.can('attendance.month-locks.read'),
  );
  const detail = useAttendanceQuery(
    path,
    monthDetailFor(period),
    access.can('attendance.month-locks.read') && query.data?.state === 'locked' && !query.isError,
  );
  const [editing, setEditing] = useState(false);
  const [notice, setNotice] = useState('');
  function close() {
    setEditing(false);
    onEditing(false);
    void query.refetch();
  }
  return (
    <>
      <div className="attendance-heading">
        <h2>{period}</h2>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={editing || query.isFetching}
          onClick={() => void query.refetch()}
        >
          Reload preview
        </Command>
      </div>
      <SavedNotice message={notice} />
      <QueryState query={query} />
      {query.data && !query.isError && (
        <>
          <dl className="attendance-record">
            <dt>State</dt>
            <dd>{query.data.state}</dd>
            <dt>Ready</dt>
            <dd>{query.data.ready ? 'Yes' : 'No'}</dd>
            <dt>Employees</dt>
            <dd>{query.data.employeeCount}</dd>
            <dt>Required day cells</dt>
            <dd>{query.data.requiredDayCount}</dd>
            <dt>Ready summaries</dt>
            <dd>{query.data.readySummaryCount}</dd>
            <dt>Carry-forward records</dt>
            <dd>{query.data.carryForwardCount}</dd>
            <dt>Issues</dt>
            <dd>{query.data.issueCount}</dd>
          </dl>
          {query.data.issues.length > 0 && (
            <div className="hr-table-scroll">
              <table className="attendance-table">
                <thead>
                  <tr>
                    <th>Readiness issue</th>
                    <th>Employee</th>
                    <th>Date</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.issues.map((issue, index) => (
                    <tr key={`${issue.code}:${issue.employeeId}:${issue.businessDate}:${index}`}>
                      <td>{setupIssues[issue.code] ?? issue.code}</td>
                      <td>{issue.employeeId ?? 'Month-wide'}</td>
                      <td>{issue.businessDate ?? 'Period'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {access.can('attendance.month-locks.finalize') && !editing && (
            <Command
              icon={LockKeyhole}
              disabled={!query.data.ready || query.isFetching}
              onClick={() => {
                setEditing(true);
                onEditing(true);
              }}
            >
              Finalize month
            </Command>
          )}
          {editing && (
            <FinalizeForm
              preview={query.data}
              path={path}
              onCancel={close}
              onSaved={() => {
                setNotice('Month finalized. Retained membership is reloading.');
                close();
              }}
            />
          )}
        </>
      )}
      {query.data?.state === 'locked' && !query.isError && (
        <section className="attendance-section">
          <h2>Retained lock membership</h2>
          <QueryState query={detail} />
          {detail.data && !detail.isError && (
            <>
              <dl className="attendance-record">
                <dt>Lock event</dt>
                <dd>{detail.data.lock.eventId}</dd>
                <dt>Locked</dt>
                <dd>{detail.data.lock.lockedAtUtc}</dd>
                <dt>Actor</dt>
                <dd>{detail.data.lock.actor}</dd>
                <dt>Reason</dt>
                <dd>{detail.data.lock.reason}</dd>
                <dt>Provenance</dt>
                <dd>{detail.data.lock.provenance}</dd>
              </dl>
              <div className="hr-table-scroll">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Date</th>
                      <th>Summary</th>
                      <th>Policy</th>
                      <th>Calculation</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.data.summaries.map((row) => (
                      <tr key={row.summaryId}>
                        <td>{row.employeeId}</td>
                        <td>{row.businessDate}</td>
                        <td>{row.summaryId}</td>
                        <td>{row.policyVersionId}</td>
                        <td>{row.calculationVersion}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <h3>Retained carry-forward revisions</h3>
              <div className="hr-table-scroll">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Source date</th>
                      <th>Revision</th>
                      <th>Target month</th>
                      <th>Worked-minute delta</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.data.carryForwards.map((row) => (
                      <tr key={row.adjustmentId}>
                        <td>{row.employeeId}</td>
                        <td>{row.sourceBusinessDate}</td>
                        <td>{row.revision}</td>
                        <td>{row.targetMonthStart}</td>
                        <td>{row.deltaWorkedMinutes}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <h3>Retained financial input membership</h3>
              <div className="hr-table-scroll">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Source revision</th>
                      <th>Handoff</th>
                      <th>Company activation evidence</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(detail.data.payrollInputs ?? []).map((row) => (
                      <tr key={row.sourceRevisionId}>
                        <td>{row.employeeId}</td>
                        <td>
                          {row.sourceRevisionId} / {row.sourceRevision}
                        </td>
                        <td>{row.handoffId ?? 'Not retained'}</td>
                        <td>{row.legalActivationFingerprint ?? 'Not retained'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </section>
      )}
    </>
  );
}
function MonthContent() {
  const access = useAttendanceAccess();
  const [input, setInput] = useState(businessToday().slice(0, 7));
  const [period, setPeriod] = useState('');
  const [editing, setEditing] = useState(false);
  const [error, setError] = useState('');
  if (!access.can('attendance.month-locks.read')) return <Denied />;
  return (
    <>
      <h1>Attendance Month Closure</h1>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (
            /^(19\d\d|[2-9]\d{3})-(0[1-9]|1[0-2])$/.test(input) &&
            Number(input.slice(0, 4)) <= 9998
          ) {
            setPeriod(input);
            setError('');
          } else setError('Choose a supported business month.');
        }}
      >
        <fieldset disabled={editing}>
          <TextInput
            label="Business month"
            type="month"
            value={input}
            required
            onChange={(event) => setInput(event.target.value)}
          />
          <Command type="submit" icon={Filter}>
            Load month
          </Command>
        </fieldset>
      </form>
      {error && (
        <p className="attendance-error" role="alert">
          {error}
        </p>
      )}
      {period && <MonthRecord key={period} period={period} onEditing={setEditing} />}
    </>
  );
}
export function MonthClosurePage() {
  return (
    <PrivateWorkspace resource="month-closure">
      <MonthContent />
    </PrivateWorkspace>
  );
}
