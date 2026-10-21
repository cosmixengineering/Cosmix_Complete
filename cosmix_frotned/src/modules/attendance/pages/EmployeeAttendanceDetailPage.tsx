import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, ExternalLink, RefreshCw } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Command, Denied, QueryState } from '../components/SchedulingUi';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { queryString } from '../api/attendance-api';
import { canEmployee, type Scope } from '../operations/review/access';
import {
  calendar,
  cursorPage,
  punchAudit,
  sessionAudit,
  type Day,
} from '../operations/review/contracts';
import { CursorControls, EmployeeWorkspace, SourceFacts } from '../operations/review/ReviewUi';
import { formatTime, monthRange, nextDate, today } from '../operations/review/helpers';
import { id } from '../schemas/common';
import '@/styles/employee-attendance-detail.css';

export function EmployeeAttendanceDetailPage() {
  const { employeeId } = useParams();
  if (!id.safeParse(employeeId).success) return <p role="alert">Invalid employee reference.</p>;
  return (
    <div className="employee-attendance-page">
      <Link to="/hr/attendance" className="ead-back">
        <ArrowLeft size={16} aria-hidden="true" /> Attendance processing
      </Link>
      <EmployeeWorkspace permission="attendance.summaries.read" title="Employee attendance">
        {(selectedEmployeeId, scope) => (
          <EmployeeAttendance employeeId={selectedEmployeeId} scope={scope} />
        )}
      </EmployeeWorkspace>
    </div>
  );
}

