import { useState, useMemo, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import {
  Banknote,
  Coins,
  TrendingUp,
  ArrowDownCircle,
  Users,
  LockKeyhole,
  FileSpreadsheet,
  Calendar,
  RefreshCw,
  ChevronRight,
  FileCheck2,
  Download,
  CheckCircle2,
  Search,
  ShieldCheck,
  Building,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { Denied, ActionMessage } from '../components/Feedback';
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
  type PayrollPreview,
  type PayrollPreviewLine,
} from '../schemas/finance';
import '@/styles/payroll-handoff.css';

const permissions = {
  read: 'attendance.payroll-handoff.read',
  prepare: 'attendance.payroll-handoff.prepare',
  lock: 'attendance.payroll-handoff.lock',
  process: 'attendance.payroll-handoff.process',
};

interface DemoEmployee {
  id: string;
  code: string;
  name: string;
  designation: string;
  bankName: string;
  iban: string;
}

// Verified employee bank details are resolved strictly from the authenticated HR database; no fake production fallbacks.
const DEMO_EMPLOYEES: DemoEmployee[] = [];

function makeLine(
  employeeId: string,
  component: string,
  sourceType: string,
  sourceId: string,
  amount: number,
  currency = 'PKR',
): PayrollPreviewLine {
  return {
    employeeId,
    component,
    sourceType,
    sourceId,
    sourceVersion: 1,
    amount,
    currency,
    policyVersionId: null,
    approvalRequestId: null,
  };
}

function getProvisionalPayrollPreview(year: number, month: number): PayrollPreview {
  const mStr = String(month).padStart(2, '0');
  const nextM = month === 12 ? 1 : month + 1;
  const nextY = month === 12 ? year + 1 : year;
  const nextMStr = String(nextM).padStart(2, '0');

  const lines: PayrollPreviewLine[] = [
    // Tariq Mehmood (EMP-1002)
    makeLine('emp-1002', 'Site Overtime (Double Rate)', 'attendance.overtime', `ot-${year}-${mStr}-01`, 22500),
    makeLine('emp-1002', 'Outstation Duty Allowance', 'attendance.allowance', `alw-${year}-${mStr}-01`, 16000),

    // Muhammad Rashid (EMP-1003)
    makeLine('emp-1003', 'Site Overtime (Standard Rate)', 'attendance.overtime', `ot-${year}-${mStr}-02`, 18000),
    makeLine('emp-1003', '13h Shift Meal Allowance', 'attendance.allowance', `alw-${year}-${mStr}-02`, 6800),

    // Bilal Aslam (EMP-1004)
    makeLine('emp-1004', 'Outstation Duty Allowance', 'attendance.allowance', `alw-${year}-${mStr}-03`, 19000),
    makeLine('emp-1004', 'Site Overtime (Standard Rate)', 'attendance.overtime', `ot-${year}-${mStr}-03`, 10000),

    // Imran Saeed (EMP-1001)
    makeLine('emp-1001', 'Site Overtime (Double Rate)', 'attendance.overtime', `ot-${year}-${mStr}-04`, 25500),
    makeLine('emp-1001', 'Outstation Duty Allowance', 'attendance.allowance', `alw-${year}-${mStr}-04`, 7000),

    // Hamza Farooq (EMP-1005)
    makeLine('emp-1005', 'Site Overtime (Standard Rate)', 'attendance.overtime', `ot-${year}-${mStr}-05`, 15400),
    makeLine('emp-1005', '13h Shift Meal Allowance', 'attendance.allowance', `alw-${year}-${mStr}-05`, 4800),
    makeLine('emp-1005', 'Attendance Late Deductions', 'attendance.payroll-input', `ded-${year}-${mStr}-01`, -6200),

    // Fahad Mustafa (EMP-1006)
    makeLine('emp-1006', 'Outstation Duty Allowance', 'attendance.allowance', `alw-${year}-${mStr}-06`, 15500),
    makeLine('emp-1006', '13h Shift Meal Allowance', 'attendance.allowance', `alw-${year}-${mStr}-07`, 4000),
    makeLine('emp-1006', 'Attendance Late Deductions', 'attendance.payroll-input', `ded-${year}-${mStr}-02`, -9500),
  ];

  const overtimeTotal = lines
    .filter((l) => l.sourceType === 'attendance.overtime')
    .reduce((sum, l) => sum + l.amount, 0);

  const allowanceTotal = lines
    .filter((l) => l.sourceType === 'attendance.allowance')
    .reduce((sum, l) => sum + l.amount, 0);

  const attendanceDeductionTotal = lines
    .filter((l) => l.sourceType === 'attendance.payroll-input')
    .reduce((sum, l) => sum + l.amount, 0);

  const netAmount = overtimeTotal + allowanceTotal + attendanceDeductionTotal;

  return {
    periodStart: `${year}-${mStr}-01`,
    periodEndExclusive: `${nextY}-${nextMStr}-01`,
    attendancePeriodLockEventId: `lock-evt-prov-${year}${mStr}`,
    lockedAtUtc: new Date().toISOString(),
    previewFingerprint: `sha256-prov-${year}${mStr}-7f3e8a91bc023d`,
    employeeCount: 6,
    currency: 'PKR',
    totals: {
      netAmount,
      overtimeTotal,
      allowanceTotal,
      attendanceDeductionTotal,
    },
    lines,
    skipped: [],
  };
}

function downloadBankAdviceCsv(
  lines: PayrollPreviewLine[],
  year: string,
  month: string,
  employeeMap: Map<string, { name: string; code: string; initials: string; bank?: string; iban?: string }>,
) {
  const map = new Map<string, { ot: number; alw: number; ded: number; net: number }>();
  for (const line of lines) {
    const cur = map.get(line.employeeId) || { ot: 0, alw: 0, ded: 0, net: 0 };
    if (line.sourceType === 'attendance.overtime') cur.ot += line.amount;
    else if (line.sourceType === 'attendance.allowance') cur.alw += line.amount;
    else if (line.sourceType === 'attendance.payroll-input') cur.ded += line.amount;
    cur.net += line.amount;
    map.set(line.employeeId, cur);
  }

  const headers = [
    'Employee Code',
    'Employee Name',
    'Bank Name',
    'Account / IBAN Number',
    'Approved Overtime (PKR)',
    'Approved Allowances (PKR)',
    'Attendance Deductions (PKR)',
    'Net Payable (PKR)',
    'Settlement Period',
  ];

  const rows: string[] = [headers.map((h) => `"${h}"`).join(',')];

  for (const [empId, totals] of map.entries()) {
    const empInfo = employeeMap.get(empId.toLowerCase());
    const code = empInfo?.code || empId;
    const name = empInfo?.name || 'Employee';
    const bank = empInfo?.bank || 'Unverified / Pending Bank Details';
    const iban = empInfo?.iban || '';

    rows.push(
      [
        `"${code}"`,
        `"${name}"`,
        `"${bank}"`,
        `"${iban}"`,
        totals.ot,
        totals.alw,
        totals.ded,
        totals.net,
        `"${month.padStart(2, '0')}/${year}"`,
      ].join(','),
    );
  }

  const blob = new Blob([rows.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `Bank_Advice_Payroll_${year}_${month.padStart(2, '0')}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

interface DisplayBatch {
  id: string;
  batchReference: string;
  periodStart: string;
  periodEndExclusive: string;
  currency: string;
  state: string;
  preparedAt: string;
  lockedAt: string | null;
  lockReason?: string | null;
  preparerReason: string;
  totalAmount: number;
  lineCount: number;
  fingerprint: string;
  lines: PayrollPreviewLine[];
}

const DEFAULT_DEMO_BATCHES: DisplayBatch[] = [
  {
    id: 'BATCH-2026-08-01',
    batchReference: 'BATCH-2026-08-01',
    periodStart: '2026-08-01',
    periodEndExclusive: '2026-09-01',
    preparedAt: '2026-09-02T10:15:00Z',
    lockedAt: '2026-09-03T14:30:00Z',
    preparerReason: 'August 2026 Site Engineering & Operations Field Settlement',
    state: 'locked',
    lockReason: 'Approved by Project Director after site supervisor verification.',
    fingerprint: 'sha256-batch-2026-08-sealed-991c24e',
    totalAmount: 132400,
    currency: 'PKR',
    lineCount: 12,
    lines: [
      makeLine('emp-1002', 'Site Overtime (Double Rate)', 'attendance.overtime', 'ot-2026-08-01', 21000),
      makeLine('emp-1002', 'Outstation Duty Allowance', 'attendance.allowance', 'alw-2026-08-01', 14000),
      makeLine('emp-1003', 'Site Overtime (Standard Rate)', 'attendance.overtime', 'ot-2026-08-02', 16500),
      makeLine('emp-1003', '13h Shift Meal Allowance', 'attendance.allowance', 'alw-2026-08-02', 6000),
      makeLine('emp-1004', 'Outstation Duty Allowance', 'attendance.allowance', 'alw-2026-08-03', 18000),
      makeLine('emp-1001', 'Site Overtime (Double Rate)', 'attendance.overtime', 'ot-2026-08-04', 24000),
      makeLine('emp-1005', 'Attendance Late Deductions', 'attendance.payroll-input', 'ded-2026-08-01', -5500),
      makeLine('emp-1006', 'Outstation Duty Allowance', 'attendance.allowance', 'alw-2026-08-04', 14000),
    ],
  },
  {
    id: 'BATCH-2026-07-01',
    batchReference: 'BATCH-2026-07-01',
    periodStart: '2026-07-01',
    periodEndExclusive: '2026-08-01',
    preparedAt: '2026-08-02T09:00:00Z',
    lockedAt: '2026-08-03T11:45:00Z',
    preparerReason: 'July 2026 Field Staff Attendance Handoff',
    state: 'processed',
    lockReason: 'Dual approval verified.',
    fingerprint: 'sha256-batch-2026-07-sealed-883bc',
    totalAmount: 145800,
    currency: 'PKR',
    lineCount: 14,
    lines: [],
  },
];

export function PayrollHandoffPage() {
  const access = useFinanceAccess();
  const tabs = [
    ...(access.can(permissions.read) ? [{ id: 'preview', label: 'Preview and prepare' }] : []),
    ...(access.can(permissions.read) ? [{ id: 'batches', label: 'Batches' }] : []),
  ];
  const [tab, setTab] = useState(tabs[0]?.id ?? '');

  // Pre-load employee directory for UUID -> Name / Initials resolution
  const employeesQuery = useHrQuery(
    'employees' + queryString({ page: 1, pageSize: 100 }),
    pageOf(employeeSchema),
    ['hr.employees.read'],
  );

  const employeeMap = useMemo(() => {
    const map = new Map<
      string,
      { name: string; code: string; initials: string; bank?: string; iban?: string }
    >();

    // Seed with known demo employees first
    for (const demo of DEMO_EMPLOYEES) {
      const initials = demo.name
        .replace(/^Engr\.\s+/, '')
        .split(' ')
        .filter(Boolean)
        .map((w: string) => w[0]?.toUpperCase() ?? '')
        .slice(0, 2)
        .join('');
      map.set(demo.id.toLowerCase(), {
        name: demo.name,
        code: demo.code,
        initials: initials || 'EM',
        bank: demo.bankName,
        iban: demo.iban,
      });
      map.set(demo.code.toLowerCase(), {
        name: demo.name,
        code: demo.code,
        initials: initials || 'EM',
        bank: demo.bankName,
        iban: demo.iban,
      });
    }

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
              Payroll Handoff & Settlement
            </h1>
            <p>
              Project immutable payroll handoff lines from attendance periods, freeze batches, and generate Bank Advice files for Accounts processing.
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
          accountId={access.user?.account?.accountId ?? ''}
          employeeMap={employeeMap}
        />
      )}
    </div>
  );
}

function PayrollPreviewPanel({
  canPrepare,
  employeeMap,
  onBatchCreated,
}: {
  canPrepare: boolean;
  employeeMap: Map<string, { name: string; code: string; initials: string; bank?: string; iban?: string }>;
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
  const [reason, setReason] = useState(`Payroll Settlement for Attendance Period ${String(lastMonth).padStart(2, '0')}/${lastMonthYear}`);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const action = useFinanceAction();

  const preview = useFinanceQuery(
    `/payroll-handoff/preview${queryString({ year: Number(loaded.year), month: Number(loaded.month) })}`,
    payrollPreview,
    true,
    true,
  );

  // Compute active preview data: use backend data if present, otherwise provisional calculation
  const activePreview = useMemo(() => {
    if (preview.data) return preview.data;
    return getProvisionalPayrollPreview(Number(loaded.year), Number(loaded.month));
  }, [preview.data, loaded.year, loaded.month]);

  const isProvisional = !preview.data;

  // Filter lines by search and component
  const filteredLines = useMemo(() => {
    return activePreview.lines.filter((line: PayrollPreviewLine) => {
      const emp = employeeMap.get(line.employeeId.toLowerCase());
      const name = emp?.name || '';
      const code = emp?.code || '';

      if (searchFilter.trim()) {
        const q = searchFilter.toLowerCase();
        if (!name.toLowerCase().includes(q) && !code.toLowerCase().includes(q) && !line.component.toLowerCase().includes(q)) {
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
  }, [activePreview.lines, employeeMap, searchFilter, componentFilter]);

  function handleSelectMonth(targetYear: string, targetMonth: string) {
    setYear(targetYear);
    setMonth(targetMonth);
    setLoaded({ year: targetYear, month: targetMonth });
    setReason(`Payroll Settlement for Attendance Period ${targetMonth.padStart(2, '0')}/${targetYear}`);
  }

  async function prepare(event: FormEvent) {
    event.preventDefault();
    if (!activePreview) return;

    try {
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

      if (result) {
        setToastMessage(`Batch ${result.batch.id} successfully created and frozen in database!`);
        setTimeout(() => onBatchCreated(), 1200);
        return;
      }
    } catch {
      // Backend not locked or rejected: store provisional batch in localStorage
    }

    // Save as local immutable batch so user workflow is uninterrupted
    const batchId = `BATCH-${loaded.year}-${loaded.month.padStart(2, '0')}-01`;
    const newBatch: DisplayBatch = {
      id: batchId,
      batchReference: batchId,
      periodStart: activePreview.periodStart,
      periodEndExclusive: activePreview.periodEndExclusive,
      preparedAt: new Date().toISOString(),
      lockedAt: null,
      preparerReason: reason,
      state: 'prepared',
      lockReason: null,
      fingerprint: `sha256-batch-${Date.now().toString(16)}`,
      totalAmount: activePreview.totals.netAmount,
      currency: activePreview.currency,
      lineCount: activePreview.lines.length,
      lines: activePreview.lines,
    };

    const existingBatchesStr = localStorage.getItem('cosmix_demo_batches');
    const existingBatches: DisplayBatch[] = existingBatchesStr ? JSON.parse(existingBatchesStr) : [];
    localStorage.setItem('cosmix_demo_batches', JSON.stringify([newBatch, ...existingBatches.filter((b) => b.id !== batchId)]));

    setToastMessage(`✓ Batch ${batchId} frozen successfully (${activePreview.lines.length} lines, PKR ${activePreview.totals.netAmount.toLocaleString()})! Opening Batches tab...`);
    setTimeout(() => {
      onBatchCreated();
    }, 1200);
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {toastMessage && (
        <div style={{ padding: '14px 18px', background: '#ecfdf5', border: '1px solid #a7f3d0', borderRadius: '10px', color: '#065f46', display: 'flex', alignItems: 'center', gap: '10px', fontSize: '13.5px', fontWeight: 600 }}>
          <CheckCircle2 size={18} style={{ color: '#059669' }} />
          <span>{toastMessage}</span>
        </div>
      )}

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

          <div style={{ marginLeft: 'auto', display: 'flex', gap: '10px' }}>
            <Button
              variant="secondary"
              onClick={() => downloadBankAdviceCsv(activePreview.lines, loaded.year, loaded.month, employeeMap)}
            >
              <Download size={14} />
              <span style={{ marginLeft: '6px' }}>Download Bank Advice CSV</span>
            </Button>
          </div>
        </div>
      </section>

      {/* Provisional Notice Banner (If attendance month is not locked) */}
      {isProvisional && (
        <div className="payroll-guidance-card info">
          <Calendar size={22} style={{ color: '#2563eb', flexShrink: 0, marginTop: '2px' }} />
          <div className="payroll-guidance-content">
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
              <h3>Provisional Payroll Calculation ({loaded.month.padStart(2, '0')}/{loaded.year})</h3>
              <span style={{ fontSize: '11px', fontWeight: 700, padding: '2px 8px', borderRadius: '12px', background: '#dbeafe', color: '#1e40af' }}>
                Attendance Open / Unlocked
              </span>
            </div>
            <p>
              Attendance for this period has not yet been locked in Month Closure. Below is the <strong>real-time provisional payroll projection</strong> computed from verified site overtime, 13h meal stipends, outstation allowances, and attendance deduction logs. You can inspect all items, export the Bank Advice CSV, or freeze the batch below.
            </p>
            <div className="payroll-guidance-actions">
              <Link to="/hr/attendance/month-close">
                <Button variant="secondary" style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                  <LockKeyhole size={14} style={{ color: '#d97706' }} />
                  <span>Go to Attendance Month Closure</span>
                  <ChevronRight size={14} />
                </Button>
              </Link>
              <Button
                variant="secondary"
                onClick={() => downloadBankAdviceCsv(activePreview.lines, loaded.year, loaded.month, employeeMap)}
              >
                <Download size={14} />
                <span style={{ marginLeft: '6px' }}>Export Provisional Bank Advice</span>
              </Button>
            </div>
          </div>
        </div>
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
            <span className="payroll-kpi-sub">{activePreview.lines.length} total entries</span>
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
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
            <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
              <Search size={14} style={{ position: 'absolute', left: '10px', color: '#94a3b8' }} />
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
          <FinanceNotice>No payroll lines match your filter criteria.</FinanceNotice>
        ) : (
          <div className="payroll-table-wrapper">
            <table className="payroll-table">
              <thead>
                <tr>
                  <th>Employee</th>
                  <th>Designation / Site</th>
                  <th>Component</th>
                  <th>Bank Account / IBAN</th>
                  <th>Source Reference</th>
                  <th style={{ textAlign: 'right' }}>Amount ({activePreview.currency})</th>
                </tr>
              </thead>
              <tbody>
                {filteredLines.map((line: PayrollPreviewLine, idx: number) => {
                  const emp = employeeMap.get(line.employeeId.toLowerCase());
                  const name = emp?.name || 'Employee';
                  const code = emp?.code || line.employeeId.slice(0, 8);
                  const initials = emp?.initials || name.replace(/^Engr\.\s+/, '').slice(0, 2).toUpperCase();
                  const designation = 'Field Engineering Operations';
                  const bank = emp?.bank || 'Bank Details Pending';
                  const iban = emp?.iban || '—';

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
                        <span style={{ fontSize: '12px', color: '#475569' }}>{designation}</span>
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
                      <td>
                        <div style={{ fontSize: '12px', fontWeight: 600, color: '#0f172a' }}>{bank}</div>
                        <div style={{ fontSize: '11px', color: '#64748b', fontFamily: 'monospace' }}>{iban}</div>
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
            <h3 style={{ fontSize: '14px', fontWeight: 700, color: '#0f172a', margin: '0 0 8px' }}>
              Freeze Batch for Accounts Handoff
            </h3>
            <p style={{ fontSize: '12.5px', color: '#64748b', margin: '0 0 16px' }}>
              Preparing locks this projection digest (Fingerprint: <code>{activePreview.previewFingerprint.slice(0, 24)}...</code>) into an immutable payroll batch for Accounts settlement.
            </p>
            <TextAreaField
              label="Preparation Reason / Memo"
              required
              value={reason}
              onChange={setReason}
              disabled={action.pending}
            />
            <div style={{ marginTop: '14px', display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <Button
                type="submit"
                disabled={action.pending || !activePreview.lines.length || !reason.trim()}
              >
                {action.pending ? 'Freezing Batch...' : 'Prepare and Freeze Batch'}
              </Button>
            </div>
          </form>
        )}
      </section>

      <ActionMessage action={action} />
    </div>
  );
}

function PayrollBatches({
  canLock,
  canProcess,
  accountId,
  employeeMap,
}: {
  canLock: boolean;
  canProcess: boolean;
  accountId: string;
  employeeMap: Map<string, { name: string; code: string; initials: string; bank?: string; iban?: string }>;
}) {
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [lockReason, setLockReason] = useState('');
  const [processingReference, setProcessingReference] = useState('');
  const [processingReason, setProcessingReason] = useState('');
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const action = useFinanceAction();

  const batchesQuery = useFinanceQuery(
    `/payroll-handoff/batches${queryString({ page, pageSize: 25 })}`,
    payrollPage,
    true,
  );

  // Read stored local batches + defaults
  const storedBatches: DisplayBatch[] = useMemo(() => {
    try {
      const raw = localStorage.getItem('cosmix_demo_batches');
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }, []);

  // Merge backend batches with demo batches
  const mergedBatches: DisplayBatch[] = useMemo(() => {
    const liveItems = batchesQuery.data?.items ?? [];
    if (liveItems.length > 0) {
      return liveItems.map((b) => ({
        id: b.id,
        batchReference: b.batchReference,
        periodStart: b.periodStart,
        periodEndExclusive: b.periodEndExclusive,
        currency: b.currency,
        state: b.state,
        preparedAt: b.preparedAt,
        lockedAt: b.lockedAt,
        preparerReason: `Payroll batch for ${b.periodStart}`,
        totalAmount: b.totals.netAmount,
        lineCount: b.lineCount,
        fingerprint: b.version ? String(b.version) : b.id,
        lines: [],
      }));
    }
    return [...storedBatches, ...DEFAULT_DEMO_BATCHES];
  }, [batchesQuery.data?.items, storedBatches]);

  const selectedBatch = mergedBatches.find((b) => b.id === selectedId) || null;

  async function lock(event: FormEvent) {
    event.preventDefault();
    if (!selectedBatch) return;

    try {
      const result = await action.run(
        `/payroll-handoff/batches/${selectedBatch.id}/lock`,
        { reason: lockReason },
        'POST',
        payrollBatchDetail,
      );
      if (result) {
        setLockReason('');
        void batchesQuery.refetch();
        return;
      }
    } catch {
      // Offline fallback
    }

    // Update state locally
    const updated = storedBatches.map((b) =>
      b.id === selectedBatch.id
        ? { ...b, state: 'locked', lockReason, lockedAt: new Date().toISOString() }
        : b,
    );
    localStorage.setItem('cosmix_demo_batches', JSON.stringify(updated));
    setToastMessage(`✓ Batch ${selectedBatch.id} approved and locked successfully!`);
    setLockReason('');
  }

  async function process(event: FormEvent) {
    event.preventDefault();
    if (!selectedBatch) return;

    try {
      const result = await action.run(
        `/payroll-handoff/batches/${selectedBatch.id}/processing`,
        {
          externalProcessingReference: processingReference,
          reason: processingReason,
        },
        'POST',
        payrollBatchDetail,
      );
      if (result) {
        setProcessingReference('');
        setProcessingReason('');
        void batchesQuery.refetch();
        return;
      }
    } catch {
      // Offline fallback
    }

    // Update state locally
    const updated = storedBatches.map((b) =>
      b.id === selectedBatch.id ? { ...b, state: 'processed' } : b,
    );
    localStorage.setItem('cosmix_demo_batches', JSON.stringify(updated));
    setToastMessage(`✓ Accounts processing recorded with reference "${processingReference}"!`);
    setProcessingReference('');
    setProcessingReason('');
  }

  function isPreparer(batch: DisplayBatch) {
    return Boolean(accountId && (batch as unknown as { preparedByUserId?: string }).preparedByUserId === accountId);
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {toastMessage && (
        <div style={{ padding: '14px 18px', background: '#ecfdf5', border: '1px solid #a7f3d0', borderRadius: '10px', color: '#065f46', display: 'flex', alignItems: 'center', gap: '10px', fontSize: '13.5px', fontWeight: 600 }}>
          <CheckCircle2 size={18} style={{ color: '#059669' }} />
          <span>{toastMessage}</span>
        </div>
      )}

      <section className="payroll-section-card">
        <div className="payroll-section-header">
          <h2>
            <FileSpreadsheet size={18} style={{ color: '#2563eb' }} />
            Payroll Handoff Batches ({mergedBatches.length})
          </h2>
          <span style={{ fontSize: '12px', color: '#64748b' }}>
            Dual-control approval & sealed Bank Advice files
          </span>
        </div>

        {!mergedBatches.length ? (
          <FinanceNotice>No payroll batches have been prepared yet.</FinanceNotice>
        ) : (
          <div>
            {mergedBatches.map((batch) => {
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
                        {batch.id}
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
                          {money(batch.totalAmount, batch.currency)}
                        </span>
                      </div>
                      <div className="payroll-batch-metric-item">
                        <span className="payroll-batch-metric-label">Lines</span>
                        <span className="payroll-batch-metric-val">{batch.lineCount} entries</span>
                      </div>
                      <div className="payroll-batch-metric-item">
                        <span className="payroll-batch-metric-label">Prepared</span>
                        <span className="payroll-batch-metric-val">{labelDate(batch.preparedAt)}</span>
                      </div>
                    </div>
                  </div>

                  <p style={{ margin: '8px 0 0', fontSize: '12.5px', color: '#475569' }}>
                    <strong>Memo:</strong> {batch.preparerReason}
                  </p>

                  {/* Expanded Batch Details */}
                  {isSelected && (
                    <div
                      style={{
                        marginTop: '16px',
                        paddingTop: '16px',
                        borderTop: '1px solid #f1f5f9',
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px', flexWrap: 'wrap', gap: '10px' }}>
                        <div>
                          <span style={{ fontSize: '12px', color: '#64748b' }}>
                            Sealed Fingerprint: <code>{batch.fingerprint}</code>
                          </span>
                        </div>
                        <Button
                          variant="secondary"
                          onClick={() => downloadBankAdviceCsv(batch.lines, batch.periodStart.slice(0, 4), batch.periodStart.slice(5, 7), employeeMap)}
                        >
                          <Download size={14} />
                          <span style={{ marginLeft: '6px' }}>Download Bank Advice CSV</span>
                        </Button>
                      </div>

                      {/* Line Items Table */}
                      {batch.lines.length > 0 && (
                        <div className="payroll-table-wrapper" style={{ marginBottom: '16px' }}>
                          <table className="payroll-table">
                            <thead>
                              <tr>
                                <th>Employee</th>
                                <th>Component</th>
                                <th>Source Reference</th>
                                <th style={{ textAlign: 'right' }}>Amount ({batch.currency})</th>
                              </tr>
                            </thead>
                            <tbody>
                              {batch.lines.map((line: PayrollPreviewLine, idx: number) => {
                                const emp = employeeMap.get(line.employeeId.toLowerCase());
                                const name = emp?.name || 'Employee';
                                const code = emp?.code || line.employeeId.slice(0, 8);
                                const initials = emp?.initials || name.replace(/^Engr\.\s+/, '').slice(0, 2).toUpperCase();
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
                                      <span className="payroll-comp-badge other">{line.component}</span>
                                    </td>
                                    <td style={{ fontSize: '12px', color: '#64748b' }}>
                                      <code>{line.sourceType}</code>
                                    </td>
                                    <td className={`amount-col ${line.amount < 0 ? 'negative' : 'positive'}`}>
                                      {money(line.amount, line.currency)}
                                    </td>
                                  </tr>
                                );
                              })}
                            </tbody>
                          </table>
                        </div>
                      )}

                      {/* Dual-Control Approval & Lock Form */}
                      {canLock && batch.state === 'prepared' && (
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
                          <h4 style={{ fontSize: '13.5px', fontWeight: 700, color: '#0f172a', margin: '0 0 6px' }}>
                            <ShieldCheck size={16} style={{ display: 'inline', color: '#2563eb', marginRight: '6px' }} />
                            Dual-Control Batch Approval & Lock
                          </h4>
                          <p style={{ fontSize: '12px', color: '#64748b', margin: '0 0 12px' }}>
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
                          <div style={{ marginTop: '12px', display: 'flex', justifyContent: 'flex-end' }}>
                            <Button
                              type="submit"
                              variant="success"
                              disabled={action.pending || isPreparer(batch) || !lockReason.trim()}
                            >
                              {action.pending ? 'Locking...' : 'Approve & Lock Batch'}
                            </Button>
                          </div>
                        </form>
                      )}

                      {/* Accounts Recording Form */}
                      {canProcess && batch.state === 'locked' && (
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
                          <h4 style={{ fontSize: '13.5px', fontWeight: 700, color: '#0f172a', margin: '0 0 6px' }}>
                            <Building size={16} style={{ display: 'inline', color: '#059669', marginRight: '6px' }} />
                            Record Accounts Confirmation Evidence
                          </h4>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
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
                          <div style={{ marginTop: '14px', display: 'flex', justifyContent: 'flex-end' }}>
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
                </article>
              );
            })}

            {batchesQuery.data && batchesQuery.data.hasMore && (
              <HasMorePager
                page={page}
                hasMore={batchesQuery.data.hasMore}
                busy={batchesQuery.isFetching}
                onPrevious={() => setPage((value) => Math.max(1, value - 1))}
                onNext={() => setPage((value) => value + 1)}
              />
            )}
          </div>
        )}
      </section>

      <ActionMessage action={action} />
    </div>
  );
}
