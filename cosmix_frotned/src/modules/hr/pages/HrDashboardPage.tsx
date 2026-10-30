import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Users,
  ClipboardList,
  Clock,
  Calendar,
  Coins,
  FileText,
  FileSpreadsheet,
  ArrowUpRight,
  Clock3,
  MapPin,
  CalendarDays,
  Banknote,
  RefreshCw,
  Network,
  ChevronRight,
  Shield,
  Inbox,
  ArrowRight,
} from 'lucide-react';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { pageOf, employeeSchema } from '../schemas/contracts';
import { siteSchema } from '../schemas/sites';
import { submissionSummarySchema } from '../schemas/approvals';
import { isSystemAdmin } from '@/auth/contracts';
import { attendanceOperationalNavigation } from '@/modules/attendance';
import { useAttendanceAccess } from '@/modules/attendance/hooks/use-attendance-access';
import { useAttendanceQuery } from '@/modules/attendance/hooks/use-attendance-query';
import { leavePage } from '@/modules/attendance/operations/leave-policy/contracts';
import { dashboardCount, dashboardCsv, dashboardMoney, dashboardRead } from '../utils/dashboard';
import { dashboardAttendanceSummary, dashboardPayrollSummary } from '../schemas/dashboard';
import { businessDate } from '@/utils/format';
import { canDeployment } from '../outstation/contracts';
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

function formatActivityDate(val?: string | null) {
  if (!val) return 'In progress';
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    const dateStr = d.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      timeZone: 'Asia/Karachi',
    });
    const timeStr = d.toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      timeZone: 'Asia/Karachi',
    });
    return `${dateStr}, ${timeStr}`;
  } catch {
    return val;
  }
}

