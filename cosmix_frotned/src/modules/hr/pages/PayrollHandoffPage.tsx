import { useState, useMemo, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { z } from 'zod';
import {
  Banknote,
  Calendar,
  Coins,
  TrendingUp,
  ArrowDownCircle,
  Users,
  FileSpreadsheet,
  RefreshCw,
  FileCheck2,
  Download,
  Search,
  ShieldCheck,
  Building,
  Lock,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { Denied, ActionMessage } from '../components/Feedback';
import { FinanceQueryState as QueryState } from '../components/finance/FinanceQueryState';
import {
  FinanceNotice,
  FinanceStatus,
  FinanceTabs,
  HasMorePager,
  TextAreaField,
} from '../components/finance/FinanceChrome';
import { money } from '../components/finance/labels';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../hooks/use-finance-queries';
import { useHrQuery } from '../hooks/use-hr-queries';
import { pageOf, employeeSchema } from '../schemas/contracts';
import { queryString } from '../api/hr-finance-api';
import { labelDate } from '../utils/messages';
import {
  payrollBatchDetail,
  payrollPage,
  payrollPreview,
  type PayrollPreviewLine,
  type PayrollBatch,
} from '../schemas/finance';
import { request } from '@/services/api/http-client';
import { useHrLifetime } from '../hooks/use-hr-lifetime';
import { financeError } from '../api/finance-messages';
import { ApiError } from '@/services/api/api-error';
import '@/styles/payroll-handoff.css';
import { SalarySources } from '../components/finance/SalarySources';
import { SalaryImport } from '../components/finance/SalaryImport';
import { SalaryPayslips } from '../components/finance/SalaryPayslips';

const permissions = {
  read: 'attendance.payroll-handoff.read',
  prepare: 'attendance.payroll-handoff.prepare',
  lock: 'attendance.payroll-handoff.lock',
  process: 'attendance.payroll-handoff.process',
};

export function PayrollHandoffPage() {
  const access = useFinanceAccess();
  const tabs = [
    ...(access.can(permissions.read) ? [{ id: 'preview', label: 'Preview and prepare' }] : []),
    ...(access.can(permissions.read) ? [{ id: 'batches', label: 'Batches' }] : []),
    ...(access.can('attendance.payroll-salary.read')
      ? [{ id: 'sources', label: 'Salary sources and rules' }]
      : []),
    ...([
      'attendance.payroll-imports.read',
      'attendance.payroll-salary.read',
      'hr.employees.compensation.read',
    ].every((permission) => access.can(permission))
      ? [{ id: 'import', label: 'Import salary sheet' }]
      : []),
  ];
  const [tab, setTab] = useState(tabs[0]?.id ?? '');

  // Pre-load employee directory for UUID -> Name / Initials resolution
  const employeesQuery = useHrQuery(
    'employees' + queryString({ page: 1, pageSize: 100 }),
    pageOf(employeeSchema),
    ['hr.employees.read'],
  );

  const employeeMap = useMemo(() => {
    const map = new Map<string, { name: string; code: string; initials: string }>();

    // Overlay real database employees
    for (const emp of employeesQuery.data?.items ?? []) {
      const name = emp.name || 'Employee';
      const initials = name
        .replace(/^Engr\.\s+/, '')
        .split(' ')
        .filter(Boolean)
        .map((w: string) => w[0]?.toUpperCase() ?? '')
        .slice(0, 2)
        .join('');
      const code = emp.identityNumber || `EMP-${emp.employeeNumber ?? ''}`;
      map.set(emp.id.toLowerCase(), {
        name,
        code,
        initials: initials || 'EM',
      });
    }
    return map;
  }, [employeesQuery.data?.items]);

  if (!tabs.length) return <Denied />;
  const active = tabs.some((item) => item.id === tab) ? tab : (tabs[0]?.id ?? '');

  return (
    <div className="payroll-handoff-container">
      {/* Header Banner */}
      <header className="payroll-header-banner">
        <div className="payroll-header-top">
          <div className="payroll-title-group">
            <h1>
              <Banknote size={24} style={{ color: '#2563eb' }} />
              Payroll Handoff &amp; Financial Settlement
            </h1>
            <p>
              Project immutable payroll handoff lines from attendance periods, freeze batches, and
              generate Bank Advice files for Accounts processing.
            </p>
          </div>
        </div>

        {/* 4-Step Payroll Lifecycle Stepper Bar */}
        <div className="payroll-pipeline-stepper">
          <Link
            to="/hr/attendance/month-close"
            className="payroll-step-item"
            title="Lock the monthly attendance register"
          >
            <span className="payroll-step-num">1</span>
            <span>Attendance Month Close</span>
          </Link>
          <span className="payroll-step-arrow">→</span>

          <div
            className={`payroll-step-item ${active === 'preview' ? 'active' : ''}`}
            onClick={() => setTab('preview')}
            role="button"
            tabIndex={0}
            style={{ cursor: 'pointer' }}
          >
            <span className="payroll-step-num">2</span>
            <span>Preview &amp; Calculate Lines</span>
          </div>
          <span className="payroll-step-arrow">→</span>

          <div
            className={`payroll-step-item ${active === 'batches' ? 'active' : ''}`}
            onClick={() => setTab('batches')}
            role="button"
            tabIndex={0}
            style={{ cursor: 'pointer' }}
          >
            <span className="payroll-step-num">3</span>
            <span>Freeze &amp; Review Batches</span>
          </div>
          <span className="payroll-step-arrow">→</span>

          <div
            className="payroll-step-item"
            title="Download Bank Advice CSV for Accounts Settlement"
          >
            <span className="payroll-step-num">4</span>
            <span>Bank Advice &amp; Accounts Settlement</span>
          </div>
        </div>

        <FinanceTabs tabs={tabs} active={active} onChange={setTab} label="Payroll handoff views" />
      </header>

      {active === 'preview' && (
        <PayrollPreviewPanel
          canPrepare={access.can(permissions.prepare)}
          employeeMap={employeeMap}
          onBatchCreated={() => setTab('batches')}
        />
      )}
      {active === 'batches' && (
        <PayrollBatches
          canPrepare={access.can(permissions.prepare)}
          canLock={access.can(permissions.lock)}
          canProcess={access.can(permissions.process)}
          canReview={access.can('attendance.payroll-handoff.hr-review')}
          accountId={access.user?.account?.accountId ?? ''}
          employeeId={access.employeeId}
          employeeMap={employeeMap}
        />
      )}
      {active === 'sources' && <SalarySources />}
      {active === 'import' && <SalaryImport />}
    </div>
  );
}

const MONTH_OPTIONS = [
  { value: '1', label: '01 - January' },
  { value: '2', label: '02 - February' },
  { value: '3', label: '03 - March' },
  { value: '4', label: '04 - April' },
  { value: '5', label: '05 - May' },
  { value: '6', label: '06 - June' },
  { value: '7', label: '07 - July' },
  { value: '8', label: '08 - August' },
  { value: '9', label: '09 - September' },
  { value: '10', label: '10 - October' },
  { value: '11', label: '11 - November' },
  { value: '12', label: '12 - December' },
];

function PayrollPreviewPanel({
  canPrepare,
  employeeMap,
  onBatchCreated,
}: {
  canPrepare: boolean;
  employeeMap: Map<string, { name: string; code: string; initials: string }>;
  onBatchCreated: () => void;
}) {
  const now = new Date();
  const currentYear = now.getFullYear();
  const currentMonth = now.getMonth() + 1;
  const lastMonth = currentMonth === 1 ? 12 : currentMonth - 1;
  const lastMonthYear = currentMonth === 1 ? currentYear - 1 : currentYear;

  const [year, setYear] = useState(String(lastMonthYear));
  const [month, setMonth] = useState(String(lastMonth));
  const [loaded, setLoaded] = useState<{ year: string; month: string }>({
    year: String(lastMonthYear),
    month: String(lastMonth),
  });

  const [searchFilter, setSearchFilter] = useState('');
  const [componentFilter, setComponentFilter] = useState('all');
  const [groupMode, setGroupMode] = useState(false);
  const [selectedEmployees, setSelectedEmployees] = useState<string[]>([]);
  const [reason, setReason] = useState(
    `Payroll Settlement for Attendance Period ${String(lastMonth).padStart(2, '0')}/${lastMonthYear}`,
  );

  const action = useFinanceAction();
  const access = useFinanceAccess();
  const canCheckMonth = access.can('attendance.month-locks.read');
  const [previewRequested, setPreviewRequested] = useState(false);
  const periodStart = `${loaded.year.padStart(4, '0')}-${loaded.month.padStart(2, '0')}-01`;
  const monthStatus = useFinanceQuery(
    `/attendance/month-locks/${Number(loaded.year)}/${Number(loaded.month)}/preview`,
    z.object({
      monthStart: z.literal(periodStart),
      state: z.enum(['ready', 'not_ready', 'locked']),
    }),
    canCheckMonth,
    canCheckMonth,
  );
  const monthLocked =
    monthStatus.isSuccess && !monthStatus.isFetching && monthStatus.data.state === 'locked';

  const preview = useFinanceQuery(
    `/payroll-handoff/preview${queryString({ year: Number(loaded.year), month: Number(loaded.month) })}`,
    payrollPreview,
    true,
    canCheckMonth ? monthLocked : previewRequested,
  );

  const isMonthNotLocked =
    (canCheckMonth && monthStatus.isSuccess && monthStatus.data.state !== 'locked') ||
    (preview.isError &&
      preview.error instanceof ApiError &&
      preview.error.code === 'payroll_handoff_month_not_locked');

  const activePreview =
    !preview.isError && (!canCheckMonth || monthLocked) ? preview.data : undefined;
  const groupEmployees = activePreview?.employeeTotals ?? [];
  const selectionValid =
    !groupMode ||
    (selectedEmployees.length > 0 &&
      selectedEmployees.every((id) =>
        groupEmployees.some((employee) => employee.employeeId === id),
      ));

  // Filter lines by search and component
  const filteredLines = useMemo(() => {
    return (activePreview?.lines ?? []).filter((line: PayrollPreviewLine) => {
      const emp = employeeMap.get(line.employeeId.toLowerCase());
      const name = emp?.name || '';
      const code = emp?.code || '';

      if (searchFilter.trim()) {
        const q = searchFilter.toLowerCase();
        if (
          !name.toLowerCase().includes(q) &&
          !code.toLowerCase().includes(q) &&
          !line.component.toLowerCase().includes(q)
        ) {
          return false;
        }
      }

      if (componentFilter !== 'all') {
        const comp = line.component.toLowerCase();
        if (componentFilter === 'overtime' && !comp.includes('overtime')) return false;
        if (componentFilter === 'allowance' && !comp.includes('allowance')) return false;
        if (componentFilter === 'deduction' && !comp.includes('deduction')) return false;
      }

      return true;
    });
  }, [activePreview?.lines, employeeMap, searchFilter, componentFilter]);

  function handleSelectMonth(targetYear: string, targetMonth: string) {
    setSelectedEmployees([]);
    setYear(targetYear);
    setMonth(targetMonth);
    if (
      !Number.isInteger(Number(targetYear)) ||
      Number(targetYear) < 1 ||
      Number(targetYear) > 9998 ||
      !Number.isInteger(Number(targetMonth)) ||
      Number(targetMonth) < 1 ||
      Number(targetMonth) > 12
    )
      return;
    if (loaded.year === targetYear && loaded.month === targetMonth) {
      if (canCheckMonth) {
        void monthStatus.refetch().then((result) => {
          if (result.isSuccess && result.data?.state === 'locked') void preview.refetch();
        });
      } else if (previewRequested) void preview.refetch();
      else setPreviewRequested(true);
    } else setLoaded({ year: targetYear, month: targetMonth });
    setPreviewRequested(true);
    setReason(
      `Payroll Settlement for Attendance Period ${targetMonth.padStart(2, '0')}/${targetYear}`,
    );
  }

  async function prepare(event: FormEvent) {
    event.preventDefault();
    if (!activePreview || preview.isFetching || !activePreview.lines.length || !selectionValid)
      return;

    const result = await action.run(
      '/payroll-handoff/batches',
      {
        year: Number(loaded.year),
        month: Number(loaded.month),
        expectedPreviewFingerprint: activePreview.previewFingerprint,
        ...(groupMode ? { employeeIds: selectedEmployees } : {}),
        reason,
      },
      'POST',
      payrollBatchDetail,
    );
    if (result) onBatchCreated();
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Month Selector Toolbar */}
      <section className="payroll-section-card">
        <div className="payroll-section-header">
          <h2>
            <Calendar size={18} style={{ color: '#2563eb' }} />
            Select Attendance Period
          </h2>
          <div className="payroll-quick-btns">
            <button
              type="button"
              className={`payroll-quick-btn ${year === String(lastMonthYear) && month === String(lastMonth) ? 'active' : ''}`}
              onClick={() => handleSelectMonth(String(lastMonthYear), String(lastMonth))}
            >
              Previous Month ({String(lastMonth).padStart(2, '0')}/{lastMonthYear})
            </button>
            <button
              type="button"
              className={`payroll-quick-btn ${year === String(currentYear) && month === String(currentMonth) ? 'active' : ''}`}
              onClick={() => handleSelectMonth(String(currentYear), String(currentMonth))}
            >
              Current Month ({String(currentMonth).padStart(2, '0')}/{currentYear})
            </button>
          </div>
        </div>

        <div className="payroll-controls-bar">
          <div className="payroll-month-selector">
            <label>Month</label>
            <select
              value={month}
              onChange={(e) => setMonth(e.target.value)}
              style={{ minWidth: '130px' }}
            >
              {MONTH_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
          <div className="payroll-month-selector">
            <label>Year</label>
            <input
              type="number"
              min={2020}
              max={2099}
              value={year}
              onChange={(e) => setYear(e.target.value)}
            />
          </div>
          <Button
            variant="secondary"
            disabled={!year || !month || preview.isFetching}
            onClick={() => handleSelectMonth(year, month)}
          >
            {preview.isFetching ? <RefreshCw size={14} className="animate-spin" /> : 'Load Preview'}
          </Button>
        </div>
      </section>

      {canCheckMonth && (monthStatus.isPending || monthStatus.isError) ? (
        <QueryState query={monthStatus} title="Attendance month status unavailable">
          {null}
        </QueryState>
      ) : !canCheckMonth && !previewRequested ? (
        <FinanceNotice>
          Select a period and load its preview. Payroll handoff requires a finalized attendance
          month.
        </FinanceNotice>
      ) : isMonthNotLocked ? (
        <div className="payroll-guidance-card">
          <div className="payroll-guidance-icon">
            <Lock size={18} />
          </div>
          <div className="payroll-guidance-content">
            <h3>
              Attendance Month {String(loaded.month).padStart(2, '0')}/{loaded.year} Not Locked Yet
            </h3>
            <p>
              In Cosmix ERP, payroll handoff requires an immutable attendance snapshot. Month{' '}
              <strong>
                {MONTH_OPTIONS.find((m) => m.value === loaded.month)?.label.split(' - ')[1] ??
                  loaded.month}{' '}
                {loaded.year}
              </strong>{' '}
              has not been closed and finalized in the attendance register yet. Please lock the
              month in Attendance Management before calculating payroll lines.
            </p>
            <div className="payroll-guidance-actions">
              <Link to="/hr/attendance/month-close" className="btn-guidance-primary">
                <Lock size={13} /> Go to Month Close &amp; Lock Period
              </Link>
              <Link to="/hr/attendance/tracking" className="btn-guidance-secondary">
                <Users size={13} /> Inspect Attendance Register
              </Link>
              <button type="button" className="btn-guidance-secondary" onClick={onBatchCreated}>
                <FileSpreadsheet size={13} /> View Already Frozen Batches
              </button>
            </div>
          </div>
        </div>
      ) : (
        <>
          <FinanceNotice>
            Preview requires a locked attendance month. Bank advice is available from a locked batch
            and contains employee totals; bank account numbers are not included.
          </FinanceNotice>
          <QueryState query={preview}>
            {activePreview && (
              <>
                <FinanceNotice>
                  {activePreview.calculationVersion === 'salary-v2' && activePreview.salaryComplete
                    ? 'Complete salary projection from approved sources.'
                    : 'Adjustment projection; this is not a complete salary run.'}
                </FinanceNotice>
                {activePreview.employeeTotals && (
                  <SalaryPayslips
                    batchId=""
                    currency={activePreview.currency}
                    employees={activePreview.employeeTotals}
                    available={false}
                  />
                )}
                {/* KPI Summary Cards */}
                <div className="payroll-kpi-grid">
                  <div className="payroll-kpi-card">
                    <div className="payroll-kpi-icon net">
                      <Banknote size={22} />
                    </div>
                    <div className="payroll-kpi-info">
                      <span className="payroll-kpi-label">Projected Net Hand-off</span>
                      <span className="payroll-kpi-value" style={{ color: '#2563eb' }}>
                        {money(activePreview.totals.netAmount, activePreview.currency)}
                      </span>
                      <span className="payroll-kpi-sub">
                        {activePreview.lines.length} total entries
                      </span>
                    </div>
                  </div>

                  <div className="payroll-kpi-card">
                    <div className="payroll-kpi-icon ot">
                      <TrendingUp size={22} />
                    </div>
                    <div className="payroll-kpi-info">
                      <span className="payroll-kpi-label">Approved Overtime</span>
                      <span className="payroll-kpi-value" style={{ color: '#059669' }}>
                        {money(activePreview.totals.overtimeTotal, activePreview.currency)}
                      </span>
                      <span className="payroll-kpi-sub">Verified duty calculations</span>
                    </div>
                  </div>

                  <div className="payroll-kpi-card">
                    <div className="payroll-kpi-icon alw">
                      <Coins size={22} />
                    </div>
                    <div className="payroll-kpi-info">
                      <span className="payroll-kpi-label">Approved Allowances</span>
                      <span className="payroll-kpi-value" style={{ color: '#16a34a' }}>
                        {money(activePreview.totals.allowanceTotal, activePreview.currency)}
                      </span>
                      <span className="payroll-kpi-sub">Meal, outstation, per-diem</span>
                    </div>
                  </div>

                  <div className="payroll-kpi-card">
                    <div className="payroll-kpi-icon ded">
                      <ArrowDownCircle size={22} />
                    </div>
                    <div className="payroll-kpi-info">
                      <span className="payroll-kpi-label">Attendance Deductions</span>
                      <span className="payroll-kpi-value" style={{ color: '#e11d48' }}>
                        {money(
                          activePreview.totals.attendanceDeductionTotal,
                          activePreview.currency,
                        )}
                      </span>
                      <span className="payroll-kpi-sub">Late & absence adjustments</span>
                    </div>
                  </div>

                  <div className="payroll-kpi-card">
                    <div className="payroll-kpi-icon emp">
                      <Users size={22} />
                    </div>
                    <div className="payroll-kpi-info">
                      <span className="payroll-kpi-label">Eligible Staff</span>
                      <span className="payroll-kpi-value">
                        {activePreview.employeeCount} Active Staff
                      </span>
                      <span className="payroll-kpi-sub">Attendance ledger records</span>
                    </div>
                  </div>
                </div>

                {/* Table of Projected Lines */}
                <section className="payroll-section-card">
                  <div className="payroll-section-header">
                    <div>
                      <h2>
                        <FileCheck2 size={18} style={{ color: '#059669' }} />
                        Projected Lines ({filteredLines.length} of {activePreview.lines.length})
                      </h2>
                      <span style={{ fontSize: '12px', color: '#64748b' }}>
                        Period: {activePreview.periodStart} to {activePreview.periodEndExclusive}
                      </span>
                    </div>

                    {/* Search & Filter Toolbar */}
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '10px',
                        flexWrap: 'wrap',
                      }}
                    >
                      <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                        <Search
                          size={14}
                          style={{ position: 'absolute', left: '10px', color: '#94a3b8' }}
                        />
                        <input
                          type="text"
                          placeholder="Search staff, code, component..."
                          value={searchFilter}
                          onChange={(e) => setSearchFilter(e.target.value)}
                          style={{
                            padding: '6px 12px 6px 30px',
                            borderRadius: '6px',
                            border: '1px solid #cbd5e1',
                            fontSize: '12px',
                            width: '210px',
                          }}
                        />
                      </div>

                      <select
                        value={componentFilter}
                        onChange={(e) => setComponentFilter(e.target.value)}
                        style={{
                          padding: '6px 10px',
                          borderRadius: '6px',
                          border: '1px solid #cbd5e1',
                          fontSize: '12px',
                          background: '#ffffff',
                          color: '#334155',
                        }}
                      >
                        <option value="all">All Components</option>
                        <option value="overtime">Overtime Only</option>
                        <option value="allowance">Allowances Only</option>
                        <option value="deduction">Deductions Only</option>
                      </select>
                    </div>
                  </div>

                  {!filteredLines.length ? (
                    <FinanceNotice>
                      {activePreview.lines.length
                        ? 'No payroll lines match your filter criteria.'
                        : 'No payroll lines were returned for this locked attendance month.'}
                    </FinanceNotice>
                  ) : (
                    <div className="payroll-table-wrapper">
                      <table className="payroll-table">
                        <thead>
                          <tr>
                            <th>Employee</th>
                            <th>Component</th>
                            <th>Source Reference</th>
                            <th style={{ textAlign: 'right' }}>
                              Amount ({activePreview.currency})
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {filteredLines.map((line: PayrollPreviewLine, idx: number) => {
                            const emp = employeeMap.get(line.employeeId.toLowerCase());
                            const name = emp?.name || 'Employee';
                            const code = emp?.code || line.employeeId.slice(0, 8);
                            const initials =
                              emp?.initials ||
                              name
                                .replace(/^Engr\.\s+/, '')
                                .slice(0, 2)
                                .toUpperCase();

                            const compLower = line.component.toLowerCase();
                            const isNegative = line.amount < 0;

                            return (
                              <tr key={`${line.sourceType}-${line.sourceId}-${idx}`}>
                                <td>
                                  <div className="payroll-emp-cell">
                                    <div className="payroll-emp-avatar">{initials}</div>
                                    <div>
                                      <div className="payroll-emp-name">{name}</div>
                                      <div className="payroll-emp-sub">{code}</div>
                                    </div>
                                  </div>
                                </td>
                                <td>
                                  <span
                                    className={`payroll-comp-badge ${
                                      compLower.includes('overtime')
                                        ? 'overtime'
                                        : compLower.includes('allowance')
                                          ? 'allowance'
                                          : compLower.includes('deduction')
                                            ? 'deduction'
                                            : 'other'
                                    }`}
                                  >
                                    {line.component}
                                  </span>
                                </td>
                                <td style={{ fontSize: '12px', color: '#64748b' }}>
                                  <code>{line.sourceType}</code> &middot; v{line.sourceVersion}
                                </td>
                                <td
                                  className={`amount-col ${isNegative ? 'negative' : 'positive'}`}
                                >
                                  {money(line.amount, line.currency)}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}

                  {/* Prepare and Freeze Batch Form */}
                  {canPrepare && (
                    <form
                      style={{
                        marginTop: '24px',
                        paddingTop: '20px',
                        borderTop: '1px solid #f1f5f9',
                      }}
                      onSubmit={(event) => void prepare(event)}
                    >
                      <h3
                        style={{
                          fontSize: '14px',
                          fontWeight: 700,
                          color: '#0f172a',
                          margin: '0 0 8px',
                        }}
                      >
                        Freeze Batch for Accounts Handoff
                      </h3>
                      <p style={{ fontSize: '12.5px', color: '#64748b', margin: '0 0 16px' }}>
                        Preparing locks this projection digest (Fingerprint:{' '}
                        <code>{activePreview.previewFingerprint.slice(0, 24)}...</code>) into an
                        immutable payroll batch for Accounts review. Preparation does not approve or
                        pay this batch.
                      </p>
                      <fieldset
                        disabled={action.pending}
                        style={{
                          border: '1px solid #cbd5e1',
                          borderRadius: 8,
                          padding: 16,
                          marginBottom: 16,
                        }}
                      >
                        <legend>Employees in this batch</legend>
                        <label>
                          <input
                            type="checkbox"
                            checked={groupMode}
                            onChange={(event) => {
                              setGroupMode(event.target.checked);
                              setSelectedEmployees([]);
                            }}
                          />{' '}
                          Prepare a selected employee group
                        </label>
                        <p>
                          Place approvers’ salaries in a separate group for independent review. An
                          employee can belong to only one active payroll batch for this month. Each
                          group needs its own HR, Management and Finance approvals.
                        </p>
                        {groupMode && (
                          <div
                            style={{ maxHeight: 260, overflowY: 'auto', display: 'grid', gap: 8 }}
                          >
                            {groupEmployees.map((employee) => (
                              <label
                                key={employee.employeeId}
                                style={{ display: 'flex', alignItems: 'center', gap: 8 }}
                              >
                                <input
                                  type="checkbox"
                                  checked={selectedEmployees.includes(employee.employeeId)}
                                  onChange={(event) => {
                                    setSelectedEmployees((current) =>
                                      event.target.checked
                                        ? [...current, employee.employeeId]
                                        : current.filter((id) => id !== employee.employeeId),
                                    );
                                  }}
                                />
                                {employeeMap.get(employee.employeeId.toLowerCase())?.name ??
                                  employee.employeeId}
                                {' — '}
                                {money(employee.netPay, activePreview.currency)}
                              </label>
                            ))}
                          </div>
                        )}
                        <p>
                          {groupMode ? selectedEmployees.length : groupEmployees.length} employees
                          selected. Other employees remain outside this batch and require separate
                          preparation.
                        </p>
                      </fieldset>
                      <TextAreaField
                        label="Preparation Reason / Memo"
                        required
                        value={reason}
                        onChange={setReason}
                        disabled={action.pending}
                      />
                      <div
                        style={{
                          marginTop: '14px',
                          display: 'flex',
                          justifyContent: 'flex-end',
                          gap: '10px',
                        }}
                      >
                        <Button
                          type="submit"
                          disabled={
                            action.pending ||
                            preview.isFetching ||
                            !activePreview.lines.length ||
                            !selectionValid ||
                            !reason.trim()
                          }
                        >
                          {action.pending ? 'Freezing Batch...' : 'Prepare and Freeze Batch'}
                        </Button>
                      </div>
                    </form>
                  )}
                </section>
              </>
            )}
          </QueryState>
        </>
      )}
      <ActionMessage action={action} />
    </div>
  );
}

function PayrollBatches({
  canPrepare,
  canLock,
  canProcess,
  canReview,
  accountId,
  employeeId,
  employeeMap,
}: {
  canPrepare: boolean;
  canLock: boolean;
  canProcess: boolean;
  canReview: boolean;
  accountId: string;
  employeeId: string | null;
  employeeMap: Map<string, { name: string; code: string; initials: string }>;
}) {
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [lockReason, setLockReason] = useState('');
  const [processingReference, setProcessingReference] = useState('');
  const [processingReason, setProcessingReason] = useState('');
  const [reviewReason, setReviewReason] = useState('');
  const action = useFinanceAction();

  const batchesQuery = useFinanceQuery(
    `/payroll-handoff/batches${queryString({ page, pageSize: 25 })}`,
    payrollPage,
    true,
  );

  const detailQuery = useFinanceQuery(
    selectedId ? `/payroll-handoff/batches/${selectedId}` : '/payroll-handoff/batches',
    payrollBatchDetail,
    true,
    Boolean(selectedId),
  );
  const batches = !batchesQuery.isError ? (batchesQuery.data?.items ?? []) : [];
  const selectedBatch =
    !detailQuery.isError && !detailQuery.isFetching ? detailQuery.data?.batch : undefined;
  const lifetime = useHrLifetime();
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  async function downloadAdvice(batch: PayrollBatch) {
    if (downloading || !canDownloadSalary(batch)) return;
    const signal = lifetime();
    setDownloading(true);
    setDownloadError(null);
    try {
      const blob = await request(`/payroll-handoff/batches/${batch.id}/bank-advice`, {
        responseType: 'blob',
        signal,
      });
      signal.throwIfAborted();
      if (!(blob instanceof Blob)) throw new Error('Invalid bank advice response.');
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `bank-advice-${batch.id}.csv`;
      link.rel = 'noopener';
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (failure) {
      if (!signal.aborted) setDownloadError(financeError(failure));
    } finally {
      if (!signal.aborted) setDownloading(false);
    }
  }

  async function lock(event: FormEvent) {
    event.preventDefault();
    if (!canLock || !selectedBatch || !canLockBatch(selectedBatch)) return;
    const result = await action.run(
      `/payroll-handoff/batches/${selectedBatch.id}/lock`,
      { expectedVersion: selectedBatch.version, reason: lockReason },
      'POST',
      payrollBatchDetail,
    );
    if (result) {
      setLockReason('');
      void batchesQuery.refetch();
      void detailQuery.refetch();
    }
  }

  async function process(event: FormEvent) {
    event.preventDefault();
    if (!canProcess || !selectedBatch || !canProcessBatch(selectedBatch)) return;
    const result = await action.run(
      `/payroll-handoff/batches/${selectedBatch.id}/processing`,
      { externalProcessingReference: processingReference, reason: processingReason },
      'POST',
      payrollBatchDetail,
    );
    if (result) {
      setProcessingReference('');
      setProcessingReason('');
      void batchesQuery.refetch();
      void detailQuery.refetch();
    }
  }

  function isPreparer(batch: PayrollBatch) {
    return Boolean(accountId && batch.preparedByUserId === accountId);
  }
  function includesOwnSalary(batch: PayrollBatch) {
    return Boolean(
      employeeId &&
      detailQuery.data?.batch.id === batch.id &&
      detailQuery.data.lines.some((line) => line.employeeId === employeeId),
    );
  }
  function canDownloadSalary(batch: PayrollBatch) {
    return (
      batch.calculationVersion === 'salary-v2' &&
      batch.salaryComplete === true &&
      ['locked', 'finance_handed_off'].includes(batch.state)
    );
  }
  function canLockBatch(batch: PayrollBatch) {
    return (
      !isPreparer(batch) &&
      !includesOwnSalary(batch) &&
      batch.calculationVersion === 'salary-v2' &&
      batch.state === 'hr_approved' &&
      batch.hrApprovedByUserId !== accountId &&
      batch.salaryComplete === true
    );
  }
  function canProcessBatch(batch: PayrollBatch) {
    return (
      batch.state === 'locked' &&
      batch.calculationVersion === 'salary-v2' &&
      batch.salaryComplete === true &&
      batch.lockedByUserId !== accountId &&
      batch.hrApprovedByUserId !== accountId &&
      !isPreparer(batch) &&
      !includesOwnSalary(batch)
    );
  }
  async function reviewBatch(decision: number) {
    if (
      !canReview ||
      !selectedBatch ||
      isPreparer(selectedBatch) ||
      includesOwnSalary(selectedBatch)
    )
      return;
    const result = await action.run(
      `/payroll-handoff/batches/${selectedBatch.id}/hr-review`,
      { expectedVersion: selectedBatch.version, decision, reason: reviewReason },
      'POST',
      payrollBatchDetail,
    );
    if (result) {
      setReviewReason('');
      void detailQuery.refetch();
      void batchesQuery.refetch();
    }
  }
  async function cancelBatch() {
    if (!canPrepare || !selectedBatch || !isPreparer(selectedBatch)) return;
    const result = await action.run(
      `/payroll-handoff/batches/${selectedBatch.id}/cancel`,
      { expectedVersion: selectedBatch.version, reason: reviewReason },
      'POST',
      payrollBatchDetail,
    );
    if (result) {
      setReviewReason('');
      void detailQuery.refetch();
      void batchesQuery.refetch();
    }
  }

  async function returnBatch() {
    if (
      !canReview ||
      !selectedBatch ||
      selectedBatch.state !== 'hr_approved' ||
      isPreparer(selectedBatch)
    )
      return;
    const result = await action.run(
      `/payroll-handoff/batches/${selectedBatch.id}/return-for-correction`,
      { expectedVersion: selectedBatch.version, reason: reviewReason },
      'POST',
      payrollBatchDetail,
    );
    if (result) {
      setReviewReason('');
      void detailQuery.refetch();
      void batchesQuery.refetch();
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      <QueryState query={batchesQuery}>
        <section className="payroll-section-card">
          <div className="payroll-section-header">
            <h2>
              <FileSpreadsheet size={18} style={{ color: '#2563eb' }} />
              Payroll Handoff Batches ({batches.length})
            </h2>
            <span style={{ fontSize: '12px', color: '#64748b' }}>
              Dual-control approval & sealed Bank Advice files
            </span>
          </div>

          {!batches.length ? (
            <FinanceNotice>No payroll batches have been prepared yet.</FinanceNotice>
          ) : (
            <div>
              {batches.map((summary) => {
                const batch = selectedBatch?.id === summary.id ? selectedBatch : summary;
                const isSelected = selectedId === batch.id;
                return (
                  <article key={batch.id} className="payroll-batch-card">
                    <div className="payroll-batch-card-top">
                      <div className="payroll-batch-ref">
                        <Button
                          variant="secondary"
                          onClick={() => setSelectedId(isSelected ? null : batch.id)}
                          style={{ fontWeight: 700 }}
                        >
                          {batch.batchReference}
                        </Button>
                        <FinanceStatus value={batch.state} />
                      </div>

                      <div className="payroll-batch-metrics">
                        <div className="payroll-batch-metric-item">
                          <span className="payroll-batch-metric-label">Period</span>
                          <span className="payroll-batch-metric-val">
                            {batch.periodStart} &rarr; {batch.periodEndExclusive}
                          </span>
                        </div>
                        <div className="payroll-batch-metric-item">
                          <span className="payroll-batch-metric-label">Total Net</span>
                          <span className="payroll-batch-metric-val" style={{ color: '#2563eb' }}>
                            {money(batch.totals.netAmount, batch.currency)}
                          </span>
                        </div>
                        <div className="payroll-batch-metric-item">
                          <span className="payroll-batch-metric-label">Lines</span>
                          <span className="payroll-batch-metric-val">
                            {batch.lineCount} entries
                          </span>
                        </div>
                        <div className="payroll-batch-metric-item">
                          <span className="payroll-batch-metric-label">Prepared</span>
                          <span className="payroll-batch-metric-val">
                            {labelDate(batch.preparedAt)}
                          </span>
                        </div>
                      </div>
                    </div>

                    {/* Expanded Batch Details */}
                    {isSelected && (
                      <QueryState query={detailQuery}>
                        {selectedBatch && (
                          <div
                            style={{
                              marginTop: '16px',
                              paddingTop: '16px',
                              borderTop: '1px solid #f1f5f9',
                            }}
                          >
                            <div
                              style={{
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                                marginBottom: '14px',
                                flexWrap: 'wrap',
                                gap: '10px',
                              }}
                            >
                              <div>
                                <span style={{ fontSize: '12px', color: '#64748b' }}>
                                  Lines revision: <code>{batch.linesRevision}</code>
                                </span>
                              </div>
                              <Button
                                variant="secondary"
                                disabled={
                                  downloading || !canDownloadSalary(batch) || detailQuery.isFetching
                                }
                                onClick={() => void downloadAdvice(batch)}
                              >
                                <Download size={14} />
                                <span style={{ marginLeft: '6px' }}>Download Bank Advice CSV</span>
                              </Button>
                            </div>

                            <Button
                              variant="secondary"
                              disabled={action.pending || detailQuery.isFetching}
                              onClick={() => {
                                void detailQuery.refetch();
                                void batchesQuery.refetch();
                              }}
                            >
                              Refresh batch details
                            </Button>
                            {/* Line Items Table */}
                            {(detailQuery.data?.lines.length ?? 0) > 0 && (
                              <div
                                className="payroll-table-wrapper"
                                style={{ marginBottom: '16px' }}
                              >
                                <table className="payroll-table">
                                  <thead>
                                    <tr>
                                      <th>Employee</th>
                                      <th>Component</th>
                                      <th>Source Reference</th>
                                      <th style={{ textAlign: 'right' }}>
                                        Amount ({batch.currency})
                                      </th>
                                    </tr>
                                  </thead>
                                  <tbody>
                                    {detailQuery.data?.lines.map(
                                      (line: PayrollPreviewLine, idx: number) => {
                                        const emp = employeeMap.get(line.employeeId.toLowerCase());
                                        const name = emp?.name || 'Employee';
                                        const code = emp?.code || line.employeeId.slice(0, 8);
                                        const initials =
                                          emp?.initials ||
                                          name
                                            .replace(/^Engr\.\s+/, '')
                                            .slice(0, 2)
                                            .toUpperCase();
                                        return (
                                          <tr key={`${line.sourceId}-${idx}`}>
                                            <td>
                                              <div className="payroll-emp-cell">
                                                <div className="payroll-emp-avatar">{initials}</div>
                                                <div>
                                                  <div className="payroll-emp-name">{name}</div>
                                                  <div className="payroll-emp-sub">{code}</div>
                                                </div>
                                              </div>
                                            </td>
                                            <td>
                                              <span className="payroll-comp-badge other">
                                                {line.component}
                                              </span>
                                            </td>
                                            <td style={{ fontSize: '12px', color: '#64748b' }}>
                                              <code>{line.sourceType}</code>
                                            </td>
                                            <td
                                              className={`amount-col ${line.amount < 0 ? 'negative' : 'positive'}`}
                                            >
                                              {money(line.amount, line.currency)}
                                            </td>
                                          </tr>
                                        );
                                      },
                                    )}
                                  </tbody>
                                </table>
                              </div>
                            )}

                            {/* Dual-Control Approval & Lock Form */}
                            {batch.calculationVersion === 'salary-v2' && (
                              <>
                                <SalaryPayslips
                                  batchId={batch.id}
                                  currency={batch.currency}
                                  employees={batch.employeeTotals ?? []}
                                  available={canDownloadSalary(batch)}
                                />
                                {batch.state === 'returned' && (
                                  <FinanceNotice>
                                    Returned for correction. Original salary and approval history
                                    are retained. Correct the source records, refresh the month
                                    preview and prepare a new run for fresh approvals.
                                  </FinanceNotice>
                                )}
                                {batch.state === 'hr_approved' &&
                                  canReview &&
                                  !isPreparer(batch) && (
                                    <section aria-label="Return payroll for correction">
                                      <TextAreaField
                                        label="Reason for returning payroll"
                                        value={reviewReason}
                                        onChange={setReviewReason}
                                        required
                                      />
                                      <Button
                                        variant="secondary"
                                        disabled={action.pending || !reviewReason.trim()}
                                        onClick={() => void returnBatch()}
                                      >
                                        Return for correction
                                      </Button>
                                      <p>
                                        The current run will be retired. A new run will require
                                        fresh HR and Management approvals.
                                      </p>
                                    </section>
                                  )}
                                {batch.state === 'prepared' && (
                                  <div>
                                    <TextAreaField
                                      label="HR review / cancellation reason"
                                      value={reviewReason}
                                      onChange={setReviewReason}
                                      required
                                    />
                                    {canReview &&
                                      !isPreparer(batch) &&
                                      !includesOwnSalary(batch) && (
                                        <>
                                          <Button
                                            disabled={
                                              action.pending ||
                                              !reviewReason.trim() ||
                                              !batch.salaryComplete
                                            }
                                            onClick={() => void reviewBatch(1)}
                                          >
                                            HR approve salary
                                          </Button>
                                          <Button
                                            variant="secondary"
                                            disabled={action.pending || !reviewReason.trim()}
                                            onClick={() => void reviewBatch(3)}
                                          >
                                            Reject salary run
                                          </Button>
                                        </>
                                      )}
                                    {canPrepare && isPreparer(batch) && (
                                      <Button
                                        variant="secondary"
                                        disabled={action.pending || !reviewReason.trim()}
                                        onClick={() => void cancelBatch()}
                                      >
                                        Cancel unapproved run
                                      </Button>
                                    )}
                                  </div>
                                )}
                              </>
                            )}
                            {canLock && canLockBatch(batch) && (
                              <form
                                style={{
                                  marginTop: '20px',
                                  padding: '16px',
                                  background: '#f8fafc',
                                  borderRadius: '8px',
                                  border: '1px solid #e2e8f0',
                                }}
                                onSubmit={(event) => void lock(event)}
                              >
                                <h4
                                  style={{
                                    fontSize: '13.5px',
                                    fontWeight: 700,
                                    color: '#0f172a',
                                    margin: '0 0 6px',
                                  }}
                                >
                                  <ShieldCheck
                                    size={16}
                                    style={{
                                      display: 'inline',
                                      color: '#2563eb',
                                      marginRight: '6px',
                                    }}
                                  />
                                  Dual-Control Batch Approval & Lock
                                </h4>
                                <p
                                  style={{ fontSize: '12px', color: '#64748b', margin: '0 0 12px' }}
                                >
                                  {isPreparer(batch)
                                    ? 'Security Rule: The preparer cannot lock their own batch. A separate authorized approver must review and lock.'
                                    : 'Locking permanently seals this batch and transitions claims to InPayrollHandoff state.'}
                                </p>
                                <TextAreaField
                                  label="Lock Approval Reason"
                                  required
                                  value={lockReason}
                                  onChange={setLockReason}
                                  disabled={action.pending || isPreparer(batch)}
                                />
                                <div
                                  style={{
                                    marginTop: '12px',
                                    display: 'flex',
                                    justifyContent: 'flex-end',
                                  }}
                                >
                                  <Button
                                    type="submit"
                                    variant="success"
                                    disabled={
                                      action.pending || isPreparer(batch) || !lockReason.trim()
                                    }
                                  >
                                    {action.pending ? 'Locking...' : 'Approve & Lock Batch'}
                                  </Button>
                                </div>
                              </form>
                            )}

                            {/* Accounts Recording Form */}
                            {canProcess && canProcessBatch(batch) && (
                              <form
                                style={{
                                  marginTop: '20px',
                                  padding: '16px',
                                  background: '#f8fafc',
                                  borderRadius: '8px',
                                  border: '1px solid #e2e8f0',
                                }}
                                onSubmit={(event) => void process(event)}
                              >
                                <h4
                                  style={{
                                    fontSize: '13.5px',
                                    fontWeight: 700,
                                    color: '#0f172a',
                                    margin: '0 0 6px',
                                  }}
                                >
                                  <Building
                                    size={16}
                                    style={{
                                      display: 'inline',
                                      color: '#059669',
                                      marginRight: '6px',
                                    }}
                                  />
                                  Record Accounts Confirmation Evidence
                                </h4>
                                <div
                                  style={{
                                    display: 'flex',
                                    flexDirection: 'column',
                                    gap: '12px',
                                  }}
                                >
                                  <TextInput
                                    label="External Processing Reference"
                                    required
                                    maxLength={150}
                                    value={processingReference}
                                    onChange={(event) => setProcessingReference(event.target.value)}
                                  />
                                  <TextAreaField
                                    label="Processing Reason / Memo"
                                    required
                                    value={processingReason}
                                    onChange={setProcessingReason}
                                    disabled={action.pending}
                                  />
                                </div>
                                <div
                                  style={{
                                    marginTop: '14px',
                                    display: 'flex',
                                    justifyContent: 'flex-end',
                                  }}
                                >
                                  <Button
                                    type="submit"
                                    disabled={
                                      action.pending ||
                                      !processingReference.trim() ||
                                      !processingReason.trim()
                                    }
                                  >
                                    {action.pending ? 'Saving...' : 'Record Processing Evidence'}
                                  </Button>
                                </div>
                              </form>
                            )}
                          </div>
                        )}
                      </QueryState>
                    )}
                  </article>
                );
              })}
            </div>
          )}
          {batchesQuery.data && (page > 1 || batchesQuery.data.hasMore) && (
            <HasMorePager
              page={page}
              hasMore={batchesQuery.data.hasMore}
              busy={batchesQuery.isFetching}
              onPrevious={() => setPage((value) => Math.max(1, value - 1))}
              onNext={() => setPage((value) => value + 1)}
            />
          )}
        </section>
      </QueryState>
      {downloadError && (
        <p className="hr-message hr-message--error" role="alert">
          {downloadError}
        </p>
      )}
      <ActionMessage action={action} />
    </div>
  );
}
