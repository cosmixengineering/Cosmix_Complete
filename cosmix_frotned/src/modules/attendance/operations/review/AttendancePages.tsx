import { useState } from 'react';
import { CalendarDays, List, RefreshCw } from 'lucide-react';
import { z } from 'zod';
import { TextInput } from '@/components/forms/TextInput';
import { IconButton } from '@/components/ui/IconButton';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import {
  Command,
  Denied,
  MutationForm,
  QueryState,
  SavedNotice,
} from '../../components/SchedulingUi';
import { queryString } from '../../api/attendance-api';
import { canEmployee, canSelf, type Scope } from './access';
import {
  calendar,
  readDay,
  pageOf,
  recalculateInput,
  summaryRevision,
  type Day,
} from './contracts';
import { EmployeeWorkspace, EvidenceField, NumberedControls, SourceFacts } from './ReviewUi';
import { formatTime, monthRange, today } from './helpers';
import {
  AttendanceEmployeeQueryState,
  AttendanceSetupNotice,
} from '../../components/AttendanceEmployeeSetup';
import { needsAttendanceSetup } from '../../schemas/employee-selection';
import '@/styles/attendance-register.css';

const missingSummaryMessage =
  'Locked period: the retained attendance summary is unavailable for this date. HR must review the historical records. Time, absence and pay have not been inferred.';

function duration(minutes: number | null | undefined) {
  if (minutes == null) return 'Unavailable';
  return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}
function readableStatus(status: string) {
  return status.replace(/[_-]/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2');
}
function dateLabel(date: string) {
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    weekday: 'short',
    timeZone: 'UTC',
  }).format(new Date(`${date}T12:00:00Z`));
}

