import { useState } from 'react';
import { Link } from 'react-router-dom';
import { FilePenLine, FileText, Filter, RefreshCw } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Command, Denied, PrivateWorkspace, QueryState } from '../components/SchedulingUi';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { hasReportGrant } from '../operations/reports/access';
import {
  dashboardFor,
  factualBatchFor,
  reportQuery,
  reportSelection,
  type ReportSelection,
} from '../operations/reports/schemas';
import { businessToday } from '../utils/messages';

const evidenceLabels = {
  closed_session_evidence: 'Closed session evidence',
  open_session: 'Open session',
  no_accepted_session_evidence: 'No accepted session evidence',
} as const;

function FactualResults({ selection }: { selection: ReportSelection }) {
  const access = useAttendanceAccess();
  const allowed = hasReportGrant(access.user, 'attendance.reports.read');
  const canCorrect = hasReportGrant(access.user, 'attendance.corrections.submit');
  const [view, setView] = useState<'register' | 'totals'>('register');
  const filter = reportQuery(selection);
  const register = useAttendanceQuery(
    '/attendance/reports/factual/batch' + filter,
    factualBatchFor(selection),
    allowed && view === 'register',
  );
  const dashboard = useAttendanceQuery(
    '/attendance/reports/dashboard' + filter,
    dashboardFor(selection),
    allowed && view === 'totals',
  );
  const current = view === 'register' ? register : dashboard;
  // A failed or refreshing request must not present retained rows as current evidence.
  const metadata = current.isSuccess && !current.isFetching ? current.data.metadata : null;
  const batch = register.isSuccess && !register.isFetching ? register.data : null;
  const totals = dashboard.isSuccess && !dashboard.isFetching ? dashboard.data.totals : null;
  if (!allowed) return <Denied>Factual attendance report access is unavailable.</Denied>;

  return (
    <section className="attendance-section" aria-label="Factual attendance results">
      <div className="attendance-heading">
        <h2>Factual attendance</h2>
        <div className="attendance-commands">
          <Command
            icon={RefreshCw}
            variant="secondary"
            disabled={current.isFetching}
            onClick={() => void current.refetch()}
          >
            Reload
          </Command>
        </div>
      </div>
      <div className="attendance-tabs" role="tablist" aria-label="Factual attendance view">
        {(['register', 'totals'] as const).map((mode) => (
          <button
            key={mode}
            type="button"
            role="tab"
            id={'attendance-processing-tab-' + mode}
            aria-controls="attendance-processing-results"
            aria-selected={view === mode}
            tabIndex={view === mode ? 0 : -1}
            onClick={() => setView(mode)}
            onKeyDown={(event) => {
              const next =
                event.key === 'Home'
                  ? 'register'
                  : event.key === 'End'
                    ? 'totals'
                    : event.key === 'ArrowLeft' || event.key === 'ArrowRight'
                      ? mode === 'register'
                        ? 'totals'
                        : 'register'
                      : null;
              if (!next) return;
              event.preventDefault();
              setView(next);
              document.getElementById('attendance-processing-tab-' + next)?.focus();
            }}
          >
            {mode === 'register' ? 'Register' : 'Totals'}
          </button>
        ))}
      </div>
      <div
        id="attendance-processing-results"
        role="tabpanel"
        aria-labelledby={'attendance-processing-tab-' + view}
        tabIndex={0}
      >
        <QueryState query={current} />
        {metadata && (
          <dl className="attendance-record">
            <dt>Business date</dt>
            <dd>{metadata.from}</dd>
            <dt>Business timezone</dt>
            <dd>{metadata.timeZone}</dd>
            <dt>Coverage</dt>
            <dd>{metadata.employeeIds.length} selected employees</dd>
            <dt>Generated UTC</dt>
            <dd>{metadata.generatedAtUtc}</dd>
            <dt>Lock state</dt>
            <dd>
              {
                { all_locked: 'All locked', all_unlocked: 'All unlocked', mixed: 'Mixed' }[
                  metadata.lockStatus
                ]
              }
            </dd>
            <dt>Policy status</dt>
            <dd>Factual evidence only; payroll calculation unavailable</dd>
          </dl>
        )}
        {view === 'register' && batch && !current.isError && (
          <>
            {batch.rows.every((row) => row.factualStatus === 'no_accepted_session_evidence') && (
              <p className="attendance-empty" role="status">
                No accepted session evidence for this selection.
              </p>
            )}
            <div className="hr-table-scroll">
              <table className="attendance-table">
                <thead>
                  <tr>
                    <th scope="col">Employee ID</th>
                    <th scope="col">Evidence status</th>
                    <th scope="col">Closed worked minutes</th>
                    <th scope="col">Sessions / open</th>
                    <th scope="col">Exceptions</th>
                    <th scope="col">Lock / saved summary</th>
                    {canCorrect && <th scope="col">Corrections</th>}
                  </tr>
                </thead>
                <tbody>
                  {batch.rows.map((row) => (
                    <tr key={row.employeeId + ':' + row.businessDate}>
                      <th scope="row" style={{ overflowWrap: 'anywhere' }}>
                        <Link to={'/hr/attendance/employees/' + row.employeeId}>
                          {row.employeeId}
                        </Link>
                      </th>
                      <td>{evidenceLabels[row.factualStatus]}</td>
                      <td>{row.workedMinutes}</td>
                      <td>
                        {row.sessionCount} / {row.openSessions}
                      </td>
                      <td>{row.exceptionCount}</td>
                      <td>
                        {row.isLocked ? 'Locked' : 'Unlocked'}
                        <br />
                        {row.summaryRevision === null
                          ? 'No current saved summary'
                          : 'Revision ' + row.summaryRevision}
                      </td>
                      {canCorrect && (
                        <td>
                          <Link to={'/hr/attendance/employees/' + row.employeeId + '/corrections'}>
                            Corrections
                          </Link>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
        {view === 'totals' && totals && !current.isError && (
          <>
            {totals.noAcceptedSessionEvidenceDays === totals.employeeDays && (
              <p className="attendance-empty" role="status">
                No accepted session evidence for this selection.
              </p>
            )}
            <dl className="attendance-record">
              <dt>Employee days</dt>
              <dd>{totals.employeeDays}</dd>
              <dt>Closed session evidence days</dt>
              <dd>{totals.closedSessionEvidenceDays}</dd>
              <dt>Open session days</dt>
              <dd>{totals.openSessionDays}</dd>
              <dt>No accepted session evidence days</dt>
              <dd>{totals.noAcceptedSessionEvidenceDays}</dd>
              <dt>Closed worked seconds</dt>
              <dd>{totals.workedSeconds}</dd>
              <dt>Sum of daily worked minutes</dt>
              <dd>{totals.sumDailyWorkedMinutes}</dd>
              <dt>Closed sessions</dt>
              <dd>{totals.closedSessions}</dd>
              <dt>Open sessions</dt>
              <dd>{totals.openSessions}</dd>
              <dt>Exceptions</dt>
              <dd>{totals.exceptionCount}</dd>
              <dt>Days with exceptions</dt>
              <dd>{totals.daysWithExceptions}</dd>
              <dt>Locked / unlocked days</dt>
              <dd>
                {totals.lockedDays} / {totals.unlockedDays}
              </dd>
              <dt>Current saved summary days</dt>
              <dd>{totals.currentSavedSummaryDays}</dd>
              <dt>Unsaved or stale summary days</dt>
              <dd>{totals.unsavedOrStaleSummaryDays}</dd>
            </dl>
          </>
        )}
      </div>
    </section>
  );
}

function ProcessingContent() {
  const access = useAttendanceAccess();
  const canRead = hasReportGrant(access.user, 'attendance.reports.read');
  const canExport = hasReportGrant(access.user, 'attendance.reports.export');
  const canCorrect = hasReportGrant(access.user, 'attendance.corrections.submit');
  const canReview = hasReportGrant(access.user, 'attendance.corrections.review');
  const [employees, setEmployees] = useState('');
  const [businessDate, setBusinessDate] = useState(() => businessToday());
  const [selection, setSelection] = useState<ReportSelection | null>(null);
  const [validation, setValidation] = useState('');

  function load() {
    const result = reportSelection.safeParse({
      employeeIds: employees
        .trim()
        .split(/[\s,]+/)
        .filter(Boolean),
      from: businessDate,
      to: businessDate,
    });
    if (!result.success) {
      setSelection(null);
      setValidation('A valid business date and 1-25 unique employee IDs are required.');
      return;
    }
    setValidation('');
    setSelection(result.data);
  }

  return (
    <>
      <div className="attendance-heading">
        <h1>Attendance Processing</h1>
        <nav className="attendance-commands" aria-label="Attendance workspaces">
          {canCorrect && (
            <Link to="/hr/attendance/corrections">
              <FilePenLine size={16} aria-hidden="true" /> Corrections
            </Link>
          )}
          {canReview && (
            <Link to="/hr/attendance/corrections/review">
              <FileText size={16} aria-hidden="true" /> Correction review
            </Link>
          )}
          {(canRead || canExport) && (
            <Link to="/hr/attendance/reports">
              <FileText size={16} aria-hidden="true" /> Reports{canExport ? ' and exports' : ''}
            </Link>
          )}
        </nav>
      </div>
      {!canRead ? (
        <Denied>Factual attendance report access is unavailable.</Denied>
      ) : (
        <>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              load();
            }}
          >
            <div className="attendance-grid">
              <TextInput
                label="Business date (Asia/Karachi)"
                type="date"
                value={businessDate}
                required
                onChange={(event) => {
                  setBusinessDate(event.target.value);
                  setSelection(null);
                  setValidation('');
                }}
              />
              <label className="field">
                Employee IDs (1-25)
                <textarea
                  value={employees}
                  maxLength={2000}
                  rows={3}
                  required
                  spellCheck={false}
                  onChange={(event) => {
                    setEmployees(event.target.value);
                    setSelection(null);
                    setValidation('');
                  }}
                />
              </label>
            </div>
            <Command icon={Filter} type="submit">
              Load attendance
            </Command>
          </form>
          {validation && (
            <p className="attendance-error" role="alert">
              {validation}
            </p>
          )}
          {selection ? (
            <FactualResults key={reportQuery(selection)} selection={selection} />
          ) : (
            <p className="attendance-empty" role="status">
              No employee/date selection loaded.
            </p>
          )}
        </>
      )}
    </>
  );
}

export function AttendanceProcessingPage() {
  return (
    <PrivateWorkspace resource="attendance-processing">
      <ProcessingContent />
    </PrivateWorkspace>
  );
}
