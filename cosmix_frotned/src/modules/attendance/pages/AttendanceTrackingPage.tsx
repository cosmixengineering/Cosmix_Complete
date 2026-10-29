import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { CalendarDays, ChevronLeft, ChevronRight, RefreshCw, Search, Users } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { ApiError } from '@/services/api/api-error';
import { Command, Denied, PrivateWorkspace, QueryState } from '../components/SchedulingUi';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { queryString } from '../api/attendance-api';
import {
  availableDay,
  loadTrackingRows,
  loadTrackingSessions,
  trackingDuration,
  trackingEmployees,
  trackingStatus,
  type TrackingEmployee,
  type TrackingRow,
} from '../api/tracking-api';
import { monthRange, today } from '../operations/review/helpers';
import { AttendanceSetupNotice } from '../components/AttendanceEmployeeSetup';
import { approvedAttendanceFilter, needsAttendanceSetup } from '../schemas/employee-selection';
import '@/styles/attendance-tracking.css';

export function AttendanceTrackingPage() {
  return (
    <PrivateWorkspace resource="hr-attendance-tracking">
      <TrackingWorkspace />
    </PrivateWorkspace>
  );
}
function TrackingWorkspace() {
  const access = useAttendanceAccess();
  const currentDate = today();
  const [month, setMonth] = useState(currentDate.slice(0, 7));
  const [selectedDate, setSelectedDate] = useState(currentDate);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('');
  const [page, setPage] = useState(1);
  const [view, setView] = useState<'all' | 'log'>('all');
  const [details, setDetails] = useState<string | null>(null);
  const [autoRefresh, setAutoRefresh] = useState(false);
  const range = monthRange(month);
  const allowed = access.can('hr.employees.read') && access.can('attendance.summaries.read');
  const directory = useAttendanceQuery(
    '/hr/employees' +
      queryString({ page, pageSize: 10, ...approvedAttendanceFilter, search: filter }),
    trackingEmployees,
    allowed,
  );
  const employees = directory.isError || directory.isFetching ? [] : (directory.data?.items ?? []);
  const from = range?.from ?? '';
  const to = range && range.to < currentDate ? range.to : currentDate;
  const rowsQuery = useQuery({
    queryKey: [
      'attendance',
      access.subject,
      'tracking',
      employees.map((employee) => employee.id),
      from,
      to,
    ],
    queryFn: ({ signal }) => {
      if (!access.ready) throw new ApiError(401);
      if (!allowed) throw new ApiError(403);
      return loadTrackingRows(employees, from, to, signal);
    },
    enabled: allowed && employees.length > 0 && Boolean(range),
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchInterval: autoRefresh ? 60000 : false,
    refetchIntervalInBackground: false,
  });
  const rows = rowsQuery.isError || directory.isFetching ? [] : (rowsQuery.data ?? []);
  const days = range
    ? Array.from(
        { length: Number(range.to.slice(-2)) },
        (_, index) => `${month}-${String(index + 1).padStart(2, '0')}`,
      )
    : [];
  const daily = rows.map((row) => ({
    ...row,
    day: availableDay(row.days.find((day) => day.businessDate === selectedDate)),
  }));
  const classified = daily.filter(
    ({ day }) =>
      !needsAttendanceSetup(day) &&
      day?.policy.status === 'calculated' &&
      day.policy.dayStatus !== null,
  );
  const present = classified.filter(({ day }) => day?.policy.dayStatus === 2).length;
  const late = classified.filter(({ day }) => (day?.policy.lateMinutes ?? 0) > 0).length;
  const unavailable = daily.filter(
    ({ day, error }) => !day || error || needsAttendanceSetup(day),
  ).length;
  const busy = directory.isFetching || rowsQuery.isFetching;
  const hasRows = rows.length > 0;
  const hasClassification = selectedDate <= currentDate && classified.length > 0;
  const lastUpdated = rowsQuery.dataUpdatedAt
    ? new Date(rowsQuery.dataUpdatedAt).toLocaleTimeString('en-GB', {
        hour: '2-digit',
        minute: '2-digit',
        timeZone: 'Asia/Karachi',
      })
    : null;
  const pageCount = Math.max(1, Math.ceil((directory.data?.totalCount ?? 0) / 10));
  if (!allowed)
    return (
      <Denied>
        Team tracking requires employee directory and attendance summary access. Ask your
        administrator for the appropriate management permissions.
      </Denied>
    );
  async function refresh() {
    if (busy) return;
    await rowsQuery.refetch();
  }
  function changeMonth(value: string) {
    setMonth(value);
    setDetails(null);
    const next = monthRange(value);
    if (next)
      setSelectedDate(currentDate >= next.from && currentDate <= next.to ? currentDate : next.from);
  }
  return (
    <div className="attendance-tracking">
      <header className="tracking-heading">
        <div>
          <h1>Attendance Tracking</h1>
          <p>Monthly team register and daily attendance log.</p>
        </div>
        <Link className="tracking-link-button" to="/hr/attendance">
          <CalendarDays size={15} /> Employee details & reviews
        </Link>
      </header>
      <section className="tracking-filters" aria-label="Tracking filters">
        <form
          onSubmit={(event) => {
            event.preventDefault();
            setFilter(search.trim());
            setPage(1);
            setDetails(null);
            if (filter === search.trim() && page === 1) void directory.refetch();
          }}
        >
          <TextInput
            label="Find employees"
            placeholder="Name or employee number"
            maxLength={100}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          <Command icon={Search} type="submit" variant="secondary" disabled={busy}>
            Search
          </Command>
        </form>
        <TextInput
          label="Month"
          type="month"
          value={month}
          onChange={(event) => changeMonth(event.target.value)}
        />
        <div className="tracking-refresh">
          <label>
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={(event) => setAutoRefresh(event.target.checked)}
            />
            Refresh every minute
          </label>
          <Command
            icon={RefreshCw}
            variant="secondary"
            disabled={busy || !range || employees.length === 0}
            onClick={() => void refresh()}
          >
            {busy ? 'Loading…' : 'Refresh'}
          </Command>
        </div>
      </section>
      <div className="tracking-context">
        <span>
          <Users size={14} /> Approved employees · page {page}
          {directory.data ? ` of ${pageCount}` : ''}
        </span>
        <span>
          {lastUpdated ? `Last loaded ${lastUpdated} PKT` : 'Waiting for attendance data'}
        </span>
      </div>
      <div className="tracking-stats" aria-label="Selected date summary">
        <TrackingStat
          label="Employees on this page"
          value={hasRows ? String(rows.length) : '—'}
          detail={selectedDate}
        />
        <TrackingStat
          label="Present"
          value={hasClassification ? String(present) : '—'}
          detail={
            hasClassification
              ? `${classified.length} classified records`
              : 'Classification unavailable'
          }
        />
        <TrackingStat
          label="Late arrivals"
          value={hasClassification ? String(late) : '—'}
          detail="From calculated attendance"
        />
        <TrackingStat
          label="Unavailable records"
          value={hasRows && selectedDate <= currentDate ? String(unavailable) : '—'}
          detail="Not counted as absence"
        />
      </div>
      <div className="tracking-view-tabs" role="group" aria-label="Tracking view">
        <button aria-pressed={view === 'all'} onClick={() => setView('all')}>
          Monthly register & daily log
        </button>
        <button aria-pressed={view === 'log'} onClick={() => setView('log')}>
          Daily log only
        </button>
      </div>
      <QueryState
        query={directory}
        empty={directory.data?.items.length === 0}
        emptyText="No approved employees match this search."
      />
      <p className="tracking-availability-note">
        Approved employees include former and archived staff for historical review. Drafts require
        completed hiring and approval before they can appear here.
      </p>
      {!range && <p role="alert">Select a valid month.</p>}
      {employees.length > 0 && range && <QueryState query={rowsQuery} />}
      {hasRows && unavailable > 0 && selectedDate <= currentDate && (
        <p className="tracking-availability-note" role="status">
          {unavailable} employee record(s) are unavailable for {selectedDate}. Open View day for
          details. Missing history is not counted as absence.
        </p>
      )}
      {view === 'all' && hasRows && (
        <section className="tracking-panel">
          <div className="tracking-panel-heading">
            <div>
              <h2>Employee attendance register</h2>
              <p>
                {month} · Showing {rows.length} of {directory.data?.totalCount} matching employees
              </p>
            </div>
            <span>Click a date to open the daily log</span>
          </div>
          <div
            className="tracking-matrix-scroll"
            tabIndex={0}
            role="region"
            aria-label="Monthly attendance table, scroll horizontally for all dates"
          >
            <table className="tracking-matrix">
              <thead>
                <tr>
                  <th scope="col">Employee</th>
                  {days.map((date) => (
                    <th
                      scope="col"
                      key={date}
                      className={selectedDate === date ? 'is-selected-date' : ''}
                    >
                      <button
                        onClick={() => {
                          setSelectedDate(date);
                          setDetails(null);
                        }}
                        aria-pressed={selectedDate === date}
                        aria-label={`Daily log for ${date}`}
                      >
                        <b>{Number(date.slice(-2))}</b>
                        <small>
                          {new Date(`${date}T12:00:00Z`).toLocaleDateString('en-GB', {
                            weekday: 'short',
                            timeZone: 'UTC',
                          })}
                        </small>
                      </button>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.employee.id}>
                    <th scope="row">
                      <EmployeeName employee={row.employee} />
                      {row.error && (
                        <small className="tracking-row-error" title={row.error}>
                          Attendance unavailable
                        </small>
                      )}
                    </th>
                    {days.map((date) => {
                      const status = trackingStatus(
                        row.days.find((day) => day.businessDate === date),
                        date > currentDate,
                      );
                      return (
                        <td key={date} className={selectedDate === date ? 'is-selected-date' : ''}>
                          <button
                            className={`tracking-mark ${status.tone}`}
                            title={`${row.employee.name ?? 'Employee'} · ${date}: ${status.label}`}
                            aria-label={`${row.employee.name ?? 'Employee'}, ${date}, ${status.label}`}
                            onClick={() => {
                              setSelectedDate(date);
                              setDetails(row.employee.id);
                            }}
                          >
                            {status.code}
                          </button>
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="tracking-legend">
            <span>
              <b>SET</b> Assignment / schedule setup required
            </span>
            <span>
              <b>P</b> Present
            </span>
            <span>
              <b>L</b> Late
            </span>
            <span>
              <b>A</b> Absent
            </span>
            <span>
              <b>LV</b> Leave
            </span>
            <span>
              <b>H / OFF</b> Holiday / rest
            </span>
            <span>
              <b>HD / HR</b> Half day / hourly
            </span>
            <span>
              <b>IN / REC</b> Recorded, unclassified
            </span>
            <span>
              <b>N/A</b> Unavailable
            </span>
          </div>
        </section>
      )}
      {hasRows && (
        <section className="tracking-panel">
          <div className="tracking-panel-heading">
            <div>
              <h2>Daily attendance log</h2>
              <p>{selectedDate} · Only the employees on this page</p>
            </div>
            <TextInput
              label="Log date"
              type="date"
              min={range?.from}
              max={range?.to}
              value={selectedDate}
              onChange={(event) => {
                if (days.includes(event.target.value)) {
                  setSelectedDate(event.target.value);
                  setDetails(null);
                }
              }}
            />
          </div>
          {selectedDate > currentDate ? (
            <p className="tracking-empty">
              This is a future date. Attendance has not been recorded yet.
            </p>
          ) : (
            <div className="tracking-log-scroll">
              <table className="tracking-log">
                <thead>
                  <tr>
                    <th>Employee</th>
                    <th>Status</th>
                    <th>Scheduled shift</th>
                    <th>Worked</th>
                    <th>Late</th>
                    <th>Overtime</th>
                    <th>Exceptions</th>
                    <th>Details</th>
                  </tr>
                </thead>
                <tbody>
                  {daily.map((row) => {
                    const status = trackingStatus(
                      row.days.find((day) => day.businessDate === selectedDate),
                    );
                    return (
                      <tr key={row.employee.id}>
                        <th scope="row">
                          <EmployeeName employee={row.employee} />
                        </th>
                        <td>
                          <span className={`tracking-mark ${status.tone}`} title={status.label}>
                            {status.code}
                          </span>
                          <small>{row.error ? 'Read unavailable' : status.label}</small>
                        </td>
                        <td>
                          {row.day
                            ? `${clockTime(row.day.roster.startsAt, row.day.timeZone)} – ${clockTime(row.day.roster.endsAt, row.day.timeZone)}`
                            : 'Unavailable'}
                        </td>
                        <td>{trackingDuration(row.day?.facts.workedMinutes)}</td>
                        <td>{trackingDuration(row.day?.policy.lateMinutes)}</td>
                        <td>{trackingDuration(row.day?.policy.overtimeMinutes)}</td>
                        <td>{row.day?.exceptionCount ?? 'Unavailable'}</td>
                        <td>
                          <button
                            className="tracking-detail-button"
                            onClick={() =>
                              setDetails(details === row.employee.id ? null : row.employee.id)
                            }
                            aria-expanded={details === row.employee.id}
                          >
                            {details === row.employee.id ? 'Close' : 'View day'}
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
          {details &&
            selectedDate <= currentDate &&
            rows.find((row) => row.employee.id === details) && (
              <TrackingDayDetails
                key={`${details}:${selectedDate}`}
                row={rows.find((row) => row.employee.id === details)!}
                date={selectedDate}
              />
            )}
        </section>
      )}
      {directory.data && !directory.isError && (
        <footer className="tracking-pagination">
          <span>10 employees per page · Counts apply to this page, not the whole company.</span>
          <div>
            <button
              disabled={page === 1 || busy}
              onClick={() => {
                setPage(page - 1);
                setDetails(null);
              }}
            >
              <ChevronLeft size={15} /> Previous
            </button>
            <span>
              {page} / {pageCount}
            </span>
            <button
              disabled={page >= pageCount || busy}
              onClick={() => {
                setPage(page + 1);
                setDetails(null);
              }}
            >
              Next <ChevronRight size={15} />
            </button>
          </div>
        </footer>
      )}
    </div>
  );
}
function EmployeeName({ employee }: { employee: TrackingEmployee }) {
  return (
    <div className="tracking-employee">
      <span className="tracking-avatar" aria-hidden="true">
        {(employee.name ?? 'Employee')
          .split(' ')
          .slice(0, 2)
          .map((part) => part[0])
          .join('')}
      </span>
      <div>
        <strong>{employee.name ?? 'Employee'}</strong>
        <small>EMP-{employee.employeeNumber}</small>
      </div>
    </div>
  );
}
function TrackingStat({ label, value, detail }: { label: string; value: string; detail: string }) {
  return (
    <div>
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </div>
  );
}
function clockTime(value: string | null | undefined, timeZone: string) {
  if (!value) return '—';
  try {
    return new Date(value).toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      timeZone,
    });
  } catch {
    return 'Time unavailable';
  }
}
function TrackingDayDetails({ row, date }: { row: TrackingRow; date: string }) {
  const access = useAttendanceAccess();
  const day = availableDay(row.days.find((entry) => entry.businessDate === date));
  const allowed = access.can('attendance.punches.read') && Boolean(day);
  const sessions = useQuery({
    queryKey: ['attendance', access.subject, 'tracking-sessions', row.employee.id, date],
    queryFn: ({ signal }) => {
      if (!access.ready || !allowed) throw new ApiError(403);
      return loadTrackingSessions(row.employee.id, date, signal);
    },
    enabled: allowed,
    retry: false,
    gcTime: 0,
    refetchOnWindowFocus: false,
  });
  return (
    <section className="tracking-day-detail">
      <h3>
        {row.employee.name ?? 'Employee'} · {date}
      </h3>
      <Link to={`/hr/attendance/employees/${row.employee.id}`}>
        Open employee attendance record
      </Link>
      {row.errorCode === 'employee_not_approved' ? (
        <AttendanceSetupNotice employeeId={row.employee.id} hiring />
      ) : row.errorCode === 'employee_assignment_unavailable' || needsAttendanceSetup(day) ? (
        <AttendanceSetupNotice employeeId={row.employee.id} />
      ) : row.error ? (
        <p role="alert">{row.error}</p>
      ) : null}
      {!day ? (
        <p>
          The retained attendance record is unavailable for this date. No absence or hours have been
          assumed.
        </p>
      ) : (
        <>
          <p>
            {day.isLocked ? 'Locked record' : 'Open record'} · {day.timeZone} ·{' '}
            {trackingDuration(day.facts.workedMinutes)} closed-session time
          </p>
          {!allowed ? (
            <p>Recorded check-in/out times require attendance punch-read permission.</p>
          ) : (
            <>
              <QueryState
                query={sessions}
                empty={sessions.data?.items.length === 0}
                emptyText="No visible current session was returned for this day."
              />
              {!sessions.isError && sessions.data && (
                <>
                  <table className="tracking-session-table">
                    <thead>
                      <tr>
                        <th>Session start</th>
                        <th>Session end</th>
                        <th>Recorded duration</th>
                      </tr>
                    </thead>
                    <tbody>
                      {sessions.data.items.map(({ session }) => (
                        <tr key={session.id}>
                          <td>{clockTime(session.startsAtUtc, day.timeZone)}</td>
                          <td>
                            {session.endsAtUtc
                              ? clockTime(session.endsAtUtc, day.timeZone)
                              : 'Still open'}
                          </td>
                          <td>{trackingDuration(session.factualWorkedMinutes)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  {sessions.data.nextCursor && (
                    <p>
                      More session revisions exist. Open the employee record for the complete audit.
                    </p>
                  )}
                </>
              )}
            </>
          )}
          {day.policy.explanations.length > 0 && (
            <details>
              <summary>Calculation notes</summary>
              <ul>
                {day.policy.explanations.map((note, index) => (
                  <li key={index}>{note}</li>
                ))}
              </ul>
            </details>
          )}
        </>
      )}
    </section>
  );
}
