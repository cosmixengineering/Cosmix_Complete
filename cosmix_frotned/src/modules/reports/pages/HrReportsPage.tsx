import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { FileSpreadsheet, Search, RefreshCw } from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/auth/use-auth';
import type { SessionUser } from '@/auth/types';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { useHrLifetime } from '../../hr/hooks/use-hr-lifetime';
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

const permissions = { read: 'hr.reports.read', export: 'hr.reports.export' };

type ReportPreview = ReportEnvelope & { requestedFilter: ReportFilter };

const reportLabels: Record<string, string> = {
  'daily-attendance': 'Daily Attendance',
  'monthly-attendance-register': 'Monthly Attendance Register',
  'punch-audit': 'Punch Audit Trail',
  'operational-zones': 'Operational Zones',
  'site-reporting': 'Site Reporting',
  'late-and-kpi': 'Late & KPI',
  overtime: 'Overtime',
  'meal-allowance': 'Meal Allowance',
  leave: 'Leave',
  'employee-master': 'Employee Master',
  'document-expiry': 'Document Expiry',
  'fuel-and-mileage': 'Fuel & Mileage',
  'payroll-handoff': 'Payroll Handoff',
  audit: 'Audit',
};

export function HrReportsPage() {
  const { user, status } = useAuth();
  const cache = useQueryClient();
  const [session, setSession] = useState({ user, revision: 0 });
  const [cacheEpoch, setCacheEpoch] = useState(0);
  if (session.user !== user) setSession({ user, revision: session.revision + 1 });
  useEffect(
    () =>
      cache.getQueryCache().subscribe((event) => {
        if (
          event.type === 'removed' &&
          event.query.queryKey[0] === 'reports' &&
          event.query.getObserversCount() > 0
        )
          setCacheEpoch((value) => value + 1);
      }),
    [cache],
  );
  if (status !== 'authenticated' || !hasReportPermission(user, permissions.read)) {
    return (
      <section aria-label="HR reports">
        <div className="hr-page-heading">
          <p className="hr-kicker">HUMAN RESOURCES &bull; Analytics</p>
          <h1>Reports</h1>
        </div>
        <p className="hr-notice" role="alert">
          You do not hold the hr.reports.read permission.
        </p>
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

// This only opens the scoped UI. Current and historical source authorization stays on the server.
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
  const [reportType, setReportType] = useState('');
  const [filter, setFilter] = useState({
    from: '',
    to: '',
    date: '',
    year: '',
    month: '',
    siteId: '',
    departmentId: '',
    status: '',
    category: '',
    documentType: '',
    page: '1',
    pageSize: '100',
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

  useEffect(() => {
    const controller = new AbortController();
    const signal = AbortSignal.any([controller.signal, lifetime()]);
    fetchReportTypes(signal)
      .then((list) => {
        if (!signal.aborted) setTypes(list);
      })
      .catch((failure) => {
        if (!signal.aborted) setTypesError(errorMessage(failure));
      });
    return () => controller.abort();
  }, []);

  const columns = useMemo(() => {
    const first = result?.rows[0];
    return first ? Object.keys(first) : [];
  }, [result]);

  async function runPreview(event: FormEvent) {
    event.preventDefault();
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
    if (!exportReason.trim()) errors.reason = 'A reason is required for the audit record.';
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
        `${extension.toUpperCase()} download started for ${result.metadata.reportTitle}. ` +
          `${reportPageContext(result.requestedFilter)}. ` +
          `Preview returned ${result.rows.length} rows; the file records the export-time count.`,
      );
      setExportReason('');
    } catch (failure) {
      if (!signal.aborted) setExportError(errorMessage(failure));
    } finally {
      exportInFlight.current = false;
      if (!signal.aborted) setExporting(false);
    }
  }

  function set(key: keyof typeof filter) {
    return (event: { target: { value: string } }) =>
      setFilter((previous) => ({ ...previous, [key]: event.target.value }));
  }

  return (
    <section aria-label="HR reports">
      <div className="hr-page-heading">
        <p className="hr-kicker">HUMAN RESOURCES &bull; Analytics</p>
        <h1>Reports</h1>
      </div>
      <div className="hr-form-section">
        <h2>Report selection</h2>
        {typesError && (
          <div className="hr-notice" role="alert">
            <p>{typesError}</p>
            <Button variant="secondary" onClick={() => window.location.reload()}>
              Reload
            </Button>
          </div>
        )}
        {!types.length && !typesError && (
          <p className="hr-notice" role="status">
            Loading report types...
          </p>
        )}
        {!!types.length && (
          <div className="hr-form-grid">
            <SelectInput
              label="Report"
              required
              disabled={pending || exporting}
              value={reportType}
              onChange={(event) => {
                setReportType(event.target.value);
                setResult(null);
                setError(null);
              }}
              options={[
                { value: '', label: 'Choose a report' },
                ...types.map((type) => ({
                  value: type,
                  label: reportLabels[type] ?? type,
                })),
              ]}
            />
            <TextInput
              label="From"
              type="date"
              value={filter.from}
              error={fieldErrors.from}
              onChange={set('from')}
            />
            <TextInput
              label="To"
              type="date"
              value={filter.to}
              error={fieldErrors.to}
              onChange={set('to')}
            />
            <TextInput label="Single date" type="date" value={filter.date} onChange={set('date')} />
            <TextInput
              label="Year"
              type="number"
              min={2000}
              max={2100}
              value={filter.year}
              onChange={set('year')}
            />
            <TextInput
              label="Month"
              type="number"
              min={1}
              max={12}
              value={filter.month}
              onChange={set('month')}
            />
            <TextInput label="Site id" value={filter.siteId} onChange={set('siteId')} />
            <TextInput
              label="Department id"
              value={filter.departmentId}
              onChange={set('departmentId')}
            />
            <TextInput label="Status" value={filter.status} onChange={set('status')} />
            <TextInput label="Category" value={filter.category} onChange={set('category')} />
            <TextInput
              label="Document type"
              value={filter.documentType}
              onChange={set('documentType')}
            />
            <TextInput
              label="Page"
              required
              type="number"
              min={1}
              value={filter.page}
              error={fieldErrors.page}
              onChange={set('page')}
            />
            <TextInput
              label="Page size"
              required
              type="number"
              min={1}
              max={1000}
              value={filter.pageSize}
              error={fieldErrors.pageSize}
              onChange={set('pageSize')}
            />
          </div>
        )}
        <div className="hr-actions">
          <Button onClick={(event) => void runPreview(event)} disabled={pending || !reportType}>
            <Search size={16} />
            {pending ? 'Loading...' : 'Preview report'}
          </Button>
        </div>
        {error && (
          <div className="hr-notice" role="alert">
            <p>{error}</p>
            <Button variant="secondary" onClick={(event) => void runPreview(event)}>
              <RefreshCw size={14} /> Retry
            </Button>
          </div>
        )}
      </div>
      {result && (
        <>
          <div className="hr-form-section">
            <div className="hr-repeat-heading">
              <h2>{result.metadata.reportTitle}</h2>
              <span className="hr-status hr-status--neutral">
                {result.rows.length} rows returned / {result.metadata.totalCount} matching &middot;{' '}
                {result.metadata.timeZone}
              </span>
            </div>
            <p className="muted">
              Generated {new Date(result.metadata.generatedAtUtc).toLocaleString()} &middot;{' '}
              {result.metadata.sourceLockStatus}
            </p>
            <p className="muted" style={{ overflowWrap: 'anywhere' }}>
              {reportPageContext(result.requestedFilter)}
            </p>
            <div className="hr-table-wrap">
              <table className="hr-table">
                <thead>
                  <tr>
                    {columns.map((column) => (
                      <th key={column}>{column}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {result.rows.map((row, index) => (
                    <tr key={index}>
                      {columns.map((column) => (
                        <td key={column}>{formatCell(row[column])}</td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {!result.rows.length && (
              <p className="hr-notice" role="status">
                No rows for the current filters and scope.
              </p>
            )}
          </div>
          {canExport && (
            <div className="hr-form-section">
              <h2>Current-page export</h2>
              <form onSubmit={(event) => void runExport(event)}>
                <div className="hr-form-grid">
                  <SelectInput
                    label="Format"
                    disabled={exporting}
                    value={exportFormat}
                    onChange={(event) => setExportFormat(event.target.value)}
                    options={exportFormatOptions}
                  />
                </div>
                <label className="hr-field">
                  <span>Reason</span>
                  <textarea
                    rows={3}
                    required
                    maxLength={1000}
                    disabled={exporting}
                    value={exportReason}
                    aria-invalid={Boolean(fieldErrors.reason)}
                    onChange={(event) => setExportReason(event.target.value)}
                  />
                  {fieldErrors.reason && <p className="field-error">{fieldErrors.reason}</p>}
                </label>
                {exportError && (
                  <p className="hr-message hr-message--error" role="alert">
                    {exportError}
                  </p>
                )}
                {exportSuccess && (
                  <p className="hr-message" role="status" style={{ overflowWrap: 'anywhere' }}>
                    {exportSuccess}
                  </p>
                )}
                <div className="hr-actions">
                  <Button type="submit" disabled={exporting || !result.rows.length}>
                    <FileSpreadsheet size={16} />
                    {exporting ? 'Exporting...' : 'Export preview page'}
                  </Button>
                </div>
              </form>
            </div>
          )}
        </>
      )}
      {canExport && (
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
      )}
    </section>
  );
}

function reportPageContext(filter: ReportFilter): string {
  const requested = Object.entries(filter)
    .filter(([key, value]) => key !== 'page' && key !== 'pageSize' && value !== undefined)
    .map(([key, value]) => `${key}: ${value}`)
    .join('; ');
  return (
    `Page ${filter.page} (page size ${filter.pageSize}); requested filters: ` +
    (requested || 'none specified; server defaults apply')
  );
}

function formatCell(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  return String(value);
}
