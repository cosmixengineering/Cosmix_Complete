import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Building2, Calendar, Clock, RefreshCw, Search, Users } from 'lucide-react';
import { Pagination } from '@/components/data-display/Pagination';
import { useHrQuery } from '@/modules/hr/hooks/use-hr-queries';
import { useHrAccess } from '@/modules/hr/hooks/use-hr-access';
import { employeeArchivePageSchema } from '@/modules/hr/components/employee/employee-archive-directory';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { queryString } from '../api/attendance-api';
import { hasReportGrant } from '../operations/reports/access';
import { factualBatchFor } from '../operations/reports/schemas';
import { Denied, QueryState } from '../components/SchedulingUi';
import { businessToday } from '../utils/messages';
import '@/styles/attendance-processing.css';

// Factual session evidence is not an approved presence, lateness, leave or pay calculation.
export function AttendanceProcessingPage() {
  const access = useAttendanceAccess();
  const hrAccess = useHrAccess();
  const allowed = hasReportGrant(access.user, 'attendance.reports.read');
  const today = businessToday();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [view, setView] = useState<'matrix' | 'evidence'>('matrix');
  const [selectedDay, setSelectedDay] = useState(today);
  const monthStart = today.slice(0, 8) + '01';
  const directory = useHrQuery(
    'employees' + queryString({ page, pageSize: 25 }),
    employeeArchivePageSchema,
    ['hr.employees.read'],
    allowed,
  );
  const employees = useMemo(
    () => (!directory.isError ? (directory.data?.items ?? []) : []),
    [directory.data, directory.isError],
  );
  const employeeIds = useMemo(() => employees.map((employee) => employee.id), [employees]);
  const path = useMemo(() => {
    const params = new URLSearchParams({ from: monthStart, to: today });
    employeeIds.forEach((id) => params.append('employeeIds', id));
    return '/attendance/reports/factual/batch?' + params.toString();
  }, [monthStart, today, employeeIds]);
  const factual = useAttendanceQuery(
    path,
    factualBatchFor({ employeeIds, from: monthStart, to: today }),
    allowed && employeeIds.length > 0,
  );
  const rows = !factual.isError ? (factual.data?.rows ?? []) : [];
  const loaded = Boolean(
    !directory.isError &&
    !directory.isPending &&
    !factual.isError &&
    !factual.isPending &&
    factual.data,
  );
  const filtered = employees.filter(
    (employee) =>
      !search.trim() ||
      `${employee.name ?? ''} ${employee.employeeNumber}`
        .toLowerCase()
        .includes(search.trim().toLowerCase()),
  );
  const days = Array.from(
    { length: new Date(`${today}T00:00:00Z`).getUTCDate() },
    (_, index) => `${today.slice(0, 8)}${String(index + 1).padStart(2, '0')}`,
  );
  const dayRows = rows.filter(
    (row) =>
      row.businessDate === selectedDay &&
      filtered.some((employee) => employee.id === row.employeeId),
  );
  const evidenceDays = rows.filter((row) => row.sessionCount > 0).length;
  const workedMinutes = rows.reduce((sum, row) => sum + row.workedMinutes, 0);
  const exceptions = rows.reduce((sum, row) => sum + row.exceptionCount, 0);

  function refresh() {
    void directory.refetch();
    if (employeeIds.length) void factual.refetch();
  }
  if (!access.ready || !hrAccess.ready)
    return <p role="status">Loading authenticated attendance access...</p>;
  if (!allowed || !hrAccess.can('hr.employees.read'))
    return <Denied>Attendance reports and employee directory access are required.</Denied>;

  return (
    <div className="att-v4-shell">
      <div className="att-v4-header">
        <div className="att-v4-title-row">
          <h1 className="att-v4-title">Attendance &amp; Time-Tracking</h1>
          <span className="att-v4-breadcrumb">
            <Building2 size={13} />
            Human Resources
          </span>
        </div>
        <button
          type="button"
          className="att-v4-btn-action"
          disabled={directory.isFetching || factual.isFetching}
          onClick={refresh}
        >
          <RefreshCw size={14} /> Refresh records
        </button>
      </div>
      <p className="hr-notice">
        Factual attendance for {monthStart} to {today}, covering this directory page only (up to 25
        employees). Session evidence does not establish lateness, absence, leave entitlement,
        overtime or deductions. Use the approved review workflows for changes.
      </p>
      <div className="att-v4-kpis-grid">
        {[
          { label: 'Employees in this selection', value: employees.length, Icon: Users },
          { label: 'Days with session evidence', value: evidenceDays, Icon: Calendar },
          { label: 'Recorded worked minutes', value: workedMinutes, Icon: Clock },
          { label: 'Recorded exceptions', value: exceptions, Icon: Building2 },
        ].map(({ label, value, Icon }) => (
          <div className="att-v4-kpi-card" key={label}>
            <div className="att-v4-kpi-left">
              <span className="att-v4-kpi-label">{label}</span>
              <div className="att-v4-kpi-val">{loaded ? value : '—'}</div>
              <span className="att-v4-kpi-sub">Selected employees and dates</span>
            </div>
            <div className="att-v4-kpi-icon-box">
              <Icon size={18} />
            </div>
          </div>
        ))}
      </div>
      <div className="att-v4-header">
        <div className="att-v4-tabs" role="tablist" aria-label="Attendance evidence views">
          <button
            type="button"
            role="tab"
            aria-selected={view === 'matrix'}
            onClick={() => setView('matrix')}
          >
            Monthly evidence
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={view === 'evidence'}
            onClick={() => setView('evidence')}
          >
            Daily evidence
          </button>
        </div>
        <Link to="/hr/attendance/corrections" className="att-v4-btn-action">
          Attendance corrections
        </Link>
      </div>
      <label>
        <Search size={14} /> Search this page
        <input
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Name or employee number"
        />
      </label>
      <QueryState
        query={directory}
        empty={!employees.length}
        emptyText="No employees returned for this page."
      />
      {employees.length > 0 && <QueryState query={factual} />}
      {loaded && !filtered.length && <p role="status">No employees match this page's search.</p>}
      {loaded &&
        filtered.length > 0 &&
        (view === 'matrix' ? (
          <div style={{ overflowX: 'auto' }}>
            <p>
              C = closed session evidence; O = open session; N = no accepted session evidence. N
              does not mean absent.
            </p>
            <table className="att-v4-matrix-table">
              <thead>
                <tr>
                  <th className="col-employee">Employee</th>
                  {days.map((day) => (
                    <th key={day} className="col-day">
                      {day.slice(8)}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filtered.map((employee) => (
                  <tr key={employee.id}>
                    <td className="col-employee">
                      <div className="att-emp-card">
                        <div className="att-emp-info">
                          <span className="att-emp-name">
                            {employee.name ?? 'Name unavailable'}
                          </span>
                          <span className="att-emp-sub">Employee #{employee.employeeNumber}</span>
                        </div>
                      </div>
                    </td>
                    {days.map((day) => {
                      const row = rows.find(
                        (item) => item.employeeId === employee.id && item.businessDate === day,
                      );
                      const code =
                        row?.factualStatus === 'closed_session_evidence'
                          ? 'C'
                          : row?.factualStatus === 'open_session'
                            ? 'O'
                            : row?.factualStatus === 'no_accepted_session_evidence'
                              ? 'N'
                              : '—';
                      return (
                        <td
                          key={day}
                          className="col-day"
                          title={
                            row
                              ? `${row.workedMinutes} worked minutes; ${row.exceptionCount} exceptions`
                              : 'No returned evidence'
                          }
                        >
                          <span className="att-badge dash">{code}</span>
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <section>
            <label>
              Evidence date
              <input
                type="date"
                min={monthStart}
                max={today}
                value={selectedDay}
                onChange={(event) => setSelectedDay(event.target.value)}
              />
            </label>
            {!dayRows.length ? (
              <p role="status">No factual records returned for the selected date and employees.</p>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table className="att-v4-matrix-table">
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Evidence</th>
                      <th>Worked minutes</th>
                      <th>Sessions</th>
                      <th>Open sessions</th>
                      <th>Exceptions</th>
                      <th>Month closure</th>
                    </tr>
                  </thead>
                  <tbody>
                    {dayRows.map((row) => (
                      <tr key={row.employeeId}>
                        <td>
                          {employees.find((employee) => employee.id === row.employeeId)?.name ??
                            row.employeeId}
                        </td>
                        <td>{row.factualStatus.replaceAll('_', ' ')}</td>
                        <td>{row.workedMinutes}</td>
                        <td>{row.sessionCount}</td>
                        <td>{row.openSessions}</td>
                        <td>{row.exceptionCount}</td>
                        <td>{row.isLocked ? 'Locked' : 'Unlocked'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        ))}
      {directory.data && !directory.isError && (
        <Pagination
          page={page}
          pageSize={25}
          total={directory.data.totalCount}
          disabled={directory.isFetching || factual.isFetching}
          onChange={setPage}
        />
      )}
    </div>
  );
}
