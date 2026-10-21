import { useEffect, useState } from 'react';
import { z } from 'zod';
import { Download, FilePlus2, Filter, RefreshCw } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { useAttendanceLifetime } from '../../hooks/use-attendance-lifetime';
import {
  Command,
  Denied,
  MutationForm,
  PrivateWorkspace,
  QueryState,
} from '../../components/SchedulingUi';
import { attendanceError, businessToday } from '../../utils/messages';
import { reason } from '../../schemas/common';
import {
  dashboardFor,
  exportJob,
  exportJobFor,
  factualBatchFor,
  reportQuery,
  reportSelection,
  type ExportJob,
  type ReportSelection,
} from './schemas';
import { hasReportGrant } from './access';
import { downloadReport } from './report-download';

const statuses = {
  closed_session_evidence: 'Closed session evidence',
  open_session: 'Open session',
  no_accepted_session_evidence: 'No accepted session evidence',
};
function ExportForm({
  selection,
  onSaved,
  onCancel,
}: {
  selection: ReportSelection;
  onSaved: (job: ExportJob) => void;
  onCancel: () => void;
}) {
  const [source] = useState(selection);
  const [format, setFormat] = useState('1');
  const [inputReason, setReason] = useState('');
  return (
    <MutationForm
      title="Request attendance export"
      submitLabel="Request export"
      onCancel={onCancel}
      onSaved={(value) => onSaved(exportJob.parse(value))}
      build={() => ({
        path: '/attendance/reports/exports',
        method: 'POST',
        schema: exportJob.refine(
          (value) =>
            value.from === source.from && value.to === source.to && value.format === Number(format),
          'Export does not match the submitted selection.',
        ),
        body: {
          ...reportSelection.parse(source),
          format: z.union([z.literal(1), z.literal(2), z.literal(3)]).parse(Number(format)),
          reason: reason().parse(inputReason),
        },
      })}
    >
      <SelectInput
        label="Format"
        value={format}
        onChange={(event) => setFormat(event.target.value)}
        options={[
          { value: '1', label: 'CSV' },
          { value: '2', label: 'Excel' },
          { value: '3', label: 'PDF' },
        ]}
      />
      <TextInput
        label="Reason"
        value={inputReason}
        maxLength={1000}
        required
        onChange={(event) => setReason(event.target.value)}
      />
      <p>
        {source.from} to {source.to} / {source.employeeIds.length} employees
      </p>
    </MutationForm>
  );
}
function ExportStatus({ jobId }: { jobId: string }) {
  const access = useAttendanceAccess();
  const query = useAttendanceQuery(
    `/attendance/reports/exports/${jobId}`,
    exportJobFor(jobId),
    hasReportGrant(access.user, 'attendance.reports.export'),
  );
  const lifetime = useAttendanceLifetime();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  async function download() {
    if (!query.data || busy) return;
    const signal = lifetime();
    setBusy(true);
    setError('');
    try {
      await downloadReport(query.data, signal);
    } catch (failure) {
      if (!signal.aborted) setError(attendanceError(failure));
    } finally {
      if (!signal.aborted) setBusy(false);
    }
  }
  return (
    <section className="attendance-section">
      <div className="attendance-heading">
        <h2>Export job</h2>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={query.isFetching || busy}
          onClick={() => void query.refetch()}
        >
          Reload status
        </Command>
      </div>
      <QueryState query={query} />
      {error && (
        <p className="attendance-error" role="alert">
          {error}
        </p>
      )}
      {query.data && !query.isError && (
        <>
          <dl className="attendance-record">
            <dt>Job</dt>
            <dd>{query.data.id}</dd>
            <dt>State</dt>
            <dd>{query.data.state}</dd>
            <dt>Period</dt>
            <dd>
              {query.data.from} to {query.data.to}
            </dd>
            <dt>Generated</dt>
            <dd>{query.data.generatedAt ?? 'Not generated'}</dd>
            <dt>Download expiry</dt>
            <dd>{query.data.downloadExpiresAt ?? 'Unavailable'}</dd>
            <dt>Failure</dt>
            <dd>{query.data.failureCode ?? 'None recorded'}</dd>
          </dl>
          <Command
            icon={Download}
            disabled={
              busy ||
              query.data.state !== 'completed' ||
              !query.data.downloadExpiresAt ||
              Date.parse(query.data.downloadExpiresAt) <= now
            }
            onClick={() => void download()}
          >
            {busy ? 'Downloading...' : 'Download'}
          </Command>
        </>
      )}
    </section>
  );
}
function ReportResults({
  selection,
  mode,
}: {
  selection: ReportSelection;
  mode: 'register' | 'matrix' | 'totals';
}) {
  const access = useAttendanceAccess();
  const allowed = hasReportGrant(access.user, 'attendance.reports.read');
  const query = useAttendanceQuery(
    '/attendance/reports/factual/batch' + reportQuery(selection),
    factualBatchFor(selection),
    allowed && mode !== 'totals',
  );
  const totals = useAttendanceQuery(
    '/attendance/reports/dashboard' + reportQuery(selection),
    dashboardFor(selection),
    allowed && mode === 'totals',
  );
  if (!allowed) return <Denied />;
  const current = mode === 'totals' ? totals : query;
  const metadata = current.data?.metadata;
  const data = query.data;
  const dates = data ? [...new Set(data.rows.map((row) => row.businessDate))].sort() : [];
  const cells = new Map(
    data?.rows.map((row) => [row.employeeId.toLowerCase() + ':' + row.businessDate, row]),
  );
  return (
    <section className="attendance-section">
      <div className="attendance-heading">
        <h2>Factual attendance</h2>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={current.isFetching}
          onClick={() => void current.refetch()}
        >
          Reload
        </Command>
      </div>
      <QueryState query={current} />
      {metadata && !current.isError && (
        <dl className="attendance-record">
          <dt>Generated UTC</dt>
          <dd>{metadata.generatedAtUtc}</dd>
          <dt>Business timezone</dt>
          <dd>{metadata.timeZone}</dd>
          <dt>Period</dt>
          <dd>
            {metadata.from} to {metadata.to}
          </dd>
          <dt>Coverage</dt>
          <dd>{metadata.employeeIds.length} selected employees</dd>
          <dt>Lock state</dt>
          <dd>
            {
              { all_locked: 'All locked', all_unlocked: 'All unlocked', mixed: 'Mixed' }[
                metadata.lockStatus
              ]
            }
          </dd>
          <dt>Policy status</dt>
          <dd>Factual evidence only</dd>
        </dl>
      )}
      {mode === 'totals' && totals.data && !totals.isError && (
        <>
          <dl className="attendance-record">
            <dt>Employee days</dt>
            <dd>{totals.data.totals.employeeDays}</dd>
            <dt>Closed evidence days</dt>
            <dd>{totals.data.totals.closedSessionEvidenceDays}</dd>
            <dt>Open session days</dt>
            <dd>{totals.data.totals.openSessionDays}</dd>
            <dt>No accepted evidence days</dt>
            <dd>{totals.data.totals.noAcceptedSessionEvidenceDays}</dd>
            <dt>Closed worked seconds</dt>
            <dd>{totals.data.totals.workedSeconds}</dd>
            <dt>Sum of daily worked minutes</dt>
            <dd>{totals.data.totals.sumDailyWorkedMinutes}</dd>
            <dt>Exceptions</dt>
            <dd>{totals.data.totals.exceptionCount}</dd>
            <dt>Current saved summary days</dt>
            <dd>{totals.data.totals.currentSavedSummaryDays}</dd>
            <dt>Unsaved or stale summary days</dt>
            <dd>{totals.data.totals.unsavedOrStaleSummaryDays}</dd>
          </dl>
          <div className="hr-table-scroll">
            <table className="attendance-table">
              <thead>
                <tr>
                  <th>Employee</th>
                  <th>Days</th>
                  <th>Closed worked seconds</th>
                  <th>Open sessions</th>
                  <th>Exceptions</th>
                  <th>Locked days</th>
                </tr>
              </thead>
              <tbody>
                {totals.data.employees.map((row) => (
                  <tr key={row.employeeId}>
                    <td>{row.employeeId}</td>
                    <td>{row.totals.employeeDays}</td>
                    <td>{row.totals.workedSeconds}</td>
                    <td>{row.totals.openSessions}</td>
                    <td>{row.totals.exceptionCount}</td>
                    <td>{row.totals.lockedDays}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
      {mode === 'register' && data && !query.isError && (
        <div className="hr-table-scroll">
          <table className="attendance-table">
            <thead>
              <tr>
                <th>Employee / date</th>
                <th>Evidence status</th>
                <th>Closed worked minutes</th>
                <th>Sessions / open</th>
                <th>Exceptions</th>
                <th>Lock / summary</th>
              </tr>
            </thead>
            <tbody>
              {data.rows.map((row) => (
                <tr key={row.employeeId + row.businessDate}>
                  <td>
                    {row.employeeId}
                    <br />
                    {row.businessDate}
                  </td>
                  <td>{statuses[row.factualStatus]}</td>
                  <td>{row.workedMinutes}</td>
                  <td>
                    {row.sessionCount} / {row.openSessions}
                  </td>
                  <td>{row.exceptionCount}</td>
                  <td>
                    {row.isLocked ? 'Locked' : 'Unlocked'}
                    <br />
                    {row.summaryRevision
                      ? `Revision ${row.summaryRevision}`
                      : 'No current saved summary'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {mode === 'matrix' && data && !query.isError && (
        <div className="hr-table-scroll">
          <table className="attendance-table attendance-matrix">
            <thead>
              <tr>
                <th>Employee</th>
                {dates.map((value) => (
                  <th key={value}>{value.slice(5)}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {selection.employeeIds.map((employee) => (
                <tr key={employee}>
                  <th>{employee}</th>
                  {dates.map((value) => {
                    const row = cells.get(employee.toLowerCase() + ':' + value)!;
                    return (
                      <td key={value}>
                        <span>
                          {row.factualStatus === 'closed_session_evidence'
                            ? 'Closed'
                            : row.factualStatus === 'open_session'
                              ? 'Open'
                              : 'No accepted evidence'}
                        </span>
                        <br />
                        {row.workedMinutes} min
                        <br />
                        {row.isLocked ? 'Locked' : 'Unlocked'}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
function ReportsContent() {
  const access = useAttendanceAccess();
  const canRead = hasReportGrant(access.user, 'attendance.reports.read');
  const canExport = hasReportGrant(access.user, 'attendance.reports.export');
  const [employees, setEmployees] = useState('');
  const [from, setFrom] = useState(businessToday());
  const [to, setTo] = useState(businessToday());
  const [selection, setSelection] = useState<ReportSelection | null>(null);
  const [error, setError] = useState('');
  const [mode, setMode] = useState<'register' | 'matrix' | 'totals'>('register');
  const [creating, setCreating] = useState(false);
  const [jobId, setJobId] = useState('');
  if (!canRead && !canExport) return <Denied />;
  function load() {
    try {
      setSelection(
        reportSelection.parse({
          employeeIds: employees
            .trim()
            .split(/[\s,]+/)
            .filter(Boolean),
          from,
          to,
        }),
      );
      setError('');
    } catch (failure) {
      setError(
        failure instanceof z.ZodError
          ? failure.issues.map((issue) => issue.message).join(' ')
          : 'Check the selection.',
      );
    }
  }
  return (
    <>
      <h1>Attendance Reports</h1>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          load();
        }}
      >
        <fieldset disabled={creating}>
          <label className="field">
            Employee IDs
            <textarea
              value={employees}
              maxLength={2000}
              rows={3}
              required
              onChange={(event) => setEmployees(event.target.value)}
            />
          </label>
          <div className="attendance-grid">
            <TextInput
              label="From"
              type="date"
              value={from}
              required
              onChange={(event) => setFrom(event.target.value)}
            />
            <TextInput
              label="To"
              type="date"
              value={to}
              required
              onChange={(event) => setTo(event.target.value)}
            />
          </div>
          <Command icon={Filter} type="submit">
            Load selection
          </Command>
        </fieldset>
      </form>
      {error && (
        <p className="attendance-error" role="alert">
          {error}
        </p>
      )}
      {selection && (
        <>
          <div className="attendance-tabs" role="tablist" aria-label="Attendance report view">
            {(['register', 'matrix', 'totals'] as const).map((value) => (
              <button
                key={value}
                type="button"
                role="tab"
                aria-selected={mode === value}
                id={`attendance-report-tab-${value}`}
                aria-controls="attendance-report-panel"
                tabIndex={mode === value ? 0 : -1}
                onKeyDown={(event) => {
                  const tabs = ['register', 'matrix', 'totals'] as const;
                  const index = tabs.indexOf(value);
                  const next =
                    event.key === 'Home'
                      ? 0
                      : event.key === 'End'
                        ? 2
                        : event.key === 'ArrowRight'
                          ? (index + 1) % 3
                          : event.key === 'ArrowLeft'
                            ? (index + 2) % 3
                            : -1;
                  if (next < 0) return;
                  event.preventDefault();
                  const target = tabs[next]!;
                  setMode(target);
                  document.getElementById(`attendance-report-tab-${target}`)?.focus();
                }}
                onClick={() => setMode(value)}
              >
                {value === 'register' ? 'Register' : value === 'matrix' ? 'Day matrix' : 'Totals'}
              </button>
            ))}
          </div>
          <div
            id="attendance-report-panel"
            role="tabpanel"
            aria-labelledby={`attendance-report-tab-${mode}`}
            tabIndex={0}
          >
            <ReportResults selection={selection} mode={mode} />
          </div>
          {canExport && !creating && (
            <Command icon={FilePlus2} onClick={() => setCreating(true)}>
              Request export
            </Command>
          )}
          {creating && (
            <ExportForm
              key={JSON.stringify(selection)}
              selection={selection}
              onCancel={() => setCreating(false)}
              onSaved={(job) => {
                setJobId(job.id);
                setCreating(false);
              }}
            />
          )}
        </>
      )}
      {jobId && canExport && <ExportStatus key={jobId} jobId={jobId} />}
    </>
  );
}
export function AttendanceReportsPage() {
  return (
    <PrivateWorkspace resource="attendance-reports">
      <ReportsContent />
    </PrivateWorkspace>
  );
}
