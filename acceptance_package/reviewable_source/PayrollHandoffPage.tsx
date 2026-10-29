import { useState, useMemo, type FormEvent } from 'react';
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
    ...(access.can('attendance.payroll-salary.read') ? [{ id: 'sources', label: 'Salary sources and rules' }] : []),
    ...(access.can('attendance.payroll-salary.request') ? [{ id: 'import', label: 'Import salary sheet' }] : []),
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
              Payroll Handoff
            </h1>
            <p>
              Project immutable payroll handoff lines from attendance periods, freeze batches, and
              generate Bank Advice files for Accounts processing.
            </p>
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
          canLock={access.can(permissions.lock)}
          canProcess={access.can(permissions.process)}
          canReview={access.can('attendance.payroll-handoff.hr-review')}
          accountId={access.user?.account?.accountId ?? ''}
          employeeMap={employeeMap}
        />
      )}
      {active === 'sources' && <SalarySources />}
      {active === 'import' && <SalaryImport />}
    </div>
  );
}

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
  const [reason, setReason] = useState(
    `Payroll Settlement for Attendance Period ${String(lastMonth).padStart(2, '0')}/${lastMonthYear}`,
  );

  const action = useFinanceAction();

  const preview = useFinanceQuery(
    `/payroll-handoff/preview${queryString({ year: Number(loaded.year), month: Number(loaded.month) })}`,
    payrollPreview,
    true,
    true,
  );

  const activePreview = !preview.isError ? preview.data : undefined;

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
    if (loaded.year === targetYear && loaded.month === targetMonth) void preview.refetch();
    else setLoaded({ year: targetYear, month: targetMonth });
    setReason(
      `Payroll Settlement for Attendance Period ${targetMonth.padStart(2, '0')}/${targetYear}`,
    );
  }

  async function prepare(event: FormEvent) {
    event.preventDefault();
    if (!activePreview || preview.isFetching || !activePreview.lines.length) return;

    const result = await action.run(
      '/payroll-handoff/batches',
      {
        year: Number(loaded.year),
        month: Number(loaded.month),
        expectedPreviewFingerprint: activePreview.previewFingerprint,
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
            <label>Year</label>
            <input
              type="number"
              min={2020}
              max={2099}
              value={year}
              onChange={(e) => setYear(e.target.value)}
            />
          </div>
          <div className="payroll-month-selector">
            <label>Month (1-12)</label>
            <input
              type="number"
              min={1}
              max={12}
              value={month}
              onChange={(e) => setMonth(e.target.value)}
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

      <FinanceNotice>
        Preview requires a locked attendance month. Bank advice is available from a locked batch and
        contains employee totals; bank account numbers are not included.
      </FinanceNotice>
      <QueryState query={preview}>
        {activePreview && (
          <>
            <FinanceNotice>{activePreview.calculationVersion === 'salary-v2' && activePreview.salaryComplete ? 'Complete salary projection from approved sources.' : 'Adjustment projection; this is not a complete salary run.'}</FinanceNotice>
            {activePreview.employeeTotals && <SalaryPayslips batchId="" currency={activePreview.currency} employees={activePreview.employeeTotals} available={false} />}
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
                    {money(activePreview.totals.attendanceDeductionTotal, activePreview.currency)}
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
                  style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}
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
                        <th style={{ textAlign: 'right' }}>Amount ({activePreview.currency})</th>
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
                            <td className={`amount-col ${isNegative ? 'negative' : 'positive'}`}>
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
                  style={{ marginTop: '24px', paddingTop: '20px', borderTop: '1px solid #f1f5f9' }}
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
                    immutable payroll batch for Accounts review. Preparation does not approve or pay
                    this batch.
                  </p>
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
      <ActionMessage action={action} />
    </div>
  );
}

function PayrollBatches({
  canLock,
  canProcess,
  canReview,
  accountId,
  employeeMap,
}: {
  canLock: boolean;
  canProcess: boolean;
  canReview: boolean;
  accountId: string;
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
    if (downloading || batch.state !== 'locked') return;
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
    if (!selectedBatch || !canLockBatch(selectedBatch)) return;
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
    if (
      !selectedBatch ||
      !canProcessBatch(selectedBatch)
    )
      return;
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
  function canLockBatch(batch: PayrollBatch) {
    return !isPreparer(batch) && (batch.calculationVersion === 'salary-v2' ? batch.state === 'hr_approved' && batch.hrApprovedByUserId !== accountId && batch.salaryComplete === true : batch.state === 'prepared');
  }
  function canProcessBatch(batch: PayrollBatch) {
    return batch.state === 'locked' && (batch.calculationVersion === 'salary-v2' ? batch.lockedByUserId !== accountId && batch.hrApprovedByUserId !== accountId && !isPreparer(batch) : batch.lockedByUserId === accountId);
  }
  async function reviewBatch(decision: number) {
    if (!selectedBatch || isPreparer(selectedBatch)) return;
    const result = await action.run(`/payroll-handoff/batches/${selectedBatch.id}/hr-review`, { expectedVersion: selectedBatch.version, decision, reason: reviewReason }, 'POST', payrollBatchDetail);
    if (result) { setReviewReason(''); void detailQuery.refetch(); void batchesQuery.refetch(); }
  }
  async function cancelBatch() {
    if (!selectedBatch || !isPreparer(selectedBatch)) return;
    const result = await action.run(`/payroll-handoff/batches/${selectedBatch.id}/cancel`, { expectedVersion: selectedBatch.version, reason: reviewReason }, 'POST', payrollBatchDetail);
    if (result) { setReviewReason(''); void detailQuery.refetch(); void batchesQuery.refetch(); }
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
                                  downloading || batch.state !== 'locked' || detailQuery.isFetching
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
                            {batch.calculationVersion === 'salary-v2' && <>
                              <SalaryPayslips batchId={batch.id} currency={batch.currency} employees={batch.employeeTotals ?? []} available={batch.salaryComplete === true && ['locked', 'finance_handed_off'].includes(batch.state)} />
                              {batch.state === 'prepared' && <div><TextAreaField label="HR review / cancellation reason" value={reviewReason} onChange={setReviewReason} required />{canReview && !isPreparer(batch) && <><Button disabled={action.pending || !reviewReason.trim() || !batch.salaryComplete} onClick={() => void reviewBatch(1)}>HR approve salary</Button><Button variant="secondary" disabled={action.pending || !reviewReason.trim()} onClick={() => void reviewBatch(3)}>Reject salary run</Button></>}{isPreparer(batch) && <Button variant="secondary" disabled={action.pending || !reviewReason.trim()} onClick={() => void cancelBatch()}>Cancel unapproved run</Button>}</div>}
                            </>}
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
                                      onChange={(event) =>
                                        setProcessingReference(event.target.value)
                                      }
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
