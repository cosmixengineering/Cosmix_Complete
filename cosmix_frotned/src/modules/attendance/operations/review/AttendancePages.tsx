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
import { calendar, day, pageOf, recalculateInput, summaryRevision, type Day } from './contracts';
import { EmployeeWorkspace, EvidenceField, NumberedControls, SourceFacts } from './ReviewUi';
import { formatTime, monthRange, today } from './helpers';

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
    <EmployeeWorkspace
      permission="attendance.summaries.read"
      title={`${self ? 'My ' : ''}attendance ${mode}`}
      self={self}
    >
      {(employeeId, scope) => (
        <AttendanceView employeeId={employeeId} scope={scope} mode={mode} self={self} />
      )}
    </EmployeeWorkspace>
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
      <div className="attendance-commands">
        <TextInput
          label="Business month"
          type="month"
          value={month}
          onChange={(e) => {
            setMonth(e.target.value);
            setSelected(null);
          }}
        />
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
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={query.isFetching || !range}
          onClick={() => void query.refetch()}
        >
          Reload month
        </Command>
      </div>
      {!range ? (
        <p role="alert">Select a valid calendar month.</p>
      ) : (
        <QueryState query={query} empty={query.data?.length === 0} />
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
                <p>{row.factualStatus}</p>
                <p>
                  {row.facts.workedMinutes} factual minutes
                  <br />
                  {row.exceptionCount} exceptions
                  <br />
                  {row.isLocked ? 'Locked' : 'Unlocked'}
                </p>
              </button>
            ))}
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="attendance-table">
              <caption>Server daily values for {month}; no monthly totals</caption>
              <thead>
                <tr>
                  <th scope="col">Business date / zone</th>
                  <th scope="col">Factual status</th>
                  <th scope="col">Scheduled / factual minutes</th>
                  <th scope="col">Policy / payable / late / OT minutes</th>
                  <th scope="col">Exceptions / lock</th>
                </tr>
              </thead>
              <tbody>
                {query.data.map((row) => (
                  <tr key={row.businessDate}>
                    <td>
                      <button type="button" onClick={() => setSelected(row.businessDate)}>
                        {row.businessDate}
                      </button>
                      <br />
                      {row.timeZone}
                    </td>
                    <td>{row.factualStatus}</td>
                    <td>
                      {row.roster.configuredScheduledMinutes ?? 'Unavailable'} /{' '}
                      {row.facts.workedMinutes}
                    </td>
                    <td>
                      {row.policy.status}
                      <br />
                      {row.policy.payableMinutes ?? 'Unavailable'} /{' '}
                      {row.policy.lateMinutes ?? 'Unavailable'} /{' '}
                      {row.policy.overtimeMinutes ?? 'Unavailable'}
                    </td>
                    <td>
                      {row.exceptionCount} / {row.isLocked ? 'Locked' : 'Unlocked'}
                    </td>
                  </tr>
                ))}
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
    day,
    allowed && z.iso.date().safeParse(businessDate).success,
  );
  const [page, setPage] = useState(1);
  const history = useAttendanceQuery(
    base + '/daily/history' + queryString({ businessDate, page, pageSize: 25 }),
    pageOf(summaryRevision),
    allowed && Boolean(query.data) && !query.isError,
  );
  const [editing, setEditing] = useState(false);
  const [reason, setReason] = useState('');
  const [notice, setNotice] = useState('');
  const value = !query.isError ? query.data : undefined;
  async function reload() {
    setEditing(false);
    setReason('');
    setNotice('');
    await query.refetch();
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
    <section className="attendance-section">
      <h2>{businessDate}</h2>
      <QueryState query={query} />
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
              disabled={value.isLocked || query.isFetching || history.isFetching}
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
              disabled={!calculate || value.isLocked || query.isFetching}
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
          <h3>Immutable calculation history</h3>
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
      <dl className="attendance-record">
        <dt>Timezone</dt>
        <dd>{value.timeZone}</dd>
        <dt>Factual status</dt>
        <dd>{value.factualStatus}</dd>
        <dt>Worked minutes / open sessions</dt>
        <dd>
          {value.facts.workedMinutes} / {value.facts.openSessions}
        </dd>
        <dt>Roster / scheduled window</dt>
        <dd>
          {value.roster.status}
          <br />
          {formatTime(value.roster.startsAt, value.timeZone)} to{' '}
          {formatTime(value.roster.endsAt, value.timeZone)}
        </dd>
        <dt>Policy state</dt>
        <dd>{value.policy.status}</dd>
        <dt>Payable / late / OT minutes</dt>
        <dd>
          {value.policy.payableMinutes ?? 'Unavailable'} /{' '}
          {value.policy.lateMinutes ?? 'Unavailable'} /{' '}
          {value.policy.overtimeMinutes ?? 'Unavailable'}
        </dd>
        <dt>Exceptions / lock</dt>
        <dd>
          {value.exceptionCount} / {value.isLocked ? 'Locked' : 'Unlocked'}
        </dd>
      </dl>
      {value.policy.explanations.map((explanation, i) => (
        <p key={i}>{explanation}</p>
      ))}
      <p>Elapsed factual time and saved attendance revisions do not confirm payroll settlement.</p>
    </>
  );
}
