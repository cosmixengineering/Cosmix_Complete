import { useMemo } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Users,
  UserCheck,
  ClipboardList,
  Wallet,
  Clock,
  Calendar,
  Coins,
  FileText,
  FileSpreadsheet,
  ArrowUpRight,
  Clock3,
  Building2,
  MapPin,
  CalendarDays,
  Banknote,
  RefreshCw,
} from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { pageOf, employeeSchema } from '../schemas/contracts';
import { siteSchema } from '../schemas/sites';
import { submissionSummarySchema } from '../schemas/approvals';
import { useAttendanceAccess } from '@/modules/attendance/hooks/use-attendance-access';
import { useAttendanceQuery } from '@/modules/attendance/hooks/use-attendance-query';
import { leavePage } from '@/modules/attendance/operations/leave-policy/contracts';
import {
  dashboardCount,
  dashboardCsv,
  dashboardMoney,
  dashboardRead,
  dashboardStatusLabel,
  type DashboardCount,
} from '../utils/dashboard';
import { dashboardAttendanceSummary, dashboardPayrollSummary } from '../schemas/dashboard';
import { errorMessage, labelDate } from '../utils/messages';
import { businessDate } from '@/utils/format';
import '@/styles/hr-dashboard.css';

interface ActivityItem {
  id: string;
  employeeName: string;
  avatarText: string;
  type: string;
  details: string;
  date: string;
  status: 'Pending' | 'Cancellation pending';
  actionUrl?: string;
}

function ReadNotice({
  label,
  state,
  query,
}: {
  label: string;
  state: DashboardCount;
  query: { error: unknown; refetch: () => unknown };
}) {
  if (state.status === 'ready') return null;
  return (
    <p className="dash-v3-notice" role={state.status === 'error' ? 'alert' : 'status'}>
      <strong>{label}: </strong>
      {state.status === 'error'
        ? errorMessage(query.error)
        : state.status === 'restricted'
          ? 'Your current permissions do not allow this view.'
          : state.label}
      {(state.status === 'error' || state.status === 'unavailable') && (
        <button type="button" className="hr-text-button" onClick={() => void query.refetch()}>
          Retry
        </button>
      )}
    </p>
  );
}