export function AttendanceDailyPage() {
  return <AttendancePage mode="daily" />;
}
export function MyAttendanceDailyPage() {
  return <AttendancePage mode="daily" self />;
}
export function AttendanceRegisterPage() {
  return <AttendancePage mode="register" />;
}
export function MyAttendanceRegisterPage() {
  return <AttendancePage mode="register" self />;
}
export function AttendanceCalendarPage() {
  return <AttendancePage mode="calendar" />;
}
export function MyAttendanceCalendarPage() {
  return <AttendancePage mode="calendar" self />;
}
function AttendancePage({
  mode,
  self = false,
}: {
  mode: 'daily' | 'register' | 'calendar';
  self?: boolean;
}) {
  return (
    <div className="att-register-page">
      <EmployeeWorkspace
        permission="attendance.summaries.read"
        title={`${self ? 'My ' : ''}attendance ${mode}`}
        self={self}
        compact={!self}
        approvedOnly={!self}
      >
        {(employeeId, scope) => (
          <AttendanceView employeeId={employeeId} scope={scope} mode={mode} self={self} />
        )}
      </EmployeeWorkspace>
    </div>
  );
}
function AttendanceView({
  employeeId,
  scope,
  mode,
  self,
}: {
  employeeId: string;
  scope?: Scope;
  mode: 'daily' | 'register' | 'calendar';
  self: boolean;
}) {
  const access = useAttendanceAccess();
  const [businessDate, setDate] = useState(today());
  const [month, setMonth] = useState(today().slice(0, 7));
  const [selected, setSelected] = useState<string | null>(null);
  const [layout, setLayout] = useState(mode === 'calendar' ? 'calendar' : 'register');
  const allowed = self
    ? canSelf(access.user, 'attendance.summaries.self')
    : canEmployee(access.user, 'attendance.summaries.read', employeeId, scope);
  const base = self ? '/me/attendance' : `/attendance/employees/${employeeId}`;
  const range = monthRange(month);
  const query = useAttendanceQuery(
    base + '/calendar' + queryString(range ?? {}),
    calendar,
    allowed && mode !== 'daily' && Boolean(range),
  );
  if (!allowed) return <Denied>Attendance summary access is unavailable.</Denied>;
  if (mode === 'daily')
    return (
      <>
        <TextInput
          label="Shift-start business date"
          type="date"
          value={businessDate}
          onChange={(e) => setDate(e.target.value)}
        />
        <DailyDetail
          key={businessDate}
          base={base}
          businessDate={businessDate}
          self={self}
          employeeId={employeeId}
          scope={scope}
          allowed={allowed}
        />
      </>
    );
  return (
    <>
      <div className="att-register-toolbar">
        <div className="att-register-toolbar-title">
          <h2>Attendance register</h2>
          <p>Select a date to view its attendance details.</p>
        </div>
        <TextInput
          label="Month"
          type="month"
          value={month}
          onChange={(e) => {
            setMonth(e.target.value);
            setSelected(null);
          }}
        />
        <div className="att-register-view-toggle" role="group" aria-label="Register layout">
          <IconButton
            label="Register view"
            aria-pressed={layout === 'register'}
            onClick={() => setLayout('register')}
          >
            <List size={16} />
          </IconButton>
          <IconButton
            label="Calendar view"
            aria-pressed={layout === 'calendar'}
            onClick={() => setLayout('calendar')}
          >
            <CalendarDays size={16} />
          </IconButton>
        </div>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={query.isFetching || !range}
          onClick={() => void query.refetch()}
        >
          Refresh
        </Command>
      </div>
      {!range ? (
        <p role="alert">Select a valid calendar month.</p>
      ) : (
        <AttendanceEmployeeQueryState
          employeeId={employeeId}
          query={query}
          empty={query.data?.length === 0}
        />
      )}
      {!query.isError && query.data?.some(needsAttendanceSetup) && (
        <AttendanceSetupNotice employeeId={employeeId} />
      )}
      {!query.isError && query.data?.some((row) => row.unavailableReason) && (
        <p className="att-register-notice" role="status">
          Some locked dates have no saved attendance record. These dates are marked unavailable;
          they are not counted as absence or zero hours.
        </p>
      )}
      {!query.isError &&
        query.data &&
        range &&
        (layout === 'calendar' ? (
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 160px), 1fr))',
              gap: 8,
            }}
          >
            {query.data.map((row) => (
              <button
                type="button"
                key={row.businessDate}
                onClick={() => setSelected(row.businessDate)}
                aria-pressed={selected === row.businessDate}
                style={{
                  border: '1px solid #dce2e8',
                  borderRadius: 4,
                  background: selected === row.businessDate ? '#eaf4f0' : '#fff',
                  padding: 12,
                  textAlign: 'left',
                  minHeight: 128,
                  overflowWrap: 'anywhere',
                }}
              >
                <strong>{row.businessDate}</strong>
                {row.unavailableReason ? (
                  <p>Locked · Record unavailable</p>
                ) : (
                  <>
                    <p>
                      {needsAttendanceSetup(row)
                        ? 'Attendance setup required'
                        : readableStatus(row.factualStatus)}
                    </p>
                    <p>
                      {duration(row.facts.workedMinutes)} worked
                      <br />
                      {row.exceptionCount} exceptions
                      <br />
                      {row.isLocked ? 'Locked' : 'Unlocked'}
                    </p>
                  </>
                )}
              </button>
            ))}
          </div>
        ) : (
          <div className="att-register-table-wrap">
            <table className="attendance-table att-register-table">
              <caption>Daily attendance · {month}</caption>
              <thead>
                <tr>
                  <th scope="col">Date</th>
                  <th scope="col">Status</th>
                  <th scope="col">Scheduled</th>
                  <th scope="col">Worked</th>
                  <th scope="col">Late</th>
                  <th scope="col">Overtime</th>
                  <th scope="col">Record</th>
                </tr>
              </thead>
              <tbody>
                {query.data.map((row) =>
                  row.unavailableReason ? (
                    <tr
                      key={row.businessDate}
                      className={selected === row.businessDate ? 'is-selected' : undefined}
                    >
                      <td>
                        <button type="button" onClick={() => setSelected(row.businessDate)}>
                          {dateLabel(row.businessDate)}
                        </button>
                      </td>
                      <td>
                        <span className="att-register-status">Unavailable</span>
                      </td>
                      <td colSpan={4} className="att-register-muted">
                        Saved attendance record is missing
                      </td>
                      <td>Locked</td>
                    </tr>
                  ) : (
                    <tr
                      key={row.businessDate}
                      className={selected === row.businessDate ? 'is-selected' : undefined}
                    >
                      <td>
                        <button type="button" onClick={() => setSelected(row.businessDate)}>
                          {dateLabel(row.businessDate)}
                        </button>
                      </td>
                      <td>
                        <span className="att-register-status">
                          {needsAttendanceSetup(row)
                            ? 'Attendance setup required'
                            : readableStatus(row.factualStatus)}
                        </span>
                      </td>
                      <td>{duration(row.roster.configuredScheduledMinutes)}</td>
                      <td>{duration(row.facts.workedMinutes)}</td>
                      <td>{duration(row.policy.lateMinutes)}</td>
                      <td>{duration(row.policy.overtimeMinutes)}</td>
                      <td>
                        {row.isLocked ? 'Locked' : 'Open'}
                        {row.exceptionCount > 0 && (
                          <small>
                            {row.exceptionCount} exception{row.exceptionCount === 1 ? '' : 's'}
                          </small>
                        )}
                      </td>
                    </tr>
                  ),
                )}
              </tbody>
            </table>
          </div>
        ))}
      {selected && (
        <DailyDetail
          key={`${month}:${selected}`}
          base={base}
          businessDate={selected}
          self={self}
          employeeId={employeeId}
          scope={scope}
          allowed={allowed}
        />
      )}
    </>
  );
}
function DailyDetail({
  base,
  businessDate,
  self,
  employeeId,
  scope,
  allowed,
}: {
  base: string;
  businessDate: string;
  self: boolean;
  employeeId: string;
  scope?: Scope;
  allowed: boolean;
}) {
  const access = useAttendanceAccess();
  const query = useAttendanceQuery(
    base + '/daily' + queryString({ businessDate }),
    readDay,
    allowed && z.iso.date().safeParse(businessDate).success,
  );
  const [page, setPage] = useState(1);
  const history = useAttendanceQuery(
    base + '/daily/history' + queryString({ businessDate, page, pageSize: 25 }),
    pageOf(summaryRevision),
    allowed && Boolean(query.data) && !query.data?.unavailableReason && !query.isError,
  );
  const [editing, setEditing] = useState(false);
  const [reason, setReason] = useState('');
  const [notice, setNotice] = useState('');
  const value = !query.isError && !query.data?.unavailableReason ? query.data : undefined;
  async function reload() {
    setEditing(false);
    setReason('');
    setNotice('');
    const updated = await query.refetch();
    if (updated.data && !updated.data.unavailableReason && !updated.isError)
      await history.refetch();
  }
  const calculate =
    !self &&
    (canEmployee(access.user, 'attendance.summaries.calculate', employeeId, scope) ||
      canEmployee(
        access.user,
        'attendance.summaries.calculate',
        employeeId,
        value?.roster.siteId ? { kind: 5, targetId: value.roster.siteId } : undefined,
      ));
  if (!z.iso.date().safeParse(businessDate).success)
    return <p role="alert">Select a valid business date.</p>;
  return (
    <section className="attendance-section att-register-day">
      <h2>Daily details · {dateLabel(businessDate)}</h2>
      <AttendanceEmployeeQueryState employeeId={employeeId} query={query} />
      {needsAttendanceSetup(value) && <AttendanceSetupNotice employeeId={employeeId} />}
      {!query.isError && query.data?.unavailableReason && (
        <p role="status">{missingSummaryMessage}</p>
      )}
      <SavedNotice message={notice} />
      {!editing && (
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={query.isFetching || history.isFetching}
          onClick={() => void reload()}
        >
          Reload day and history
        </Command>
      )}
      {value && (
        <>
          <DayFacts value={value} />
          <SourceFacts
            title="Roster, provenance and saved revision"
            value={{
              roster: value.roster,
              latestSavedRevision: value.latestSavedRevision,
              facts: value.facts,
              policy: value.policy,
            }}
          />
          {calculate && !editing && (
            <Command
              icon={RefreshCw}
              disabled={
                value.isLocked ||
                needsAttendanceSetup(value) ||
                query.isFetching ||
                history.isFetching
              }
              onClick={() => {
                setNotice('');
                setEditing(true);
              }}
            >
              Review recalculation
            </Command>
          )}
          {editing && (
            <MutationForm
              title="Recalculate unlocked attendance"
              disabled={
                !calculate || value.isLocked || needsAttendanceSetup(value) || query.isFetching
              }
              build={() => ({
                path: base + '/daily/recalculate',
                method: 'POST',
                schema: summaryRevision,
                body: recalculateInput.parse({
                  businessDate,
                  expectedRevision: value.latestSavedRevision?.revision ?? 0,
                  reason,
                }),
              })}
              onCancel={() => void reload()}
              onSaved={(result) => {
                const saved = summaryRevision.parse(result);
                setEditing(false);
                setReason('');
                setNotice(`Saved attendance revision ${saved.revision}.`);
                void query.refetch();
                void history.refetch();
              }}
            >
              <p>Expected revision: {value.latestSavedRevision?.revision ?? 0}</p>
              <EvidenceField label="Reason" max={1000} value={reason} onChange={setReason} />
            </MutationForm>
          )}
          <h3>Saved calculation history</h3>
          <QueryState query={history} empty={history.data?.items.length === 0} />
          {!history.isError && history.data && (
            <>
              {history.data.items.map((row) => (
                <SourceFacts
                  key={row.id}
                  title={`Revision ${row.revision}, ${formatTime(row.calculatedAtUtc, value.timeZone)}`}
                  value={row}
                />
              ))}
              <NumberedControls
                page={page}
                data={history.data}
                onChange={setPage}
                disabled={history.isFetching || editing}
              />
            </>
          )}
        </>
      )}
    </section>
  );
}
function DayFacts({ value }: { value: Day }) {
  return (
    <>
      <dl className="att-register-day-facts">
        <div>
          <dt>Attendance status</dt>
          <dd>
            {needsAttendanceSetup(value)
              ? 'Attendance setup required'
              : readableStatus(value.factualStatus)}
          </dd>
        </div>
        <div>
          <dt>Hours worked</dt>
          <dd>{duration(value.facts.workedMinutes)}</dd>
        </div>
        <div>
          <dt>Late time</dt>
          <dd>{duration(value.policy.lateMinutes)}</dd>
        </div>
        <div>
          <dt>Overtime</dt>
          <dd>{duration(value.policy.overtimeMinutes)}</dd>
        </div>
        <div>
          <dt>Scheduled shift</dt>
          <dd>
            {formatTime(value.roster.startsAt, value.timeZone)} –{' '}
            {formatTime(value.roster.endsAt, value.timeZone)}
          </dd>
        </div>
        <div>
          <dt>Time zone</dt>
          <dd>{value.timeZone}</dd>
        </div>
        <div>
          <dt>Open sessions</dt>
          <dd>{value.facts.openSessions}</dd>
        </div>
        <div>
          <dt>Exceptions</dt>
          <dd>{value.exceptionCount}</dd>
        </div>
        <div>
          <dt>Payable time</dt>
          <dd>{duration(value.policy.payableMinutes)}</dd>
        </div>
        <div>
          <dt>Calculation status</dt>
          <dd>{readableStatus(value.policy.status)}</dd>
        </div>
        <div>
          <dt>Record status</dt>
          <dd>{value.isLocked ? 'Locked' : 'Open'}</dd>
        </div>
      </dl>
      {value.policy.explanations.map((explanation, i) => (
        <p key={i}>{explanation}</p>
      ))}
      <p>Elapsed factual time and saved attendance revisions do not confirm payroll settlement.</p>
    </>
  );
}
