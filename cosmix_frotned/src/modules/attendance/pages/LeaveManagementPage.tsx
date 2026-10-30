import { EmployeeReference, EmployeePicker } from '@/components/employees/EmployeeReference';
import { useState } from 'react';
import { employeeSearchTerm, formatEmployeeCode } from '@/utils/employee-code';
import { ScopeSelect } from '../operations/review/ReviewUi';
import { Link, useParams } from 'react-router-dom';
import {
  Award,
  Banknote,
  Calendar,
  CalendarCheck,
  CalendarDays,
  CheckCircle2,
  ClipboardCheck,
  ExternalLink,
  RefreshCw,
  Search,
  Settings,
  ShieldCheck,
  UserCheck,
  Users,
} from 'lucide-react';
import { z } from 'zod';
import { Pagination } from '@/components/data-display/Pagination';
import { Denied, PrivateWorkspace, QueryState } from '../components/SchedulingUi';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { queryString } from '../api/attendance-api';
import { date, id, pageOf } from '../schemas/common';
import { canEmployee, scopeNames, supportedScopes, type Scope } from '../operations/review/access';
import { canAll } from '../operations/leave-policy/inputs';
import { monthRange, today, validRange } from '../operations/review/helpers';
import {
  approvedAttendanceEmployees,
  approvedAttendanceFilter,
} from '../schemas/employee-selection';
import '@/styles/leave-management.css';

const permission = 'attendance.leaves.team-calendar.read';

const statusLabels = {
  pending: 'Pending Admin review',
  approved: 'Approved',
  returned: 'Returned',
  rejected: 'Rejected',
  cancelled: 'Cancelled',
  cancellation_pending: 'Cancellation pending',
  superseded: 'Superseded',
} as const;

type LeaveStatus = keyof typeof statusLabels;

const entry = z.object({
  employeeId: id,
  date,
  status: z.enum(Object.keys(statusLabels) as [LeaveStatus, ...Array<LeaveStatus>]),
});

export function LeaveManagementPage() {
  return (
    <PrivateWorkspace resource="leave-management">
      <LeaveManagementWorkspace />
    </PrivateWorkspace>
  );
}