export function HrDashboardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const access = useHrAccess();
  const attendanceAccess = useAttendanceAccess();

  const displayName = user?.account?.displayName || 'HR Officer';

  const canReadEmployees = access.can('hr.employees.read');
  const canReadSites = access.can('hr.sites.read');
  const canReadHiring = access.can('hr.employees.read', 'hr.employees.submissions.review');
  const canReadLeaves = attendanceAccess.can('attendance.leaves.review');
  const canReadAttendance = access.can('attendance.reports.read');
  const canReadPayroll = access.can(
    'hr.employees.compensation.read',
    'attendance.payroll-handoff.read',
  );
  const year = Number(
    new Intl.DateTimeFormat('en-US', { timeZone: 'Asia/Karachi', year: 'numeric' }).format(
      new Date(),
    ),
  );

  const employeesQuery = useHrQuery('employees?page=1&pageSize=1', pageOf(employeeSchema), [
    'hr.employees.read',
  ]);

  // The API's status filter is onboarding status, not current employment status.
  const approvedEmployeesQuery = useHrQuery(
    'employees?status=2&pageSize=1',
    pageOf(employeeSchema),
    ['hr.employees.read'],
  );

  const draftEmployeesQuery = useHrQuery('employees?status=0&pageSize=1', pageOf(employeeSchema), [
    'hr.employees.read',
  ]);
  const sitesQuery = useHrQuery('sites?page=1&pageSize=1', pageOf(siteSchema), ['hr.sites.read']);

  const submissionsQuery = useHrQuery(
    'employees/submissions?page=1&pageSize=10&status=1',
    pageOf(submissionSummarySchema),
    ['hr.employees.read', 'hr.employees.submissions.review'],
  );
  const pendingLeavesQuery = useAttendanceQuery(
    `/leaves/admin/queue?pendingOnly=true&from=${year}-01-01&to=${year}-12-31&pageSize=10`,
    leavePage,
    canReadLeaves,
  );
  const attendanceQuery = useHrQuery('dashboard/attendance', dashboardAttendanceSummary, [
    'attendance.reports.read',
  ]);
  const payrollQuery = useHrQuery('dashboard/payroll', dashboardPayrollSummary, [
    'hr.employees.compensation.read',
    'attendance.payroll-handoff.read',
  ]);
  const employees = dashboardCount(employeesQuery, canReadEmployees, access.ready);
  const approved = dashboardCount(approvedEmployeesQuery, canReadEmployees, access.ready);
  const drafts = dashboardCount(draftEmployeesQuery, canReadEmployees, access.ready);
  const sites = dashboardCount(sitesQuery, canReadSites, access.ready);
  const hiring = dashboardCount(submissionsQuery, canReadHiring, access.ready);
  const leaves = dashboardCount(pendingLeavesQuery, canReadLeaves, attendanceAccess.ready);
  const attendance = dashboardRead(attendanceQuery, canReadAttendance, access.ready);
  const payroll = dashboardRead(payrollQuery, canReadPayroll, access.ready);
  const attendanceData = attendance.status === 'ready' ? attendanceQuery.data : undefined;
  const payrollData = payroll.status === 'ready' ? payrollQuery.data : undefined;
  const approvalsLabel =
    hiring.count !== undefined && leaves.count !== undefined
      ? `${hiring.count + leaves.count} Cases`
      : hiring.status === 'ready' || leaves.status === 'ready'
        ? 'Partial view'
        : hiring.status === 'loading' || leaves.status === 'loading'
          ? 'Loading…'
          : hiring.status === 'restricted' && leaves.status === 'restricted'
            ? 'Access restricted'
            : 'Could not load';
  const approvalsDetail = `Hires: ${hiring.label} • Leaves (${year}): ${leaves.label}`;
  const attendanceLabel = attendanceData
    ? `${attendanceData.withAcceptedSessionEvidence} / ${attendanceData.activeEmployees}`
    : attendance.label;
  const attendanceDetail = attendanceData
    ? `Employees with accepted session evidence on ${attendanceData.businessDate} (Asia/Karachi).`
    : 'Today’s attendance evidence for active employees.';
  const salaryGroups = payrollData?.effectiveMonthlySalary;
  const payrollLabel = !payrollData
    ? payroll.label
    : payrollData.activeEmployees === 0
      ? 'No active employees'
      : payrollData.monthlySalaryEmployeeCount === 0
        ? payrollData.missingSalaryEmployeeCount > 0
          ? 'Salary setup required'
          : 'No monthly salaries'
        : salaryGroups?.length === 1 && salaryGroups[0]
          ? dashboardMoney(salaryGroups[0].grossAmount, salaryGroups[0].currency)
          : `${salaryGroups?.length} currency totals`;
  const payrollDetail = payrollData
    ? `${payrollData.monthlySalaryEmployeeCount} of ${payrollData.activeEmployees} active employees have an effective monthly salary.`
    : 'Effective monthly salary and prepared payroll handoff evidence.';
  const dashboardReads = [
    { query: employeesQuery, allowed: canReadEmployees },
    { query: approvedEmployeesQuery, allowed: canReadEmployees },
    { query: draftEmployeesQuery, allowed: canReadEmployees },
    { query: sitesQuery, allowed: canReadSites },
    { query: submissionsQuery, allowed: canReadHiring },
    { query: pendingLeavesQuery, allowed: canReadLeaves },
    { query: attendanceQuery, allowed: canReadAttendance },
    { query: payrollQuery, allowed: canReadPayroll },
  ];
  const refreshing = dashboardReads.some(({ query, allowed }) => allowed && query.isFetching);

  const dynamicActivities: ActivityItem[] = useMemo(() => {
    const list: ActivityItem[] = [];

    if (hiring.status === 'ready' && submissionsQuery.data?.items?.length) {
      for (const sub of submissionsQuery.data.items) {
        const empName = sub.name || `Employee #${sub.employeeNumber}`;
        const initials = empName
          .split(' ')
          .map((n) => n[0])
          .join('')
          .slice(0, 2)
          .toUpperCase();
        list.push({
          id: `sub-${sub.id}`,
          employeeName: empName,
          avatarText: initials || 'EM',
          type: 'Hiring approval',
          details: `Employee #${sub.employeeNumber} • Submitted for review`,
          date: labelDate(sub.submittedAt),
          status: 'Pending',
          actionUrl: '/hr/approvals',
        });
      }
    }

    if (leaves.status === 'ready' && pendingLeavesQuery.data?.items?.length) {
      for (const lr of pendingLeavesQuery.data.items) {
        const cancellationPending = lr.cancellations.some((request) => request.state === 0);
        list.push({
          id: `leave-${lr.id}`,
          employeeName: `Employee ID: ${lr.employeeId}`,
          avatarText: 'LV',
          type: `${lr.typeName} Request`,
          details: `${lr.requestedMinutes} minutes • ${lr.startDate} to ${lr.endDate}`,
          date: 'Submission time unavailable',
          status: cancellationPending ? 'Cancellation pending' : 'Pending',
          actionUrl: '/hr/leaves',
        });
      }
    }

    return list;
  }, [submissionsQuery.data, pendingLeavesQuery.data, hiring.status, leaves.status]);

  const handleExportCSV = () => {
    const csvContent = dashboardCsv([
      ['Metric', 'Value', 'Details'],
      ['Employee directory', employees.label, 'Unarchived directory records; includes drafts.'],
      ['Approved onboarding', approved.label, 'Onboarding status; not an active employment total.'],
      ['Draft onboarding', drafts.label, 'Draft onboarding records.'],
      ['Site directory', sites.label, 'Site records; not an attendance total.'],
      ["Today's turnout", attendanceLabel, attendanceDetail],
      ...(attendanceData
        ? [
            ['Attendance generated UTC', attendanceData.generatedAtUtc, attendanceData.coverage],
            ['Attendance business date', attendanceData.businessDate, attendanceData.timeZone],
            ['Active attendance population', String(attendanceData.activeEmployees), ''],
            ['Open session employees', String(attendanceData.openSessionEmployees), ''],
            [
              'Closed session only employees',
              String(attendanceData.closedSessionOnlyEmployees),
              '',
            ],
            [
              'No accepted session evidence',
              String(attendanceData.noAcceptedSessionEvidenceEmployees),
              'No accepted session evidence is not an absence classification.',
            ],
            ['Classified employees', String(attendanceData.classifiedEmployees), ''],
            ['Awaiting classification', String(attendanceData.notYetClassifiedEmployees), ''],
            ['Current saved summaries', String(attendanceData.currentSavedSummaryEmployees), ''],
            [
              'Unsaved or stale summaries',
              String(attendanceData.unsavedOrStaleSummaryEmployees),
              '',
            ],
            ...Object.entries(attendanceData.classifiedStatusCounts).map(([status, count]) => [
              `Attendance classification: ${dashboardStatusLabel(status)}`,
              String(count),
              attendanceData.businessDate,
            ]),
          ]
        : []),
      ['Pending approvals', approvalsLabel, approvalsDetail],
      ['Pending hiring approvals', hiring.label, 'All pending hiring submissions.'],
      [
        'Pending leave approvals',
        leaves.label,
        `Leave and cancellation queue: calendar year ${year}.`,
      ],
      ['Monthly salary estimate', payrollLabel, payrollDetail],
      ...(payrollData
        ? [
            ['Payroll summary generated UTC', payrollData.generatedAtUtc, payrollData.timeZone],
            ['Salary effective date', payrollData.businessDate, 'Approved effective compensation.'],
            ['Payroll period start', payrollData.periodStart, ''],
            ['Payroll period end (exclusive)', payrollData.periodEndExclusive, ''],
            ['Active salary population', String(payrollData.activeEmployees), ''],
            ['Missing salary records', String(payrollData.missingSalaryEmployeeCount), ''],
            ['Non-monthly salary records', String(payrollData.nonMonthlySalaryEmployeeCount), ''],
            ['Future salary versions excluded', String(payrollData.futureSalaryVersions), ''],
            [
              'Pending compensation changes excluded',
              String(payrollData.pendingCompensationChangeCount),
              '',
            ],
            ...payrollData.effectiveMonthlySalary.flatMap((group) => [
              [
                `Monthly base salary (${group.currency})`,
                dashboardMoney(group.baseAmount, group.currency),
                `${group.employeeCount} employees`,
              ],
              [
                `Monthly gross salary (${group.currency})`,
                dashboardMoney(group.grossAmount, group.currency),
                'Current monthly salary estimate before payroll adjustments.',
              ],
            ]),
            ...(payrollData.handoff.length === 0
              ? [
                  [
                    'Prepared handoff component groups',
                    '0',
                    'No prepared handoff components for active employees this month.',
                  ],
                ]
              : payrollData.handoff.map((group) => [
                  `Handoff components (${group.currency}, ${dashboardStatusLabel(group.state)})`,
                  dashboardMoney(group.signedComponentTotal, group.currency),
                  `${group.batchCount} batches; ${group.lockedBatchCount} locked; ${group.employeeCount} employees; ${group.lineCount} lines. Signed adjustment components, separate from base salary.`,
                ])),
          ]
        : []),
      [],
      [
        'Request list scope',
        'Up to 10 requests per available queue.',
        `Leaves cover calendar year ${year}; other approval queues are not included.`,
      ],
      ['Employee', 'Request type', 'Details', 'Status', 'Date'],
      ...dynamicActivities.map((a) => [a.employeeName, a.type, a.details, a.status, a.date]),
    ]);
    const url = URL.createObjectURL(new Blob([csvContent], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute(
      'download',
      `hr_summary_${attendanceData?.businessDate ?? payrollData?.businessDate ?? businessDate()}.csv`,
    );
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };

  return (
    <div className="dash-v3-container">
      {/* 1. Header Bar with Welcome & Export Actions */}
      <div className="dash-v3-header">
        <div className="dash-v3-title-wrap">
          <h1 className="dash-v3-title">Welcome back, {displayName}</h1>
          <p className="dash-v3-subtitle">Cosmix Engineering HR operations & enterprise modules.</p>
        </div>

        <div className="dash-v3-actions">
          <button
            type="button"
            className="dash-v3-btn-export"
            disabled={refreshing}
            onClick={() => {
              for (const { query, allowed } of dashboardReads) if (allowed) void query.refetch();
            }}
          >
            <RefreshCw size={14} aria-hidden="true" />
            <span>{refreshing ? 'Refreshing…' : 'Refresh'}</span>
          </button>
          <button type="button" className="dash-v3-btn-export" onClick={handleExportCSV}>
            <FileSpreadsheet size={14} color="#16a34a" />
            <span>CSV Summary</span>
          </button>
          <button type="button" className="dash-v3-btn-export" onClick={() => window.print()}>
            <FileText size={14} color="#dc2626" />
            <span>Print / PDF</span>
          </button>
        </div>
      </div>

      {/* Counts come only from successful, authorized API responses. */}
      <div className="dash-v3-kpis-grid">
        <Link
          to="/hr/employees"
          className="dash-v3-kpi-card"
          style={{ textDecoration: 'none', color: 'inherit' }}
        >
          <div className="dash-v3-kpi-left">
            <span className="dash-v3-kpi-title">EMPLOYEE DIRECTORY</span>
            <div className="dash-v3-kpi-value">
              {employees.label}{' '}
              {employees.status === 'ready' && <span className="sub">Records</span>}
            </div>
            <div className="dash-v3-kpi-sub">
              Approved onboarding: {approved.label} • Drafts: {drafts.label}
            </div>
            <div className="dash-v3-kpi-sub">Includes drafts; excludes archived records.</div>
          </div>
          <div className="dash-v3-kpi-icon-wrap bg-blue">
            <Users size={18} />
          </div>
        </Link>

        <Link
          to="/hr/attendance"
          className="dash-v3-kpi-card"
          style={{ textDecoration: 'none', color: 'inherit' }}
        >
          <div className="dash-v3-kpi-left">
            <span className="dash-v3-kpi-title">TODAY'S TURNOUT</span>
            <div className="dash-v3-kpi-value">{attendanceLabel}</div>
            <div className="dash-v3-kpi-sub">{attendanceDetail}</div>
            {attendanceData && (
              <>
                <div className="dash-v3-kpi-sub">
                  Open: {attendanceData.openSessionEmployees} • Closed:{' '}
                  {attendanceData.closedSessionOnlyEmployees}
                </div>
                <div className="dash-v3-kpi-sub">
                  Awaiting classification: {attendanceData.notYetClassifiedEmployees}
                </div>
              </>
            )}
          </div>
          <div className="dash-v3-kpi-icon-wrap bg-green">
            <UserCheck size={18} />
          </div>
        </Link>

        <Link
          to="/hr/approvals"
          className="dash-v3-kpi-card"
          style={{ textDecoration: 'none', color: 'inherit' }}
        >
          <div className="dash-v3-kpi-left">
            <span className="dash-v3-kpi-title color-orange">PENDING APPROVALS</span>
            <div className="dash-v3-kpi-value val-orange">{approvalsLabel}</div>
            <div className="dash-v3-kpi-sub">{approvalsDetail}</div>
            <div className="dash-v3-kpi-sub">Hiring and leave queues only.</div>
          </div>
          <div className="dash-v3-kpi-icon-wrap bg-orange">
            <ClipboardList size={18} />
          </div>
        </Link>

        <div className="dash-v3-kpi-card">
          <div className="dash-v3-kpi-left">
            <span className="dash-v3-kpi-title">MONTHLY SALARY ESTIMATE</span>
            <div className="dash-v3-kpi-value dash-v3-salary-value">{payrollLabel}</div>
            <div className="dash-v3-kpi-sub">{payrollDetail}</div>
            {payrollData && (
              <>
                <div className="dash-v3-kpi-sub">
                  Missing salary: {payrollData.missingSalaryEmployeeCount} • Non-monthly:{' '}
                  {payrollData.nonMonthlySalaryEmployeeCount}
                </div>
                <div className="dash-v3-kpi-sub">
                  Future versions excluded: {payrollData.futureSalaryVersions}
                </div>
              </>
            )}
          </div>
          <div className="dash-v3-kpi-icon-wrap bg-purple">
            <Wallet size={18} />
          </div>
        </div>
      </div>

      <ReadNotice label="Employee directory" state={employees} query={employeesQuery} />
      <ReadNotice label="Approved onboarding" state={approved} query={approvedEmployeesQuery} />
      <ReadNotice label="Draft onboarding" state={drafts} query={draftEmployeesQuery} />
      <ReadNotice label="Site directory" state={sites} query={sitesQuery} />
      <ReadNotice label="Hiring queue" state={hiring} query={submissionsQuery} />
      <ReadNotice label={`Leave queue (${year})`} state={leaves} query={pendingLeavesQuery} />
      <ReadNotice label="Attendance summary" state={attendance} query={attendanceQuery} />
      <ReadNotice label="Salary and payroll summary" state={payroll} query={payrollQuery} />

      {(attendanceData || payrollData) && (
        <div className="dash-v3-source-grid">
          {attendanceData && (
            <section className="dash-v3-source-card" aria-label="Attendance source details">
              <h2>Today's attendance evidence</h2>
              <p className="dash-v3-source-meta">
                {attendanceData.businessDate} • Asia/Karachi • Updated{' '}
                {labelDate(attendanceData.generatedAtUtc)}
              </p>
              <p className="dash-v3-source-note">
                Population: {attendanceData.activeEmployees} verified active employees.
              </p>
              <dl className="dash-v3-source-counts">
                <div>
                  <dt>No accepted session evidence</dt>
                  <dd>{attendanceData.noAcceptedSessionEvidenceEmployees}</dd>
                </div>
                <div>
                  <dt>Classified employees</dt>
                  <dd>{attendanceData.classifiedEmployees}</dd>
                </div>
                <div>
                  <dt>Current saved summaries</dt>
                  <dd>{attendanceData.currentSavedSummaryEmployees}</dd>
                </div>
                <div>
                  <dt>Unsaved or stale summaries</dt>
                  <dd>{attendanceData.unsavedOrStaleSummaryEmployees}</dd>
                </div>
              </dl>
              {Object.values(attendanceData.classifiedStatusCounts).some((count) => count > 0) && (
                <div className="dash-v3-source-tags">
                  {Object.entries(attendanceData.classifiedStatusCounts)
                    .filter(([, count]) => count > 0)
                    .map(([status, count]) => (
                      <span key={status}>
                        {dashboardStatusLabel(status)}: <strong>{count}</strong>
                      </span>
                    ))}
                </div>
              )}
              <p className="dash-v3-source-note">
                A missing session is not an absence classification. Turnout counts accepted session
                evidence for active employees.
              </p>
            </section>
          )}
          {payrollData && (
            <section className="dash-v3-source-card" aria-label="Salary and payroll source details">
              <h2>Salary &amp; payroll sources</h2>
              <p className="dash-v3-source-meta">
                Month {payrollData.periodStart.slice(0, 7)} • Effective {payrollData.businessDate} •
                Updated {labelDate(payrollData.generatedAtUtc)}
              </p>
              <p className="dash-v3-source-note">
                Population: {payrollData.activeEmployees} verified active employees.
              </p>
              {payrollData.effectiveMonthlySalary.map((group) => (
                <div className="dash-v3-salary-source" key={group.currency}>
                  <strong>{dashboardMoney(group.grossAmount, group.currency)} monthly gross</strong>
                  <span>
                    {group.employeeCount} employees • Base{' '}
                    {dashboardMoney(group.baseAmount, group.currency)}
                  </span>
                </div>
              ))}
              <p className="dash-v3-source-note">
                {payrollData.missingSalaryEmployeeCount > 0
                  ? `${payrollData.missingSalaryEmployeeCount} active employees need an effective salary record. `
                  : 'Effective salary coverage checked for all active employees. '}
                {payrollData.nonMonthlySalaryEmployeeCount} non-monthly records excluded from
                monthly totals. Future versions: {payrollData.futureSalaryVersions}; pending
                changes: {payrollData.pendingCompensationChangeCount}, both excluded.
              </p>
              <h3>Prepared handoff components</h3>
              {payrollData.handoff.length === 0 ? (
                <p className="dash-v3-source-note">
                  No prepared handoff components for active employees this month.
                </p>
              ) : (
                <ul className="dash-v3-handoff-list">
                  {payrollData.handoff.map((group) => (
                    <li key={`${group.currency}:${group.state}`}>
                      <strong>
                        {dashboardMoney(group.signedComponentTotal, group.currency)} ·{' '}
                        {dashboardStatusLabel(group.state)}
                      </strong>
                      <span>
                        {group.batchCount} batches • {group.lockedBatchCount} locked •{' '}
                        {group.employeeCount} employees • {group.lineCount} lines
                      </span>
                    </li>
                  ))}
                </ul>
              )}
              <p className="dash-v3-source-note">
                Monthly gross is a salary estimate before payroll adjustments. Handoff amounts are
                signed adjustment components, separate from base salary.
              </p>
              <Link to="/hr/payroll-handoff" className="dash-v3-link-more">
                Review payroll handoff <ArrowUpRight size={13} aria-hidden="true" />
              </Link>
            </section>
          )}
        </div>
      )}

      {/* 3. 8 Enterprise Module Quick Launch Cards Grid (4 columns x 2 rows) */}
      <div className="dash-v3-modules-grid">
        {/* Module 1: Employee Master */}
        <Link to="/hr/employees" className="dash-v3-module-card">
          <div className="dash-v3-module-icon" style={{ background: '#eff6ff', color: '#2563eb' }}>
            <Users size={17} />
          </div>
          <div>
            <h3 className="dash-v3-module-title">1. Employee Master</h3>
            <p className="dash-v3-module-desc">
              Directory, Onboarding, Profiles, Credentials & Roles.
            </p>
          </div>
        </Link>

        {/* Module 2: Organization Structure */}
        <Link to="/hr/organization" className="dash-v3-module-card">
          <div className="dash-v3-module-icon" style={{ background: '#f8fafc', color: '#475569' }}>
            <Building2 size={17} />
          </div>
          <div>
            <h3 className="dash-v3-module-title">2. Organization Structure</h3>
            <p className="dash-v3-module-desc">
              Departments, Designations, Branches, Categories & Holidays.
            </p>
          </div>
        </Link>

        {/* Module 3: Working Sites & Geofences */}
        <Link to="/hr/sites" className="dash-v3-module-card">
          <div className="dash-v3-module-icon" style={{ background: '#ecfdf5', color: '#047857' }}>
            <MapPin size={17} />
          </div>
          <div>
            <h3 className="dash-v3-module-title">3. Working Sites & Geofences</h3>
            <p className="dash-v3-module-desc">
              Project sites, GPS coordinates & operational work zones.
            </p>
          </div>
        </Link>

        {/* Module 4: Attendance Tracking */}
        <Link to="/hr/attendance" className="dash-v3-module-card">
          <div className="dash-v3-module-icon" style={{ background: '#f0fdf4', color: '#16a34a' }}>
            <Clock size={17} />
          </div>
          <div>
            <h3 className="dash-v3-module-title">4. Attendance & Punches</h3>
            <p className="dash-v3-module-desc">
              Biometrics, Mobile face verification, GPS radius & exceptions.
            </p>
          </div>
        </Link>

        {/* Module 5: Shift Management */}
        <Link to="/hr/attendance/shifts" className="dash-v3-module-card">
          <div className="dash-v3-module-icon" style={{ background: '#fdf4ff', color: '#c026d3' }}>
            <CalendarDays size={17} />
          </div>
          <div>
            <h3 className="dash-v3-module-title">5. Shift Management</h3>
            <p className="dash-v3-module-desc">
              Office & site shifts, schedules, night shifts & rosters.
            </p>
          </div>
        </Link>

        {/* Module 6: Leave Management */}
        <Link to="/hr/leaves" className="dash-v3-module-card">
          <div className="dash-v3-module-icon" style={{ background: '#faf5ff', color: '#9333ea' }}>
            <Calendar size={17} />
          </div>
          <div>
            <h3 className="dash-v3-module-title">6. Leave & Absence</h3>
            <p className="dash-v3-module-desc">
              Leave balances, requests, medical sick leaves & approvals.
            </p>
          </div>
        </Link>

        {/* Module 7: Overtime & Allowances */}
        <Link to="/hr/workspaces/meals-and-outstation-travel" className="dash-v3-module-card">
          <div className="dash-v3-module-icon" style={{ background: '#eff6ff', color: '#1d4ed8' }}>
            <Banknote size={17} />
          </div>
          <div>
            <h3 className="dash-v3-module-title">7. Overtime & Allowances</h3>
            <p className="dash-v3-module-desc">
              Continuous 13h/12h duty meal allowances & travel days.
            </p>
          </div>
        </Link>

        {/* Module 8: Claims & Advances */}
        <Link to="/hr/requests" className="dash-v3-module-card">
          <div className="dash-v3-module-icon" style={{ background: '#fff7ed', color: '#ea580c' }}>
            <Coins size={17} />
          </div>
          <div>
            <h3 className="dash-v3-module-title">8. Claims & Advances</h3>
            <p className="dash-v3-module-desc">
              Motorcycle fuel (28 km/L), tuning claims & advances.
            </p>
          </div>
        </Link>
      </div>

      {/* 4. Bottom Activity & Approvals Section */}
      <div className="dash-v3-section-head">
        <span className="dash-v3-section-title">PENDING HIRING &amp; LEAVE REQUESTS</span>
        <Link to="/hr/approvals" className="dash-v3-link-more">
          <span>Hiring queue</span>
          <ArrowUpRight size={13} />
        </Link>
      </div>

      <div className="dash-v3-activity-card">
        <p className="dash-v3-notice">
          Up to 10 requests per available queue. Leaves cover calendar year {year}; other approval
          queues are not included.
        </p>
        <table className="dash-v3-table">
          <thead>
            <tr>
              <th>Employee / Team</th>
              <th>Request Type</th>
              <th>Details & Evidence</th>
              <th>Date & Time</th>
              <th>Status</th>
              <th style={{ textAlign: 'right' }}>Action</th>
            </tr>
          </thead>
          <tbody>
            {dynamicActivities.length === 0 && (
              <tr>
                <td colSpan={6}>
                  {hiring.status === 'ready' && leaves.status === 'ready'
                    ? 'No pending hiring or leave requests in the displayed ranges.'
                    : 'No requests to display from the available queue results. Check the queue status above.'}
                </td>
              </tr>
            )}
            {dynamicActivities.map((act) => (
              <tr key={act.id}>
                <td>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <div
                      style={{
                        width: '26px',
                        height: '26px',
                        borderRadius: '50%',
                        backgroundColor: '#eff6ff',
                        color: '#2563eb',
                        fontWeight: 700,
                        fontSize: '10px',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                    >
                      {act.avatarText}
                    </div>
                    <strong style={{ color: '#0f172a' }}>{act.employeeName}</strong>
                  </div>
                </td>
                <td>
                  <span style={{ fontWeight: 600, color: '#334155' }}>{act.type}</span>
                </td>
                <td>
                  <span style={{ color: '#64748b' }}>{act.details}</span>
                </td>
                <td>
                  <div
                    style={{ display: 'flex', alignItems: 'center', gap: '4px', color: '#94a3b8' }}
                  >
                    <Clock3 size={12} />
                    <span>{act.date}</span>
                  </div>
                </td>
                <td>
                  <span
                    style={{
                      padding: '2px 8px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                      backgroundColor: '#ffedd5',
                      color: '#c2410c',
                    }}
                  >
                    {act.status}
                  </span>
                </td>
                <td style={{ textAlign: 'right' }}>
                  <button
                    type="button"
                    onClick={() => void navigate(act.actionUrl || '/hr/approvals')}
                    style={{
                      padding: '4px 10px',
                      borderRadius: '5px',
                      border: '1px solid #cbd5e1',
                      background: '#ffffff',
                      fontSize: '11px',
                      fontWeight: 600,
                      color: '#334155',
                      cursor: 'pointer',
                    }}
                  >
                    Review
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