function EmployeeAttendance({ employeeId, scope }: { employeeId: string; scope?: Scope }) {
  const access = useAttendanceAccess();
  const [month, setMonth] = useState(() => today().slice(0, 7));
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const allowed =
    access.ready && canEmployee(access.user, 'attendance.summaries.read', employeeId, scope);
  const range = monthRange(month);
  const schema = calendar.refine(
    (rows) =>
      Boolean(range) &&
      new Set(rows.map((row) => row.businessDate)).size === rows.length &&
      rows.every(
        (row) =>
          row.employeeId === employeeId &&
          row.roster.employeeId === employeeId &&
          row.roster.businessDate === row.businessDate &&
          row.businessDate >= range!.from &&
          row.businessDate <= range!.to,
      ),
    'Attendance records do not match the requested employee and month.',
  );
  const query = useAttendanceQuery(
    `/attendance/employees/${employeeId}/calendar` + queryString(range ?? {}),
    schema,
    allowed && Boolean(range),
  );
  if (!allowed) return <Denied>Matching attendance summary scope is required.</Denied>;

  const rows = range && !query.isError && !query.isFetching ? query.data : undefined;
  const selected = rows?.find((row) => row.businessDate === selectedDate) ?? rows?.[0];
  const recordPath = `/hr/attendance/employees/${employeeId}`;

  return (
    <>
      <header className="ead-header">
        <div className="ead-heading">
          <h2>Attendance records</h2>
          <p style={{ overflowWrap: 'anywhere' }}>Employee ID: {employeeId}</p>
          <div className="ead-actions">
            {access.can('hr.employees.read') && (
              <Link className="ead-button" to={`/hr/employees/${employeeId}`}>
                <ExternalLink size={14} aria-hidden="true" /> Employee record
              </Link>
            )}
            <Link className="ead-button" to={recordPath + '/daily'}>
              <ExternalLink size={14} aria-hidden="true" /> Daily details and calculation history
            </Link>
          </div>
        </div>
        <div className="attendance-commands">
          <TextInput
            label="Business month"
            type="month"
            value={month}
            onChange={(event) => {
              setMonth(event.target.value);
              setSelectedDate(null);
            }}
          />
          <Command
            icon={RefreshCw}
            variant="secondary"
            disabled={!range || query.isFetching}
            onClick={() => {
              if (allowed && range) void query.refetch();
            }}
          >
            Reload month
          </Command>
        </div>
      </header>

      {!range ? (
        <p role="alert">Select a valid business month.</p>
      ) : (
        <QueryState
          query={query}
          empty={rows?.length === 0}
          emptyText="No attendance records returned for this employee and month."
        />
      )}

      {rows && rows.length > 0 && (
        <>
          <dl className="ead-metrics" aria-label="Returned attendance facts">
            {[
              ['Days returned', rows.length],
              [
                'Factual worked minutes',
                rows.reduce((total, row) => total + row.facts.workedMinutes, 0),
              ],
              ['Open sessions', rows.reduce((total, row) => total + row.facts.openSessions, 0)],
              ['Recorded exceptions', rows.reduce((total, row) => total + row.exceptionCount, 0)],
            ].map(([label, value]) => (
              <div className="ead-metric" key={label}>
                <dt>{label}</dt>
                <dd>{value}</dd>
              </div>
            ))}
          </dl>
          <section className="ead-attendance-log" aria-labelledby="ead-log-title">
            <div className="ead-section-heading">
              <h2 id="ead-log-title">Attendance log</h2>
              <span className="ead-period">{month}</span>
            </div>
            <div
              className="ead-table-scroll"
              tabIndex={0}
              role="region"
              aria-label="Attendance records"
            >
              <table className="ead-table">
                <thead>
                  <tr>
                    <th scope="col">Business date</th>
                    <th scope="col">Factual status</th>
                    <th scope="col">Roster</th>
                    <th scope="col">Worked minutes</th>
                    <th scope="col">Policy status</th>
                    <th scope="col">Payable minutes</th>
                    <th scope="col">Late minutes</th>
                    <th scope="col">Overtime minutes</th>
                    <th scope="col">Exceptions</th>
                    <th scope="col">Period</th>
                    <th scope="col">Details</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr
                      key={row.businessDate}
                      className={
                        selected?.businessDate === row.businessDate ? 'ead-row-selected' : undefined
                      }
                    >
                      <th scope="row">{row.businessDate}</th>
                      <td>{row.factualStatus}</td>
                      <td>{row.roster.status}</td>
                      <td>{row.facts.workedMinutes}</td>
                      <td>{row.policy.status}</td>
                      <td>{row.policy.payableMinutes ?? 'Unavailable'}</td>
                      <td>{row.policy.lateMinutes ?? 'Unavailable'}</td>
                      <td>{row.policy.overtimeMinutes ?? 'Unavailable'}</td>
                      <td>{row.exceptionCount}</td>
                      <td>{row.isLocked ? 'Locked' : 'Unlocked'}</td>
                      <td>
                        <button
                          className="ead-icon-button"
                          type="button"
                          title={`Inspect ${row.businessDate}`}
                          aria-label={`Inspect ${row.businessDate}`}
                          aria-pressed={selected?.businessDate === row.businessDate}
                          onClick={() => setSelectedDate(row.businessDate)}
                        >
                          <ExternalLink size={14} aria-hidden="true" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}

      {selected && (
        <section className="ead-audit" aria-labelledby="ead-selected-title">
          <div className="ead-section-heading">
            <h2 id="ead-selected-title">{selected.businessDate}</h2>
          </div>
          <dl className="ead-fields">
            <div>
              <dt>Timezone</dt>
              <dd>{selected.timeZone}</dd>
            </div>
            <div>
              <dt>Scheduled start</dt>
              <dd>{formatTime(selected.roster.startsAt, selected.timeZone)}</dd>
            </div>
            <div>
              <dt>Scheduled end</dt>
              <dd>{formatTime(selected.roster.endsAt, selected.timeZone)}</dd>
            </div>
            <div>
              <dt>Payroll ready</dt>
              <dd>
                {selected.policy.payrollReady === undefined
                  ? 'Unavailable'
                  : selected.policy.payrollReady
                    ? 'Yes'
                    : 'No'}
              </dd>
            </div>
          </dl>
          <SourceFacts
            title="Roster, policy and saved revision"
            value={{
              roster: selected.roster,
              policy: selected.policy,
              facts: selected.facts,
              latestSavedRevision: selected.latestSavedRevision,
            }}
          />
          <EmployeeEvidence
            key={`${employeeId}:${selected.businessDate}`}
            employeeId={employeeId}
            scope={scope}
            day={selected}
          />
        </section>
      )}
    </>
  );
}

function EmployeeEvidence({
  employeeId,
  scope,
  day,
}: {
  employeeId: string;
  scope?: Scope;
  day: Day;
}) {
  const access = useAttendanceAccess();
  const [kind, setKind] = useState('punches');
  const allowed =
    access.ready &&
    (canEmployee(access.user, 'attendance.punches.read', employeeId, scope) ||
      (day.roster.siteId !== null &&
        canEmployee(access.user, 'attendance.punches.read', employeeId, {
          kind: 5,
          targetId: day.roster.siteId,
        })));
  if (!allowed)
    return <Denied>Punch and session evidence requires separate matching read scope.</Denied>;
  return (
    <section aria-label="Punch and session evidence">
      <SelectInput
        label="Evidence stream"
        value={kind}
        options={[
          { value: 'punches', label: 'Raw punch receipts and assessments' },
          { value: 'sessions', label: 'Immutable session revisions' },
        ]}
        onChange={(event) => setKind(event.target.value)}
      />
      <EvidenceHistory
        key={kind}
        employeeId={employeeId}
        businessDate={day.businessDate}
        kind={kind}
        allowed={allowed}
      />
    </section>
  );
}

function EvidenceHistory({
  employeeId,
  businessDate,
  kind,
  allowed,
}: {
  employeeId: string;
  businessDate: string;
  kind: string;
  allowed: boolean;
}) {
  const [cursors, setCursors] = useState<(string | undefined)[]>([undefined]);
  const cursor = cursors[cursors.length - 1];
  const schema = cursorPage(kind === 'punches' ? punchAudit : sessionAudit).refine(
    (value) =>
      value.items.every(
        (item) => ('punch' in item ? item.punch : item.session).businessDate === businessDate,
      ),
    'Evidence does not match the requested business date.',
  );
  const query = useAttendanceQuery(
    `/attendance/employees/${employeeId}/${kind}` +
      queryString({ from: businessDate, to: nextDate(businessDate), pageSize: 25, cursor }),
    schema,
    allowed,
  );
  const data = !query.isError && !query.isFetching ? query.data : undefined;
  return (
    <>
      <Command
        icon={RefreshCw}
        variant="secondary"
        disabled={!allowed || query.isFetching}
        onClick={() => {
          if (allowed) void query.refetch();
        }}
      >
        Reload evidence
      </Command>
      <QueryState
        query={query}
        empty={data?.items.length === 0}
        emptyText="No evidence records returned for this date."
      />
      {data && (
        <>
          {data.items.map((entry) => {
            const record = 'punch' in entry ? entry.punch : entry.session;
            return (
              <SourceFacts
                key={record.id}
                title={
                  'action' in record
                    ? `${record.action}: ${record.outcome}`
                    : `Session revision ${record.revision}`
                }
                value={entry}
              />
            );
          })}
          <CursorControls
            cursor={cursor}
            next={data.nextCursor}
            back={cursors.length > 1}
            disabled={query.isFetching}
            onBack={() => setCursors((values) => values.slice(0, -1))}
            onNext={(value) => setCursors((values) => [...values, value])}
          />
        </>
      )}
    </>
  );
}
