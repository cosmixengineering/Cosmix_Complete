import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import {
  FileSpreadsheet,
  Search,
  RefreshCw,
  CalendarCheck,
  TableProperties,
  Fingerprint,
  MapPin,
  Building2,
  Clock3,
  TrendingUp,
  Utensils,
  CalendarDays,
  Users,
  FileText,
  Fuel,
  Banknote,
  ShieldCheck,
  CheckCircle2,
  Filter,
  Download,
  AlertCircle,
  Eye,
  SlidersHorizontal,
} from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/auth/use-auth';
import type { SessionUser } from '@/auth/types';
import { Button } from '@/components/ui/Button';
import { useHrLifetime } from '../../hr/hooks/use-hr-lifetime';
import { useHrQuery } from '../../hr/hooks/use-hr-queries';
import { pageOf } from '../../hr/schemas/contracts';
import { siteSchema } from '../../hr/schemas/sites';
import { errorMessage } from '../../hr/utils/messages';
import { fetchReportTypes } from '../api/reports-api';
import {
  exportReportPage,
  fetchExportableReport,
  isValidReportExportReason,
  saveReportBlob,
} from '../api/report-export-jobs';
import { HrReportExportJobs } from '../components/HrReportExportJobs';
import {
  exportFormatOptions,
  type ReportEnvelope,
  type ReportFilter,
} from '../schemas/report-contracts';
import '@/styles/hr-reports.css';

const permissions = { read: 'hr.reports.read', export: 'hr.reports.export' };

type ReportPreview = ReportEnvelope & { requestedFilter: ReportFilter };

interface ReportDef {
  id: string;
  label: string;
  category: 'attendance' | 'performance' | 'workforce' | 'operations';
  desc: string;
  icon: typeof CalendarCheck;
}

const REPORT_CATALOG: ReportDef[] = [
  // 1. Attendance & Duty
  {
    id: 'daily-attendance',
    label: 'Daily Attendance',
    category: 'attendance',
    desc: 'Daily verified punches, check-in/out times, sites, and exceptions.',
    icon: CalendarCheck,
  },
  {
    id: 'monthly-attendance-register',
    label: 'Monthly Attendance Register',
    category: 'attendance',
    desc: '30-day attendance grid register with presence, absence, and leave counts.',
    icon: TableProperties,
  },
  {
    id: 'punch-audit',
    label: 'Punch Audit Trail',
    category: 'attendance',
    desc: 'Raw biometric & mobile punch evidence with geo-coordinates and face confidence.',
    icon: Fingerprint,
  },
  {
    id: 'operational-zones',
    label: 'Operational Zones',
    category: 'attendance',
    desc: 'Zone compliance (Green, Amber, Red) and boundary breach records.',
    icon: MapPin,
  },
  {
    id: 'site-reporting',
    label: 'Site Reporting',
    category: 'attendance',
    desc: 'Field supervisor structured attendance reports and manual rosters.',
    icon: Building2,
  },

  // 2. Performance & Time
  {
    id: 'late-and-kpi',
    label: 'Late & KPI Assessments',
    category: 'performance',
    desc: 'Grace period consumption, monthly late counts, and scoring impact.',
    icon: Clock3,
  },
  {
    id: 'overtime',
    label: 'Overtime Register',
    category: 'performance',
    desc: 'Approved site overtime hours, hourly rates, and verified payouts in PKR.',
    icon: TrendingUp,
  },
  {
    id: 'meal-allowance',
    label: 'Meal & Outstation Allowance',
    category: 'performance',
    desc: 'Office 13h meal (PKR 400), dinner, and PKR 1,000 outstation duty claims.',
    icon: Utensils,
  },
  {
    id: 'leave',
    label: 'Leave Balances & Utilization',
    category: 'performance',
    desc: 'Fiscal quota (12 Casual, 6 Sick), medical attachments, and encashments.',
    icon: CalendarDays,
  },

  // 3. Workforce & Compliance
  {
    id: 'employee-master',
    label: 'Employee Master',
    category: 'workforce',
    desc: 'Master directory of approved staff with designation, site, and bank status.',
    icon: Users,
  },
  {
    id: 'document-expiry',
    label: 'Document Expiry & Certifications',
    category: 'workforce',
    desc: 'CNIC, PEC engineering license, driving license, and passport tracking.',
    icon: FileText,
  },
  {
    id: 'audit',
    label: 'System & HR Audit Trail',
    category: 'workforce',
    desc: 'Tamper-evident logs of administrative edits, approvals, and mutations.',
    icon: ShieldCheck,
  },

  // 4. Operations & Finance
  {
    id: 'fuel-and-mileage',
    label: 'Fuel & Vehicle Maintenance',
    category: 'operations',
    desc: 'Motorcycle 28 km/L fuel reimbursements, 1000km tuning, and 8000km overhaul.',
    icon: Fuel,
  },
  {
    id: 'payroll-handoff',
    label: 'Monthly Payroll Handoff',
    category: 'operations',
    desc: 'Locked attendance deductions, net adjustments, and bank advice totals.',
    icon: Banknote,
  },
];