function LeaveManagementWorkspace() {
  const access = useAttendanceAccess();
  const params = useParams<{ employeeId?: string }>();
  const [selectedEmployeeId, setSelectedEmployeeId] = useState<string>(params.employeeId ?? '');
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [customScope, setCustomScope] = useState<Scope | undefined>(undefined);

  const allowedDirectory = access.can('hr.employees.read');
  const allowedCalendar = access.can(permission);

  // Fetch approved employees for directory dropdown & quick selection
  const directoryQuery = useAttendanceQuery(
    '/hr/employees' +
      queryString({
        search: employeeSearchTerm(submittedSearch),
        page: 1,
        pageSize: 100,
        ...approvedAttendanceFilter,
      }),
    approvedAttendanceEmployees,
    allowedDirectory,
  );

  const employees = directoryQuery.data?.items ?? [];
  const totalEmployees = directoryQuery.data?.totalCount ?? employees.length;

  const currentEmployee = employees.find((e) => e.id === selectedEmployeeId);
  const currentMonth = today().slice(0, 7);

  const scopes = supportedScopes(access.user, permission);
  const effectiveScope =
    customScope ??
    scopes.find((grant) => grant.kind === 7) ??
    (scopes.length === 1 ? scopes[0] : undefined);

  if (!allowedCalendar && !access.can('attendance.leaves.review')) {
    return (
      <Denied>
        Leave management requires attendance leave-calendar or leave-review permissions. Ask your
        administrator for the appropriate HR authorization.
      </Denied>
    );
  }

  return (
    <div className="leave-management-page">
      {/* 1. Header Bar */}
      <header className="leave-header">
        <div className="leave-title-group">
          <span className="leave-eyebrow">Attendance / Leave Management</span>
          <h1>Leave management</h1>
          <p>Review recorded employee leave dates and follow up on pending requests.</p>
        </div>

        <nav className="leave-header-actions" aria-label="Leave navigation actions">
          {canAll(access.user, 'attendance.leaves.review') && (
            <Link className="leave-action-btn leave-action-btn--primary" to="/hr/attendance/leaves">
              <ClipboardCheck size={13} aria-hidden="true" />
              Admin review queue
            </Link>
          )}
          {canAll(access.user, 'attendance.leaves.configure') && (
            <Link className="leave-action-btn" to="/hr/attendance/leave-settings">
              <Settings size={13} aria-hidden="true" />
              Leave settings
            </Link>
          )}
          <Link className="leave-action-btn" to="/hr/attendance/leave-benefits">
            <Award size={13} aria-hidden="true" />
            Leave benefits
          </Link>
          <Link className="leave-action-btn" to="/hr/leave-encashment">
            <Banknote size={13} aria-hidden="true" />
            Leave encashment
          </Link>
        </nav>
      </header>

      {/* 2. Executive KPI Summary Metrics */}
      <section className="leave-stats" aria-label="Leave management summary">
        <div className="leave-stat-card">
          <div className="leave-stat-info">
            <span className="leave-stat-label">Approved staff</span>
            <strong className="leave-stat-value">
              {directoryQuery.isFetching ? '…' : totalEmployees}
            </strong>
            <span className="leave-stat-note">Employee directory</span>
          </div>
          <span className="leave-stat-icon blue">
            <Users size={16} aria-hidden="true" />
          </span>
        </div>

        <div className="leave-stat-card">
          <div className="leave-stat-info">
            <span className="leave-stat-label">Active cycle</span>
            <strong className="leave-stat-value">{currentMonth}</strong>
            <span className="leave-stat-note">Monthly calendar window</span>
          </div>
          <span className="leave-stat-icon navy">
            <Calendar size={16} aria-hidden="true" />
          </span>
        </div>

        <div className="leave-stat-card">
          <div className="leave-stat-info">
            <span className="leave-stat-label">Tracked statuses</span>
            <strong className="leave-stat-value">7 types</strong>
            <span className="leave-stat-note">Approved, pending, returned…</span>
          </div>
          <span className="leave-stat-icon green">
            <CheckCircle2 size={16} aria-hidden="true" />
          </span>
        </div>

        <div className="leave-stat-card">
          <div className="leave-stat-info">
            <span className="leave-stat-label">Calendar authority</span>
            <strong className="leave-stat-value">
              {scopes.some((s) => s.kind === 7) ? 'All Scopes' : `${scopes.length} Grants`}
            </strong>
            <span className="leave-stat-note">Authorized access</span>
          </div>
          <span className="leave-stat-icon amber">
            <ShieldCheck size={16} aria-hidden="true" />
          </span>
        </div>
      </section>

      {/* 3. Employee Selection Toolbar Card */}
      <section className="leave-picker-card" aria-label="Employee selection">
        <div className="leave-picker-heading">
          <h2>
            <UserCheck size={14} aria-hidden="true" />
            Employee Leave Calendar
          </h2>
          <p>
            Select an employee from the directory to review their booked leaves and scheduled
            absence.
          </p>
        </div>

        <div className="leave-picker-row">
          <div className="leave-picker-field">
            <label htmlFor="leave-emp-search">Find employee</label>
            <input
              id="leave-emp-search"
              type="search"
              placeholder="Filter by name or employee number…"
              value={search}
              maxLength={100}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  setSubmittedSearch(search.trim());
                }
              }}
            />
          </div>

          <button
            type="button"
            className="leave-picker-btn"
            disabled={directoryQuery.isFetching}
            onClick={() => setSubmittedSearch(search.trim())}
          >
            <Search size={13} aria-hidden="true" />
            Search
          </button>

          <div className="leave-picker-field" style={{ flex: '1.4 1 280px' }}>
            <label htmlFor="leave-emp-select">Select employee</label>
            <select
              id="leave-emp-select"
              value={selectedEmployeeId}
              onChange={(e) => setSelectedEmployeeId(e.target.value)}
            >
              <option value="">-- Choose an employee to load calendar --</option>
              {employees.map((emp) => (
                <option key={emp.id} value={emp.id}>
                  {emp.name ?? 'Employee'} ({formatEmployeeCode(emp.employeeNumber)})
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Quick-select chips */}
        {employees.length > 0 && (
          <div className="leave-quick-select">
            <span>Quick view:</span>
            {employees.slice(0, 5).map((emp) => (
              <button
                type="button"
                key={emp.id}
                className={`leave-chip ${selectedEmployeeId === emp.id ? 'is-active' : ''}`}
                onClick={() => setSelectedEmployeeId(emp.id)}
              >
                {emp.name ?? 'Employee'} <small>({formatEmployeeCode(emp.employeeNumber)})</small>
              </button>
            ))}
          </div>
        )}

        {/* Advanced manual ID & scope entry */}
        <details className="leave-advanced-details">
          <summary>Employee selection & authority scope</summary>
          <div className="leave-advanced-form">
            <div className="leave-picker-field">
              <EmployeePicker
                label="Employee"
                value={selectedEmployeeId}
                onChange={setSelectedEmployeeId}
              />
            </div>

            {scopes.length > 1 && !scopes.some((grant) => grant.kind === 7) && (
              <div className="leave-picker-field">
                <ScopeSelect
                  permission={permission}
                  value={customScope}
                  onChange={setCustomScope}
                />
              </div>
            )}
          </div>
        </details>
      </section>

      {/* 4. Main Calendar Content Card */}
      <section className="leave-calendar-card" aria-label="Leave calendar details">
        {selectedEmployeeId ? (
          <LeaveCalendarView
            employeeId={selectedEmployeeId}
            employeeName={currentEmployee?.name ?? null}
            employeeNumber={currentEmployee?.employeeNumber}
            scope={effectiveScope}
            onClear={() => setSelectedEmployeeId('')}
          />
        ) : (
          <div className="leave-empty-workspace">
            <span className="leave-empty-icon" aria-hidden="true">
              <CalendarDays size={22} />
            </span>
            <h3>Select an Employee to View Leave Calendar</h3>
            <p>
              Choose an employee from the dropdown above or click one of the quick view buttons to
              load their approved leave schedule and recorded absence dates.
            </p>
          </div>
        )}
      </section>
    </div>
  );
}