export function HrDashboardPage() {
  const navigate = useNavigate();
  const access = useHrAccess();
  const attendanceAccess = useAttendanceAccess();

  const [activeTab, setActiveTab] = useState<'hiring' | 'leave'>('hiring');

  const isAdmin = isSystemAdmin(access.user);
  const canReadEmployees = access.can('hr.employees.read');
  const canReadSites = access.can('hr.sites.read');
  const canReadHiring =
    isAdmin && access.can('hr.employees.read', 'hr.employees.submissions.review');
  const canReadLeaves = isAdmin && attendanceAccess.can('attendance.leaves.review');
  const overtimeApprovalLink = attendanceOperationalNavigation(access.user).find(
    (link) => access.ready && link.allowed && link.to === '/hr/overtime/review',
  );
  const canReadAllowances =
    access.ready &&
    (access.can('attendance.allowances.review') ||
      access.can('attendance.allowances.policy') ||
      canDeployment(access.user, 'read'));
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

  const formattedToday = new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date());

  const employeesQuery = useHrQuery('employees?page=1&pageSize=1', pageOf(employeeSchema), [
    'hr.employees.read',
  ]);

  const approvedEmployeesQuery = useHrQuery(
    'employees?status=2&pageSize=1',
    pageOf(employeeSchema),
    ['hr.employees.read'],
  );

  const draftEmployeesQuery = useHrQuery('employees?status=0&pageSize=1', pageOf(employeeSchema), [
    'hr.employees.read',
  ]);
  const recentDraftsQuery = useHrQuery(
    'employees?status=0&page=1&pageSize=5',
    pageOf(employeeSchema),
    ['hr.employees.read'],
  );
  const sitesQuery = useHrQuery('sites?page=1&pageSize=1', pageOf(siteSchema), ['hr.sites.read']);

  const submissionsQuery = useHrQuery(
    'employees/submissions?page=1&pageSize=10&status=1',
    pageOf(submissionSummarySchema),
    ['hr.employees.read', 'hr.employees.submissions.review'],
    canReadHiring,
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

  const totalEmployeesCount = employees.label;
  const approvedCount = approved.label;
  const draftsCount = drafts.label;
  const hiringCount = hiring.label;
  const onboardingLabel = canReadHiring
    ? hiring.count !== undefined && drafts.count !== undefined
      ? String(hiring.count + drafts.count)
      : 'Unavailable'
    : drafts.label;
  const payrollLabel = payrollData
    ? payrollData.effectiveMonthlySalary.length
      ? payrollData.effectiveMonthlySalary
          .map((group) => dashboardMoney(group.grossAmount, group.currency))
          .join(' / ')
      : 'No effective monthly salaries'
    : payroll.label;
  const approvedPercent =
    employees.count && approved.count !== undefined
      ? Math.min(100, (approved.count / employees.count) * 100)
      : 0;
  const draftsPercent =
    employees.count && drafts.count !== undefined
      ? Math.min(100, (drafts.count / employees.count) * 100)
      : 0;

  const dashboardReads = [
    { query: employeesQuery, allowed: canReadEmployees },
    { query: approvedEmployeesQuery, allowed: canReadEmployees },
    { query: draftEmployeesQuery, allowed: canReadEmployees },
    { query: recentDraftsQuery, allowed: canReadEmployees },
    { query: sitesQuery, allowed: canReadSites },
    { query: submissionsQuery, allowed: canReadHiring },
    { query: pendingLeavesQuery, allowed: canReadLeaves },
    { query: attendanceQuery, allowed: canReadAttendance },
    { query: payrollQuery, allowed: canReadPayroll },
  ];
  const refreshing = dashboardReads.some(({ query, allowed }) => allowed && query.isFetching);

  const dynamicActivities: ActivityItem[] = useMemo(() => {
    const list: ActivityItem[] = [];

    if (activeTab === 'hiring') {
      if (submissionsQuery.data?.items?.length) {
        for (const sub of submissionsQuery.data.items) {
          const empName = sub.name || `Employee #${sub.employeeNumber}`;
          const initials = sub.name
            ? sub.name
                .split(' ')
                .filter(Boolean)
                .map((n) => n[0])
                .join('')
                .slice(0, 2)
                .toUpperCase()
            : `#${sub.employeeNumber}`;
          list.push({
            id: `sub-${sub.id}`,
            employeeName: empName,
            avatarText: initials,
            type: 'Hiring approval',
            details: `Employee #${sub.employeeNumber} • Submitted for review`,
            date: sub.submittedAt ? formatActivityDate(sub.submittedAt) : 'Pending review',
            status: 'Pending',
            actionUrl: '/hr/approvals',
          });
        }
      }

      if (recentDraftsQuery.data?.items?.length) {
        for (const emp of recentDraftsQuery.data.items) {
          const empName = emp.name || `Employee #${emp.employeeNumber}`;
          const initials = emp.name
            ? emp.name
                .split(' ')
                .filter(Boolean)
                .map((n) => n[0])
                .join('')
                .slice(0, 2)
                .toUpperCase()
            : `#${emp.employeeNumber}`;
          list.push({
            id: `draft-${emp.id}`,
            employeeName: empName,
            avatarText: initials,
            type: 'Onboarding Draft',
            details: `Employee #${emp.employeeNumber} • Document completion pending`,
            date: emp.createdAt ? formatActivityDate(emp.createdAt) : 'In progress',
            status: 'Pending',
            actionUrl: `/hr/employees/${emp.id}`,
          });
        }
      }
    } else if (activeTab === 'leave') {
      if (pendingLeavesQuery.data?.items?.length) {
        for (const lr of pendingLeavesQuery.data.items) {
          const cancellationPending = lr.cancellations?.some((request) => request.state === 0);
          list.push({
            id: `leave-${lr.id}`,
            employeeName: `Employee #${('employeeNumber' in lr ? lr.employeeNumber : undefined) ?? (lr.employeeId ? lr.employeeId.slice(0, 8) : 'Staff')}`,
            avatarText: 'LV',
            type: `${lr.typeName} Request`,
            details: `${lr.requestedMinutes} mins • ${lr.startDate} to ${lr.endDate}`,
            date: lr.startDate || `${year}`,
            status: cancellationPending ? 'Cancellation pending' : 'Pending',
            actionUrl: `/hr/attendance/leaves/${lr.id}`,
          });
        }
      }
    }

    return list;
  }, [activeTab, submissionsQuery.data, recentDraftsQuery.data, pendingLeavesQuery.data, year]);

  const handleExportCSV = () => {
    const csvContent = dashboardCsv([
      ['Metric', 'Value', 'Details'],
      [
        'Employee directory',
        String(totalEmployeesCount),
        'Unarchived directory records; includes drafts.',
      ],
      ['Approved onboarding', String(approvedCount), 'Onboarding status.'],
      ['Draft onboarding', String(draftsCount), 'Draft onboarding records.'],
      ['Site directory', sites.label, 'Site records.'],
      [
        "Today's turnout",
        attendanceData ? String(attendanceData.withAcceptedSessionEvidence) : attendance.label,
        attendanceData
          ? `Accepted attendance session evidence; ${attendanceData.unavailableSummaryEmployees} unavailable employee summaries excluded (${attendanceData.coverage}).`
          : 'Attendance evidence unavailable; no turnout inferred.',
      ],
      ['Pending approvals', hiringCount, 'Hiring review queue'],
      [
        'Monthly salary summary',
        payrollLabel,
        'Effective salaries grouped by currency; not payment or settlement',
      ],
      [],
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
    <div className="dash-exact-page">
      {/* 1. Header Bar matching Screenshot */}
      <div className="dash-exact-header">
        <div className="dash-exact-title-area">
          <h1 className="dash-exact-main-title">HR dashboard</h1>
          <p className="dash-exact-subtitle">People, attendance and workforce operations</p>
        </div>

        <div className="dash-exact-actions">
          <span className="dash-exact-date-text">{formattedToday}</span>
          <button
            type="button"
            className="dash-exact-btn"
            disabled={refreshing}
            onClick={() => {
              for (const { query, allowed } of dashboardReads) if (allowed) void query.refetch();
            }}
          >
            <RefreshCw size={11} className={refreshing ? 'animate-spin' : ''} />
            <span>{refreshing ? 'Refreshing…' : 'Refresh'}</span>
          </button>
          <button type="button" className="dash-exact-btn" onClick={handleExportCSV}>
            <FileSpreadsheet size={11} color="#16a34a" />
            <span>CSV Summary</span>
          </button>
          <button type="button" className="dash-exact-btn" onClick={() => window.print()}>
            <FileText size={11} color="#dc2626" />
            <span>Print / PDF</span>
          </button>
        </div>
      </div>

      {/* 2. 4 Top KPI Cards matching Screenshot */}
      <div className="dash-exact-kpi-grid">
        {/* KPI Card 1: Employee Directory (Dark Navy) */}
        <Link to="/hr/employees" className="dash-exact-kpi-card is-navy">
          <div className="dash-exact-kpi-info">
            <span className="dash-exact-kpi-label">EMPLOYEE DIRECTORY</span>
            <div className="dash-exact-kpi-value-row">
              <span className="dash-exact-kpi-value-lg">{totalEmployeesCount}</span>
              <span className="dash-exact-kpi-value-unit">records</span>
            </div>
            <span className="dash-exact-kpi-meta">
              {approvedCount} approved • {draftsCount} drafts
            </span>
          </div>
          <div className="dash-exact-kpi-icon-box is-navy-icon">
            <Users size={14} />
          </div>
        </Link>

        {/* KPI Card 2: Today's Turnout */}
        <Link to="/hr/attendance" className="dash-exact-kpi-card">
          <div className="dash-exact-kpi-info">
            <span className="dash-exact-kpi-label">TODAY'S TURNOUT</span>
            <div className="dash-exact-kpi-value-row">
              <span className="dash-exact-kpi-value-lg">
                {attendanceData ? attendanceData.withAcceptedSessionEvidence : '—'}
              </span>
              <span className="dash-exact-kpi-value-unit muted">recorded check-ins</span>
            </div>
            <span className="dash-exact-kpi-meta">
              {attendanceData
                ? `${attendanceData.openSessionEmployees} on shift • ${attendanceData.activeEmployees} active total`
                : attendance.status === 'loading'
                  ? 'Loading attendance…'
                  : 'Attendance data unavailable'}
            </span>
          </div>
          <div className="dash-exact-kpi-icon-box is-blue-icon">
            <Clock size={14} />
          </div>
        </Link>

        {/* KPI Card 3: Pending Approvals */}
        <Link to="/hr/approvals" className="dash-exact-kpi-card">
          <div className="dash-exact-kpi-info">
            <div className="dash-exact-kpi-label-row">
              <span className="dash-exact-kpi-label">PENDING APPROVALS</span>
              <span className="dash-exact-badge-peach">Live queue</span>
            </div>
            <div className="dash-exact-kpi-value-row">
              <span className="dash-exact-kpi-value-lg">{onboardingLabel}</span>
              <span className="dash-exact-kpi-value-unit muted">in queue</span>
            </div>
            <span className="dash-exact-kpi-meta">
              {draftsCount} draft onboarding • {hiring.label} submissions
            </span>
          </div>
          <div className="dash-exact-kpi-icon-box is-peach-icon">
            <ClipboardList size={14} />
          </div>
        </Link>

        {/* KPI Card 4: Monthly Salary Estimate */}
        <Link to="/hr/payroll-handoff" className="dash-exact-kpi-card">
          <div className="dash-exact-kpi-info">
            <span className="dash-exact-kpi-label">MONTHLY SALARY ESTIMATE</span>
            <div className="dash-exact-kpi-value-row">
              <span className="dash-exact-kpi-value-md" title={payrollLabel}>
                {payrollLabel}
              </span>
            </div>
            <span className="dash-exact-kpi-meta">
              {approvedCount} approved headcount in active cycle
            </span>
          </div>
          <div className="dash-exact-kpi-icon-box is-blue-icon">
            <Banknote size={14} />
          </div>
        </Link>
      </div>

      {/* 3. Alert Banner */}
      <div className="dash-exact-alert-banner">
        <Shield size={13} className="dash-exact-alert-icon" />
        <div className="dash-exact-alert-text">
          <strong>Directory summary:</strong> {totalEmployeesCount} directory records (
          {approvedCount} approved onboarding, {draftsCount} in onboarding queue). Attendance
          coverage and salary availability are reported separately.
          <span className="dash-exact-attendance-note" role="status">
            {attendanceData
              ? attendanceData.unavailableSummaryEmployees > 0
                ? `Attendance: ${attendanceData.unavailableSummaryEmployees} employee summaries need HR review and are excluded from the counts above.`
                : 'Attendance: no unavailable employee summaries were reported.'
              : attendance.status === 'loading'
                ? 'Loading attendance coverage…'
                : 'Attendance coverage is unavailable. No attendance totals have been inferred.'}
          </span>
        </div>
      </div>

      {/* 4. HR Workspace */}
      <div className="dash-exact-workspace-section">
        <h2 className="dash-exact-section-title">HR workspace</h2>
        <div className="dash-exact-workspace-grid">
          {/* Card 1: Employee Master */}
          <Link to="/hr/employees" className="dash-exact-workspace-card">
            <div className="dash-exact-card-left">
              <div className="dash-exact-card-icon-wrap">
                <Users size={13} />
              </div>
              <div className="dash-exact-card-text">
                <span className="dash-exact-card-title">Employee Master</span>
                <span className="dash-exact-card-sub">Directory, onboarding & profiles</span>
              </div>
            </div>
            <ChevronRight size={12} className="dash-exact-card-arrow" />
          </Link>

          {/* Card 2: Organization Structure */}
          <Link to="/hr/organization" className="dash-exact-workspace-card">
            <div className="dash-exact-card-left">
              <div className="dash-exact-card-icon-wrap">
                <Network size={13} />
              </div>
              <div className="dash-exact-card-text">
                <span className="dash-exact-card-title">Organization Structure</span>
                <span className="dash-exact-card-sub">Departments, branches & designations</span>
              </div>
            </div>
            <ChevronRight size={12} className="dash-exact-card-arrow" />
          </Link>

          {/* Card 3: Working Sites & Geofences */}
          <Link to="/hr/sites" className="dash-exact-workspace-card">
            <div className="dash-exact-card-left">
              <div className="dash-exact-card-icon-wrap">
                <MapPin size={13} />
              </div>
              <div className="dash-exact-card-text">
                <span className="dash-exact-card-title">Working Sites & Geofences</span>
                <span className="dash-exact-card-sub">Project sites & operational zones</span>
              </div>
            </div>
            <ChevronRight size={12} className="dash-exact-card-arrow" />
          </Link>

          {/* Card 4: Attendance & Punches */}
          <Link to="/hr/attendance" className="dash-exact-workspace-card">
            <div className="dash-exact-card-left">
              <div className="dash-exact-card-icon-wrap">
                <Clock size={13} />
              </div>
              <div className="dash-exact-card-text">
                <span className="dash-exact-card-title">Attendance & Punches</span>
                <span className="dash-exact-card-sub">Records, verification & exceptions</span>
              </div>
            </div>
            <ChevronRight size={12} className="dash-exact-card-arrow" />
          </Link>

          {/* Card 5: Shift Management */}
          <Link to="/hr/attendance/shifts" className="dash-exact-workspace-card">
            <div className="dash-exact-card-left">
              <div className="dash-exact-card-icon-wrap">
                <CalendarDays size={13} />
              </div>
              <div className="dash-exact-card-text">
                <span className="dash-exact-card-title">Shift Management</span>
                <span className="dash-exact-card-sub">Shifts, schedules & rosters</span>
              </div>
            </div>
            <ChevronRight size={12} className="dash-exact-card-arrow" />
          </Link>

          {/* Card 6: Leave & Absence */}
          <Link to="/hr/leaves" className="dash-exact-workspace-card">
            <div className="dash-exact-card-left">
              <div className="dash-exact-card-icon-wrap">
                <Calendar size={13} />
              </div>
              <div className="dash-exact-card-text">
                <span className="dash-exact-card-title">Leave & Absence</span>
                <span className="dash-exact-card-sub">Balances, requests & approvals</span>
              </div>
            </div>
            <ChevronRight size={12} className="dash-exact-card-arrow" />
          </Link>

          {overtimeApprovalLink && (
            <Link to={overtimeApprovalLink.to} className="dash-exact-workspace-card">
              <div className="dash-exact-card-left">
                <div className="dash-exact-card-icon-wrap">
                  <ClipboardList size={13} />
                </div>
                <div className="dash-exact-card-text">
                  <span className="dash-exact-card-title">{overtimeApprovalLink.label}</span>
                  <span className="dash-exact-card-sub">Review overtime requests & decisions</span>
                </div>
              </div>
              <ChevronRight size={12} className="dash-exact-card-arrow" />
            </Link>
          )}

          {canReadAllowances && (
            <Link to="/hr/allowances" className="dash-exact-workspace-card">
              <div className="dash-exact-card-left">
                <div className="dash-exact-card-icon-wrap">
                  <Banknote size={13} />
                </div>
                <div className="dash-exact-card-text">
                  <span className="dash-exact-card-title">Allowances</span>
                  <span className="dash-exact-card-sub">
                    Claims, policies & outstation management
                  </span>
                </div>
              </div>
              <ChevronRight size={12} className="dash-exact-card-arrow" />
            </Link>
          )}

          {/* Card 8: Claims & Advances */}
          <Link to="/hr/advances-loans" className="dash-exact-workspace-card">
            <div className="dash-exact-card-left">
              <div className="dash-exact-card-icon-wrap">
                <Coins size={13} />
              </div>
              <div className="dash-exact-card-text">
                <span className="dash-exact-card-title">Claims & Advances</span>
                <span className="dash-exact-card-sub">Employee claims & advances</span>
              </div>
            </div>
            <ChevronRight size={12} className="dash-exact-card-arrow" />
          </Link>
        </div>
      </div>

      {/* 5. Bottom Split Section matching Screenshot */}
      <div className="dash-exact-bottom-split">
        {/* Left Column: Pending hiring & leave requests */}
        <div className="dash-exact-requests-card">
          <div className="dash-exact-card-header">
            <span className="dash-exact-card-heading">Pending hiring & leave requests</span>
            <Link to="/hr/approvals" className="dash-exact-card-top-link">
              <span>Hiring queue</span>
              <ArrowUpRight size={13} />
            </Link>
          </div>

          {/* Tabs */}
          <div className="dash-exact-tabs-bar">
            <button
              type="button"
              className={`dash-exact-tab-btn ${activeTab === 'hiring' ? 'active' : ''}`}
              onClick={() => setActiveTab('hiring')}
            >
              <span>Hiring & Onboarding</span>
              <span className="dash-exact-tab-count">{onboardingLabel}</span>
            </button>
            <button
              type="button"
              className={`dash-exact-tab-btn ${activeTab === 'leave' ? 'active' : ''}`}
              onClick={() => setActiveTab('leave')}
            >
              <span>Leave {year}</span>
              <span className="dash-exact-tab-count">{leaves.label}</span>
            </button>
          </div>

          {/* Table Container */}
          <div className="dash-exact-table-wrapper">
            {/* Table Header */}
            <div className="dash-exact-table-head-row">
              <div className="dash-col col-emp">Employee / Team</div>
              <div className="dash-col col-type">Request type</div>
              <div className="dash-col col-details">Details & evidence</div>
              <div className="dash-col col-date">Date & time</div>
              <div className="dash-col col-status">Status</div>
              <div className="dash-col col-action">Action</div>
            </div>

            {/* Empty State or Activity Rows */}
            {dynamicActivities.length === 0 ? (
              <div className="dash-exact-empty-state">
                <div className="dash-exact-empty-icon">
                  <Inbox size={34} strokeWidth={1.5} />
                </div>
                <h3 className="dash-exact-empty-title">
                  {activeTab === 'hiring'
                    ? drafts.status === 'ready' && (!canReadHiring || hiring.status === 'ready')
                      ? 'No records returned'
                      : 'Hiring records unavailable'
                    : leaves.status === 'ready'
                      ? 'No pending leave requests'
                      : leaves.label}
                </h3>
                <p className="dash-exact-empty-sub">
                  {activeTab === 'hiring'
                    ? 'Only records available to your account appear here. Refresh to check again.'
                    : 'The queue only shows requests within your permitted review scope and date range.'}
                </p>
                <button
                  type="button"
                  className="dash-exact-empty-btn"
                  onClick={() =>
                    void navigate(activeTab === 'hiring' ? '/hr/employees/new' : '/hr/leaves')
                  }
                >
                  <span>{activeTab === 'hiring' ? 'Add New Employee' : 'Open Leave Desk'}</span>
                  <ArrowUpRight size={13} />
                </button>
              </div>
            ) : (
              <div className="dash-exact-table-body">
                {dynamicActivities.map((act) => (
                  <div key={act.id} className="dash-exact-table-row">
                    <div className="dash-col col-emp">
                      <div className="dash-emp-pill">
                        <div className="dash-emp-avatar">{act.avatarText}</div>
                        <span className="dash-emp-name">{act.employeeName}</span>
                      </div>
                    </div>
                    <div className="dash-col col-type">
                      <span className="dash-type-text">{act.type}</span>
                    </div>
                    <div className="dash-col col-details">
                      <span className="dash-details-text">{act.details}</span>
                    </div>
                    <div className="dash-col col-date">
                      <span className="dash-date-text">
                        <Clock3 size={11} style={{ marginRight: '4px', verticalAlign: '-1px' }} />
                        {act.date}
                      </span>
                    </div>
                    <div className="dash-col col-status">
                      <span className="dash-status-pill">{act.status}</span>
                    </div>
                    <div className="dash-col col-action">
                      <button
                        type="button"
                        className="dash-action-btn"
                        onClick={() => void navigate(act.actionUrl || '/hr/approvals')}
                      >
                        Review
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Footer */}
          <div className="dash-exact-card-footer">
            <span>
              {activeTab === 'hiring'
                ? `${onboardingLabel} onboarding & hiring records`
                : `${leaves.label} leave requests for ${year}`}
            </span>
          </div>
        </div>

        {/* Right Column: Onboarding summary */}
        <div className="dash-exact-onboarding-card">
          <div className="dash-exact-card-header">
            <span className="dash-exact-card-heading">Onboarding summary</span>
          </div>

          <div className="dash-exact-onboarding-content">
            <div className="dash-exact-onboarding-stat">
              <span className="dash-exact-onboarding-num">{totalEmployeesCount}</span>
              <span className="dash-exact-onboarding-stat-unit">directory records</span>
            </div>

            {/* Split Progress Bar */}
            <div className="dash-exact-progress-track">
              <div
                className="dash-exact-progress-fill-approved"
                style={{ width: `${approvedPercent}%` }}
                title={`Approved: ${approvedCount}`}
              />
              <div
                className="dash-exact-progress-fill-drafts"
                style={{ width: `${draftsPercent}%` }}
                title={`Drafts: ${draftsCount}`}
              />
            </div>

            {/* Legend */}
            <div className="dash-exact-legend-list">
              <div className="dash-exact-legend-item">
                <div className="dash-exact-legend-left">
                  <span className="dash-exact-dot is-navy" />
                  <span className="dash-exact-legend-text">Approved onboarding</span>
                </div>
                <span className="dash-exact-legend-val">{approvedCount}</span>
              </div>

              <div className="dash-exact-legend-item">
                <div className="dash-exact-legend-left">
                  <span className="dash-exact-dot is-draft" />
                  <span className="dash-exact-legend-text">Drafts</span>
                </div>
                <span className="dash-exact-legend-val">{draftsCount}</span>
              </div>
            </div>

            <p className="dash-exact-onboarding-note">
              Includes drafts; excludes archived records.
            </p>
          </div>

          <div className="dash-exact-onboarding-footer">
            <Link to="/hr/employees" className="dash-exact-link-dir">
              <span>View employee directory</span>
              <ArrowRight size={13} />
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