export function HrReportsPage() {
  const { user, status } = useAuth();
  const cache = useQueryClient();
  const [session, setSession] = useState({ user, revision: 0 });
  const [cacheEpoch, setCacheEpoch] = useState(0);

  if (session.user !== user) setSession({ user, revision: session.revision + 1 });

  useEffect(() => {
    return cache.getQueryCache().subscribe((event) => {
      if (
        event.type === 'removed' &&
        event.query.queryKey[0] === 'reports' &&
        event.query.getObserversCount() > 0
      )
        setCacheEpoch((value) => value + 1);
    });
  }, [cache]);

  if (status !== 'authenticated' || !hasReportPermission(user, permissions.read)) {
    return (
      <section aria-label="HR reports" className="reports-page-container">
        <div className="reports-hero-banner">
          <h1>Access Restricted</h1>
          <p>Your current role permissions do not allow reading HR reports.</p>
        </div>
      </section>
    );
  }

  const subject = `${user?.account?.accountId}:${session.revision}:${cacheEpoch}`;
  return (
    <ReportWorkspace
      key={subject}
      subject={subject}
      canExport={hasReportPermission(user, permissions.export)}
    />
  );
}

function hasReportPermission(user: SessionUser | null, permission: string) {
  return Boolean(
    user?.account &&
      user.grants.some(
        (grant) =>
          grant.permission === permission &&
          ((grant.scopeKind === 7 && grant.targetId === null) ||
            (grant.targetId !== null &&
              grant.targetId !== '00000000-0000-0000-0000-000000000000' &&
              ((grant.scopeKind === 1 && grant.targetId === user.account?.employeeId) ||
                [2, 3, 4, 5, 6].includes(grant.scopeKind)))),
      ),
  );
}