function LeaveCalendarView({
  employeeId,
  employeeName,
  employeeNumber,
  scope,
  onClear,
}: {
  employeeId: string;
  employeeName: string | null;
  employeeNumber?: number;
  scope?: Scope;
  onClear: () => void;
}) {
  const access = useAttendanceAccess();
  const initial = monthRange(today().slice(0, 7))!;
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [page, setPage] = useState(1);

  const valid = validRange(from, to, true);
  const allowed = canEmployee(access.user, permission, employeeId, scope);

  const schema = pageOf(entry).refine(
    (result) =>
      result.page === page &&
      result.pageSize === 25 &&
      result.items.every(
        (row) => row.employeeId === employeeId && row.date >= from && row.date <= to,
      ),
    'The calendar response does not match the selected employee and dates.',
  );

  const query = useAttendanceQuery(
    '/leaves/team-calendar' +
      queryString({ employeeIds: employeeId, from, to, page, pageSize: 25 }),
    schema,
    allowed && valid,
  );

  const rows = valid && !query.isError ? (query.data?.items ?? []) : [];
  const totalCount = query.data?.totalCount ?? 0;

  function setMonthPreset(offset: number) {
    const d = new Date();
    d.setMonth(d.getMonth() + offset);
    const m = d.toISOString().slice(0, 7);
    const range = monthRange(m);
    if (range) {
      setFrom(range.from);
      setTo(range.to);
      setPage(1);
    }
  }

  function setNext30Days() {
    const start = today();
    const end = new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10);
    setFrom(start);
    setTo(end);
    setPage(1);
  }

  const initials = (employeeName ?? 'Employee')
    .split(' ')
    .slice(0, 2)
    .map((p) => p[0])
    .join('');

  return (
    <>
      {/* Selected Employee Profile Header */}
      <div className="leave-employee-banner">
        <div className="leave-employee-profile">
          <span className="leave-avatar" aria-hidden="true">
            {initials}
          </span>
          <div className="leave-employee-text">
            <strong>{employeeName ?? 'Employee'}</strong>
            <small>
              <EmployeeReference employeeId={employeeId} employeeNumber={employeeNumber} /> · Leave
              Calendar
            </small>
          </div>
        </div>

        <div className="leave-employee-badges">
          <span className="leave-scope-badge">
            <ShieldCheck size={11} style={{ marginRight: 3 }} />
            {scope
              ? scope.kind === 7
                ? 'Scope: All'
                : `Scope: ${scopeNames[scope.kind]}`
              : 'Scope: Default'}
          </span>
          <Link
            to={`/hr/employees/${employeeId}`}
            className="leave-action-btn"
            style={{ height: 26, fontSize: '10.5px' }}
          >
            Profile <ExternalLink size={11} />
          </Link>
          <button
            type="button"
            className="leave-action-btn"
            style={{ height: 26, fontSize: '10.5px' }}
            onClick={onClear}
          >
            Change
          </button>
        </div>
      </div>

      {/* Date Range Toolbar */}
      <div className="leave-range-toolbar">
        <div className="leave-range-form">
          <div className="leave-date-group">
            <span>From:</span>
            <input
              type="date"
              value={from}
              onChange={(e) => {
                setFrom(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div className="leave-date-group">
            <span>To:</span>
            <input
              type="date"
              value={to}
              onChange={(e) => {
                setTo(e.target.value);
                setPage(1);
              }}
            />
          </div>

          <div className="leave-range-presets">
            <button type="button" className="leave-preset-btn" onClick={() => setMonthPreset(0)}>
              This Month
            </button>
            <button type="button" className="leave-preset-btn" onClick={() => setNext30Days()}>
              Next 30 Days
            </button>
          </div>
        </div>

        <button
          type="button"
          className="leave-refresh-btn"
          disabled={!valid || !allowed || query.isFetching}
          onClick={() => void query.refetch()}
        >
          <RefreshCw
            size={12}
            className={query.isFetching ? 'animate-spin' : ''}
            aria-hidden="true"
          />
          {query.isFetching ? 'Refreshing…' : 'Refresh calendar'}
        </button>
      </div>

      <p className="leave-range-note">
        Viewing up to 31 calendar dates. Days without recorded leave indicate regular rostered
        workdays, rest periods, or public holidays.
      </p>

      {/* Query State Feedback */}
      {!valid && (
        <p className="leave-range-note" style={{ color: '#b91c1c' }} role="alert">
          Please select a valid date range of at most 31 days (inclusive).
        </p>
      )}

      {valid && <QueryState query={query} />}

      {/* Leave Records Table */}
      {valid && !query.isFetching && rows.length > 0 && (
        <>
          <div className="leave-table-container">
            <table className="leave-table">
              <thead>
                <tr>
                  <th scope="col" style={{ width: '130px' }}>
                    Date
                  </th>
                  <th scope="col" style={{ width: '110px' }}>
                    Day of week
                  </th>
                  <th scope="col" style={{ width: '220px' }}>
                    Recorded leave status
                  </th>
                  <th scope="col">Administrative note</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const dayName = new Date(`${row.date}T12:00:00Z`).toLocaleDateString('en-GB', {
                    weekday: 'long',
                    timeZone: 'UTC',
                  });
                  return (
                    <tr key={`${row.date}:${row.status}`}>
                      <td style={{ fontWeight: 650, color: '#0f172a' }}>{row.date}</td>
                      <td style={{ color: '#64748b' }}>{dayName}</td>
                      <td>
                        <span className={`leave-status-pill ${row.status}`}>
                          {statusLabels[row.status] ?? row.status}
                        </span>
                      </td>
                      <td style={{ color: '#64748b' }}>
                        Leave entry recorded on team calendar roster
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {totalCount > 25 && (
            <div className="leave-pagination">
              <span>
                Showing {rows.length} of {totalCount} records
              </span>
              <Pagination
                page={page}
                pageSize={25}
                total={totalCount}
                onChange={setPage}
                disabled={query.isFetching}
              />
            </div>
          )}
        </>
      )}

      {/* Empty State when no leaves in period */}
      {valid && !query.isFetching && !query.isError && rows.length === 0 && (
        <div className="leave-no-records">
          <CalendarCheck size={28} style={{ color: '#16a34a', marginBottom: 4 }} />
          <strong>No recorded leave dates in this period</strong>
          <span>
            The selected employee has no booked or pending leave between {from} and {to}.
          </span>
        </div>
      )}
    </>
  );
}