function ReportWorkspace({ canExport, subject }: { canExport: boolean; subject: string }) {
  const lifetime = useHrLifetime();
  const [types, setTypes] = useState<string[]>([]);
  const [typesError, setTypesError] = useState<string | null>(null);

  // Active Category & Active Report selection
  const [activeCategory, setActiveCategory] = useState<string>('all');
  const [reportType, setReportType] = useState<string>('daily-attendance');

  // Today Date & Defaults
  const todayStr = useMemo(() => new Date().toISOString().slice(0, 10), []);
  const currentYear = useMemo(() => new Date().getFullYear(), []);
  const currentMonth = useMemo(() => new Date().getMonth() + 1, []);

  // Filter State
  const [filter, setFilter] = useState({
    from: todayStr.slice(0, 8) + '01',
    to: todayStr,
    date: todayStr,
    year: String(currentYear),
    month: String(currentMonth),
    siteId: '',
    departmentId: '',
    status: '',
    category: '',
    documentType: '',
    page: '1',
    pageSize: '50',
  });

  const [result, setResult] = useState<ReportPreview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [exportSuccess, setExportSuccess] = useState<string | null>(null);
  const [exportFormat, setExportFormat] = useState('2');
  const [exportReason, setExportReason] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const inFlight = useRef(false);
  const exportInFlight = useRef(false);

  // Load real active sites for dropdown
  const sitesQuery = useHrQuery(
    'sites?page=1&pageSize=100&isActive=true',
    pageOf(siteSchema),
    ['hr.sites.read'],
  );
  const sitesList = sitesQuery.data?.items ?? [];

  useEffect(() => {
    const controller = new AbortController();
    const signal = AbortSignal.any([controller.signal, lifetime()]);
    fetchReportTypes(signal)
      .then((list) => {
        if (!signal.aborted) {
          setTypes(list);
          if (list.length > 0 && !list.includes(reportType)) {
            setReportType(list[0]!);
          }
        }
      })
      .catch((failure) => {
        if (!signal.aborted) setTypesError(errorMessage(failure));
      });
    return () => controller.abort();
  }, []);

  // Filter Catalog by Active Category Tab and Available Types
  const visibleReports = useMemo(() => {
    let list = REPORT_CATALOG;
    if (types.length > 0) list = list.filter((r) => types.includes(r.id));
    if (activeCategory === 'all') return list;
    return list.filter((r) => r.category === activeCategory);
  }, [activeCategory, types]);

  const activeDef = useMemo(
    () => REPORT_CATALOG.find((r) => r.id === reportType) ?? REPORT_CATALOG[0]!,
    [reportType],
  );

  const columns = useMemo(() => {
    const first = result?.rows[0];
    return first ? Object.keys(first) : [];
  }, [result]);

  // Quick Preset Handlers
  function applyPreset(preset: 'today' | 'yesterday' | 'thisMonth' | 'last30Days') {
    const now = new Date();
    if (preset === 'today') {
      const d = now.toISOString().slice(0, 10);
      setFilter((prev) => ({ ...prev, from: d, to: d, date: d }));
    } else if (preset === 'yesterday') {
      const y = new Date(now.getTime() - 86400000).toISOString().slice(0, 10);
      setFilter((prev) => ({ ...prev, from: y, to: y, date: y }));
    } else if (preset === 'thisMonth') {
      const from = now.toISOString().slice(0, 8) + '01';
      const to = now.toISOString().slice(0, 10);
      setFilter((prev) => ({ ...prev, from, to, year: String(now.getFullYear()), month: String(now.getMonth() + 1) }));
    } else if (preset === 'last30Days') {
      const from = new Date(now.getTime() - 30 * 86400000).toISOString().slice(0, 10);
      const to = now.toISOString().slice(0, 10);
      setFilter((prev) => ({ ...prev, from, to }));
    }
  }

  async function runPreview(event?: FormEvent) {
    if (event) event.preventDefault();
    if (inFlight.current || !reportType) return;
    const errors: Record<string, string> = {};

    if (filter.from && filter.to && filter.to < filter.from)
      errors.to = 'The end date cannot be before the start date.';
    if (filter.from && filter.to) {
      const days = (Date.parse(filter.to) - Date.parse(filter.from)) / 86400000;
      if (days >= 366) errors.to = 'The date range cannot exceed 366 days.';
    }

    const page = Number(filter.page);
    const pageSize = Number(filter.pageSize);
    if (!Number.isInteger(page) || page < 1) errors.page = 'Enter a page number of 1 or more.';
    if (!Number.isInteger(pageSize) || pageSize < 1 || pageSize > 1000)
      errors.pageSize = 'Page size must be between 1 and 1000.';

    setFieldErrors(errors);
    if (Object.keys(errors).length) return;

    inFlight.current = true;
    setPending(true);
    setError(null);
    setExportError(null);
    setExportSuccess(null);

    const requestedFilter: ReportFilter = {
      from: filter.from || undefined,
      to: filter.to || undefined,
      date: filter.date || undefined,
      year: filter.year ? Number(filter.year) : undefined,
      month: filter.month ? Number(filter.month) : undefined,
      siteId: filter.siteId || undefined,
      departmentId: filter.departmentId || undefined,
      status: filter.status || undefined,
      category: filter.category || undefined,
      documentType: filter.documentType || undefined,
      page,
      pageSize,
    };

    const signal = lifetime();
    try {
      const data = await fetchExportableReport(reportType, requestedFilter, signal);
      if (!signal.aborted) setResult({ ...data, requestedFilter: data.metadata.filter });
    } catch (failure) {
      if (!signal.aborted) setError(errorMessage(failure));
    } finally {
      inFlight.current = false;
      if (!signal.aborted) setPending(false);
    }
  }

  async function runExport(event: FormEvent) {
    event.preventDefault();
    if (exportInFlight.current || !canExport || !result) return;
    const errors: Record<string, string> = {};
    if (!exportReason.trim()) errors.reason = 'An audit reason is required.';
    else if (exportReason.length > 1000)
      errors.reason = 'The reason must be at most 1000 characters.';
    else if (!isValidReportExportReason(exportReason))
      errors.reason = 'Enter the reason on one line without control characters.';

    setFieldErrors(errors);
    if (Object.keys(errors).length) return;

    exportInFlight.current = true;
    setExporting(true);
    setExportError(null);
    setExportSuccess(null);
    const signal = lifetime();

    try {
      const blob = await exportReportPage(
        result.metadata.reportType,
        Number(exportFormat),
        exportReason.trim(),
        result.requestedFilter,
        signal,
      );
      if (signal.aborted) return;
      const extension = exportFormat === '1' ? 'csv' : 'xlsx';
      saveReportBlob(
        blob,
        `${result.metadata.reportType}-${new Date().toISOString().slice(0, 10)}.${extension}`,
        signal,
      );
      setExportSuccess(
        `${extension.toUpperCase()} download initiated for ${result.metadata.reportTitle} (${result.rows.length} rows).`,
      );
      setExportReason('');
    } catch (failure) {
      if (!signal.aborted) setExportError(errorMessage(failure));
    } finally {
      exportInFlight.current = false;
      if (!signal.aborted) setExporting(false);
    }
  }

  function handleSelectReport(typeId: string) {
    setReportType(typeId);
    setResult(null);
    setError(null);
  }

  // Which filters to display based on active report
  const isSingleDateReport = ['daily-attendance', 'operational-zones'].includes(reportType);
  const isMonthYearReport = ['monthly-attendance-register', 'payroll-handoff'].includes(reportType);
  const isMasterReport = reportType === 'employee-master';
  const isDocExpiryReport = reportType === 'document-expiry';

  return (
    <div className="reports-page-container">
      {/* 1. Hero / Header Banner */}
      <section className="reports-hero-banner">
        <div className="reports-hero-top">
          <div className="reports-hero-title">
            <h1>
              <FileSpreadsheet size={24} style={{ color: '#2563eb' }} />
              Reports & Executive Analytics
            </h1>
            <p>
              Inspect, preview, and export 14 audited operational reports across attendance, workforce, assets, and payroll.
            </p>
          </div>
          <div style={{ display: 'flex', gap: '8px' }}>
            <Button
              variant="secondary"
              onClick={() => void runPreview()}
              disabled={pending || !reportType}
            >
              <RefreshCw size={14} className={pending ? 'animate-spin' : ''} />
              <span style={{ marginLeft: '6px' }}>Run Query</span>
            </Button>
          </div>
        </div>

        {/* Category Tabs */}
        <nav className="reports-category-nav" aria-label="Report Categories">
          <button
            type="button"
            className={`reports-cat-btn ${activeCategory === 'all' ? 'active' : ''}`}
            onClick={() => setActiveCategory('all')}
          >
            <span>All Reports</span>
            <span className="reports-cat-badge">14</span>
          </button>
          <button
            type="button"
            className={`reports-cat-btn ${activeCategory === 'attendance' ? 'active' : ''}`}
            onClick={() => setActiveCategory('attendance')}
          >
            <CalendarCheck size={14} />
            <span>Attendance & Duty</span>
            <span className="reports-cat-badge">5</span>
          </button>
          <button
            type="button"
            className={`reports-cat-btn ${activeCategory === 'performance' ? 'active' : ''}`}
            onClick={() => setActiveCategory('performance')}
          >
            <Clock3 size={14} />
            <span>Time & Performance</span>
            <span className="reports-cat-badge">4</span>
          </button>
          <button
            type="button"
            className={`reports-cat-btn ${activeCategory === 'workforce' ? 'active' : ''}`}
            onClick={() => setActiveCategory('workforce')}
          >
            <Users size={14} />
            <span>Workforce & Compliance</span>
            <span className="reports-cat-badge">3</span>
          </button>
          <button
            type="button"
            className={`reports-cat-btn ${activeCategory === 'operations' ? 'active' : ''}`}
            onClick={() => setActiveCategory('operations')}
          >
            <Banknote size={14} />
            <span>Operations & Finance</span>
            <span className="reports-cat-badge">2</span>
          </button>
        </nav>

        {typesError && (
          <div style={{ marginTop: '12px', padding: '10px 14px', background: '#fffbeb', border: '1px solid #fde68a', borderRadius: '8px', color: '#92400e', fontSize: '12px' }}>
            Catalog Notice: {typesError}
          </div>
        )}

        {/* 2. Interactive Report Catalog Cards Grid */}
        <div className="reports-cards-grid">
          {visibleReports.map((item) => {
            const Icon = item.icon;
            const isSelected = item.id === reportType;
            return (
              <div
                key={item.id}
                role="button"
                tabIndex={0}
                className={`report-item-card ${isSelected ? 'selected' : ''}`}
                onClick={() => handleSelectReport(item.id)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') handleSelectReport(item.id);
                }}
              >
                <div>
                  <div className="report-card-header">
                    <div className="report-card-icon">
                      <Icon size={18} />
                    </div>
                    <span className="report-card-title">{item.label}</span>
                  </div>
                  <p className="report-card-desc" style={{ marginTop: '8px' }}>
                    {item.desc}
                  </p>
                </div>
                <div className="report-card-footer">
                  <span style={{ textTransform: 'capitalize' }}>{item.category}</span>
                  {isSelected && (
                    <span style={{ color: '#2563eb', fontWeight: 700, display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                      <CheckCircle2 size={13} /> Active
                    </span>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </section>

      {/* 3. Context-Aware Smart Filters Box */}
      <section className="reports-filter-card">
        <div className="reports-filter-header">
          <div className="reports-filter-title">
            <SlidersHorizontal size={18} style={{ color: '#2563eb' }} />
            <h2>
              Filter Parameters &bull;{' '}
              <span style={{ color: '#2563eb', fontWeight: 600 }}>{activeDef.label}</span>
            </h2>
          </div>

          {/* Quick Date Presets */}
          <div className="reports-quick-presets">
            <span style={{ fontSize: '11px', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>
              Presets:
            </span>
            <button
              type="button"
              className="reports-preset-btn"
              onClick={() => applyPreset('today')}
            >
              Today
            </button>
            <button
              type="button"
              className="reports-preset-btn"
              onClick={() => applyPreset('yesterday')}
            >
              Yesterday
            </button>
            <button
              type="button"
              className="reports-preset-btn"
              onClick={() => applyPreset('thisMonth')}
            >
              This Month
            </button>
            <button
              type="button"
              className="reports-preset-btn"
              onClick={() => applyPreset('last30Days')}
            >
              Last 30 Days
            </button>
          </div>
        </div>

        <form onSubmit={(e) => void runPreview(e)}>
          <div className="reports-form-grid">
            {/* 1. Date Controls based on Report Type */}
            {isSingleDateReport ? (
              <div className="reports-field-group">
                <label>Attendance Date</label>
                <input
                  type="date"
                  value={filter.date}
                  onChange={(e) =>
                    setFilter((prev) => ({ ...prev, date: e.target.value, from: e.target.value, to: e.target.value }))
                  }
                />
              </div>
            ) : isMonthYearReport ? (
              <>
                <div className="reports-field-group">
                  <label>Calendar Year</label>
                  <input
                    type="number"
                    min={2020}
                    max={2099}
                    value={filter.year}
                    onChange={(e) => setFilter((prev) => ({ ...prev, year: e.target.value }))}
                  />
                </div>
                <div className="reports-field-group">
                  <label>Month (1 - 12)</label>
                  <select
                    value={filter.month}
                    onChange={(e) => setFilter((prev) => ({ ...prev, month: e.target.value }))}
                  >
                    {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
                      <option key={m} value={m}>
                        {new Date(2026, m - 1).toLocaleString('en-US', { month: 'long' })} ({m})
                      </option>
                    ))}
                  </select>
                </div>
              </>
            ) : (
              <>
                <div className="reports-field-group">
                  <label>Date From</label>
                  <input
                    type="date"
                    value={filter.from}
                    onChange={(e) => setFilter((prev) => ({ ...prev, from: e.target.value }))}
                  />
                  {fieldErrors.from && <span style={{ color: '#ef4444', fontSize: '11px' }}>{fieldErrors.from}</span>}
                </div>
                <div className="reports-field-group">
                  <label>Date To</label>
                  <input
                    type="date"
                    value={filter.to}
                    onChange={(e) => setFilter((prev) => ({ ...prev, to: e.target.value }))}
                  />
                  {fieldErrors.to && <span style={{ color: '#ef4444', fontSize: '11px' }}>{fieldErrors.to}</span>}
                </div>
              </>
            )}

            {/* 2. Site Dropdown (Replaces raw Site GUID input!) */}
            <div className="reports-field-group">
              <label>Work Site / Operational Boundary</label>
              <select
                value={filter.siteId}
                onChange={(e) => setFilter((prev) => ({ ...prev, siteId: e.target.value }))}
              >
                <option value="">All Sites & Projects</option>
                {sitesList.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name} ({s.code})
                  </option>
                ))}
              </select>
            </div>

            {/* 3. Specialized Report Filters */}
            {isMasterReport && (
              <>
                <div className="reports-field-group">
                  <label>Employment Status</label>
                  <select
                    value={filter.status}
                    onChange={(e) => setFilter((prev) => ({ ...prev, status: e.target.value }))}
                  >
                    <option value="">All Statuses</option>
                    <option value="2">Active & Approved</option>
                    <option value="0">Draft</option>
                    <option value="3">Archived</option>
                  </select>
                </div>
                <div className="reports-field-group">
                  <label>Staff Taxonomy / Category</label>
                  <select
                    value={filter.category}
                    onChange={(e) => setFilter((prev) => ({ ...prev, category: e.target.value }))}
                  >
                    <option value="">All Categories</option>
                    <option value="OFFICE">Office Staff</option>
                    <option value="SITE">Site / Field Staff</option>
                    <option value="RIDER">Delivery Rider</option>
                    <option value="DRIVER">Vehicle Driver</option>
                    <option value="TECHNICIAN">HVAC Technician</option>
                  </select>
                </div>
              </>
            )}

            {isDocExpiryReport && (
              <div className="reports-field-group">
                <label>Document Type</label>
                <select
                  value={filter.documentType}
                  onChange={(e) => setFilter((prev) => ({ ...prev, documentType: e.target.value }))}
                >
                  <option value="">All Verified Documents</option>
                  <option value="CNIC">CNIC / Smart National Identity</option>
                  <option value="DRIVING_LICENSE">Valid Driving License</option>
                  <option value="PEC_LICENSE">PEC Engineer Registration</option>
                  <option value="PASSPORT">Passport</option>
                  <option value="VEHICLE_REGISTRATION">Vehicle Registration</option>
                </select>
              </div>
            )}

            {/* 4. Page Size & Pagination */}
            <div className="reports-field-group">
              <label>Page Size</label>
              <select
                value={filter.pageSize}
                onChange={(e) => setFilter((prev) => ({ ...prev, pageSize: e.target.value }))}
              >
                <option value="25">25 rows</option>
                <option value="50">50 rows</option>
                <option value="100">100 rows</option>
                <option value="250">250 rows</option>
                <option value="500">500 rows</option>
              </select>
            </div>
          </div>

          <div className="reports-actions-row">
            <Button type="submit" disabled={pending || !reportType}>
              <Search size={16} />
              <span style={{ marginLeft: '6px' }}>{pending ? 'Executing Query...' : 'Generate Preview'}</span>
            </Button>
          </div>
        </form>

        {error && (
          <div style={{ marginTop: '16px', background: '#fef2f2', border: '1px solid #fecdd3', borderRadius: '8px', padding: '14px', color: '#b91c1c', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <AlertCircle size={18} />
            <span style={{ fontSize: '13px' }}>{error}</span>
          </div>
        )}
      </section>

      {/* 4. Results Card & Tabular Data */}
      {result && (
        <section className="reports-result-card">
          <div className="reports-result-header">
            <div>
              <div className="reports-result-title">
                <h2>
                  <Eye size={18} style={{ color: '#2563eb' }} />
                  {result.metadata.reportTitle}
                </h2>
              </div>
              <div className="reports-meta-chips">
                <span className="reports-chip success">
                  <CheckCircle2 size={12} />
                  {result.rows.length} rows returned (Total matching: {result.metadata.totalCount})
                </span>
                <span className="reports-chip">
                  <Clock3 size={12} />
                  Generated: {new Date(result.metadata.generatedAtUtc).toLocaleTimeString()} ({result.metadata.timeZone})
                </span>
                <span className="reports-chip">
                  Lock State: {result.metadata.sourceLockStatus}
                </span>
              </div>
            </div>

            {/* Quick Export Buttons */}
            {canExport && (
              <div style={{ display: 'flex', gap: '8px' }}>
                <Button
                  variant="secondary"
                  disabled={exporting || !result.rows.length}
                  onClick={() => {
                    setExportFormat('2');
                    setExportReason(`Executive Excel export of ${result.metadata.reportTitle}`);
                    const event = { preventDefault: () => {} } as FormEvent;
                    void runExport(event);
                  }}
                  style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
                >
                  <FileSpreadsheet size={15} style={{ color: '#059669' }} />
                  <span>Excel (XLSX)</span>
                </Button>
                <Button
                  variant="secondary"
                  disabled={exporting || !result.rows.length}
                  onClick={() => {
                    setExportFormat('1');
                    setExportReason(`CSV export of ${result.metadata.reportTitle}`);
                    const event = { preventDefault: () => {} } as FormEvent;
                    void runExport(event);
                  }}
                  style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
                >
                  <Download size={15} style={{ color: '#2563eb' }} />
                  <span>CSV File</span>
                </Button>
              </div>
            )}
          </div>

          {/* Table */}
          {!result.rows.length ? (
            <div style={{ textAlign: 'center', padding: '40px 20px', background: '#f8fafc', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
              <Filter size={24} style={{ color: '#94a3b8', margin: '0 auto 8px' }} />
              <p style={{ fontSize: '14px', fontWeight: 600, color: '#334155', margin: 0 }}>
                No records found matching the specified parameters.
              </p>
              <p style={{ fontSize: '12px', color: '#64748b', margin: '4px 0 0' }}>
                Try widening your date range or clearing the site filter.
              </p>
            </div>
          ) : (
            <div className="reports-table-container">
              <table className="reports-data-table">
                <thead>
                  <tr>
                    {columns.map((column) => (
                      <th key={column}>{formatHeader(column)}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {result.rows.map((row, index) => (
                    <tr key={index}>
                      {columns.map((column) => (
                        <td key={column}>{renderCell(column, row[column])}</td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Export Audit Bar */}
          {canExport && (
            <div className="reports-export-box">
              <div className="reports-export-box-left">
                <h3>Export Scoped Report</h3>
                <p>Download the current result set with cryptographic hash verification and audit entry.</p>
              </div>

              <form className="reports-export-form" onSubmit={(e) => void runExport(e)}>
                <select
                  disabled={exporting}
                  value={exportFormat}
                  onChange={(e) => setExportFormat(e.target.value)}
                  style={{ height: '36px', padding: '0 10px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                >
                  {exportFormatOptions.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>

                <input
                  type="text"
                  placeholder="Audit reason / purpose (required)..."
                  required
                  value={exportReason}
                  onChange={(e) => setExportReason(e.target.value)}
                  style={{ height: '36px', width: '280px', padding: '0 10px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '12px' }}
                />

                <Button type="submit" disabled={exporting || !result.rows.length}>
                  <Download size={14} />
                  <span style={{ marginLeft: '6px' }}>{exporting ? 'Exporting...' : 'Export File'}</span>
                </Button>
              </form>
            </div>
          )}

          {exportError && (
            <p style={{ margin: '12px 0 0', color: '#b91c1c', fontSize: '12.5px' }}>{exportError}</p>
          )}
          {exportSuccess && (
            <p style={{ margin: '12px 0 0', color: '#047857', fontSize: '12.5px' }}>{exportSuccess}</p>
          )}
        </section>
      )}

      {/* 5. Historical Background Export Jobs */}
      {canExport && (
        <section style={{ marginTop: '10px' }}>
          <HrReportExportJobs
            subject={subject}
            selection={
              result
                ? {
                    reportType: result.metadata.reportType,
                    title: result.metadata.reportTitle,
                    filter: result.requestedFilter,
                  }
                : null
            }
          />
        </section>
      )}
    </div>
  );
}

// Convert camelCase or snake_case key into clean Title Case
function formatHeader(key: string): string {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/[_-]/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase())
    .trim();
}

function renderCell(column: string, value: unknown) {
  if (value === null || value === undefined || value === '') return <span style={{ color: '#cbd5e1' }}>—</span>;
  if (typeof value === 'boolean') {
    return value ? (
      <span className="reports-cell-badge positive">Yes</span>
    ) : (
      <span className="reports-cell-badge neutral">No</span>
    );
  }

  const str = String(value);
  const lowerCol = column.toLowerCase();

  // Highlight status values
  if (lowerCol.includes('status') || lowerCol.includes('state')) {
    const sLower = str.toLowerCase();
    if (sLower.includes('present') || sLower.includes('approved') || sLower.includes('completed') || sLower.includes('active')) {
      return <span className="reports-cell-badge positive">{str}</span>;
    }
    if (sLower.includes('late') || sLower.includes('pending') || sLower.includes('warning')) {
      return <span className="reports-cell-badge warning">{str}</span>;
    }
    if (sLower.includes('absent') || sLower.includes('rejected') || sLower.includes('failed')) {
      return <span className="reports-cell-badge danger">{str}</span>;
    }
  }

  // Format currency or large numbers
  if (lowerCol.includes('amount') || lowerCol.includes('pkr') || lowerCol.includes('rupees')) {
    const num = Number(value);
    if (!isNaN(num)) {
      return <strong style={{ color: '#0f172a' }}>PKR {num.toLocaleString()}</strong>;
    }
  }

  return str;
}
