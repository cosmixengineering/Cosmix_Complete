import { useEffect, useLayoutEffect, useRef, useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Download, FilePlus2, RefreshCw } from 'lucide-react';
import { SelectInput } from '@/components/forms/SelectInput';
import { TextInput } from '@/components/forms/TextInput';
import { Button } from '@/components/ui/Button';
import { ApiError } from '@/services/api/api-error';
import {
  canDownloadReportExport,
  createReportExportIntent,
  downloadReportExport,
  fetchReportExportJob,
  isValidReportExportReason,
  reportExportError,
  reportExportFailure,
  requestReportExport,
  type ReportExportIntent,
  type ReportExportJob,
  type ReportExportSelection,
} from '../api/report-export-jobs';

export function HrReportExportJobs({
  selection,
  subject,
}: {
  selection: ReportExportSelection | null;
  subject: string;
}) {
  const [format, setFormat] = useState('2');
  const [reason, setReason] = useState('');
  const [intent, setIntent] = useState<ReportExportIntent | null>(null);
  const [created, setCreated] = useState<
    { job: ReportExportJob; selection: ReportExportSelection }[]
  >([]);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const [reasonError, setReasonError] = useState('');
  const active = useRef(false);
  const lifetime = useRef<AbortController | null>(null);
  useLayoutEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    return () => controller.abort();
  }, []);

  async function submit(event: FormEvent) {
    event.preventDefault();
    const signal = lifetime.current?.signal;
    if (!signal || signal.aborted || active.current || (!intent && !selection)) return;
    let current = intent;
    if (!current) {
      if (!isValidReportExportReason(reason)) {
        setReasonError('Enter a single-line audit reason of 1 to 1000 characters.');
        return;
      }
      current = createReportExportIntent(selection!, Number(format), reason);
    }
    active.current = true;
    setPending(true);
    setIntent(current);
    setError('');
    setReasonError('');
    try {
      const job = await requestReportExport(current, signal);
      if (signal.aborted) return;
      setCreated((previous) => [
        { job, selection: current.selection },
        ...previous.filter((entry) => entry.job.id !== job.id),
      ]);
      setIntent(null);
      setReason('');
    } catch (failure) {
      if (signal.aborted) return;
      const uncertain =
        !(failure instanceof ApiError) || failure.status >= 500 || failure.status === 408;
      if (!uncertain) setIntent(null);
      setError(reportExportError(failure));
    } finally {
      active.current = false;
      if (!signal.aborted) setPending(false);
    }
  }

  const source = intent?.selection ?? selection;
  return (
    <>
      {source && (
        <section className="hr-form-section" aria-label="Full filtered export">
          <h2>Full filtered export</h2>
          {source && (
            <form onSubmit={(event) => void submit(event)}>
              <p className="muted" style={{ overflowWrap: 'anywhere' }}>
                {source.title} &middot; All matching rows
              </p>
              <p className="muted" style={{ overflowWrap: 'anywhere' }}>
                {filterContext(source)}
              </p>
              <div className="hr-form-grid">
                <SelectInput
                  label="Full export format"
                  value={format}
                  disabled={pending || intent !== null}
                  onChange={(event) => setFormat(event.target.value)}
                  options={[
                    { value: '1', label: 'CSV' },
                    { value: '2', label: 'XLSX (Excel)' },
                    { value: '3', label: 'PDF' },
                  ]}
                />
                <TextInput
                  label="Full export reason"
                  required
                  maxLength={1000}
                  value={reason}
                  disabled={pending || intent !== null}
                  error={reasonError}
                  onChange={(event) => setReason(event.target.value)}
                />
              </div>
              {error && (
                <p className="hr-message hr-message--error" role="alert">
                  {error}
                </p>
              )}
              {intent && !pending && (
                <p className="hr-notice" role="status">
                  Request outcome unconfirmed. Retry retains the original filters, format, reason
                  and request key.
                </p>
              )}
              <div className="hr-actions">
                <Button type="submit" disabled={pending}>
                  {intent ? (
                    <RefreshCw size={16} aria-hidden="true" />
                  ) : (
                    <FilePlus2 size={16} aria-hidden="true" />
                  )}
                  {pending
                    ? 'Requesting...'
                    : intent
                      ? 'Retry unchanged request'
                      : 'Request full export'}
                </Button>
              </div>
            </form>
          )}
        </section>
      )}
      {created.map((entry) => (
        <ExportJobStatus
          key={entry.job.id}
          initial={entry.job}
          selection={entry.selection}
          subject={subject}
        />
      ))}
    </>
  );
}

function ExportJobStatus({
  initial,
  selection,
  subject,
}: {
  initial: ReportExportJob;
  selection: ReportExportSelection;
  subject: string;
}) {
  const query = useQuery({
    queryKey: ['reports', subject, 'hr-export', initial.id],
    queryFn: ({ signal }) => fetchReportExportJob(initial, signal),
    initialData: initial,
    staleTime: 0,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchInterval: (current) =>
      current.state.status !== 'error' &&
      (current.state.data?.state === 'pending' || current.state.data?.state === 'running')
        ? 3000
        : false,
    refetchIntervalInBackground: false,
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [downloaded, setDownloaded] = useState(false);
  const [now, setNow] = useState(Date.now);
  const active = useRef(false);
  const lifetime = useRef<AbortController | null>(null);
  useLayoutEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    return () => controller.abort();
  }, []);
  const job = query.isError ? null : query.data;
  useEffect(() => {
    if (!job?.downloadExpiresAt) return;
    const timer = window.setTimeout(
      () => setNow(Date.now()),
      Math.max(0, Date.parse(job.downloadExpiresAt) - Date.now()),
    );
    return () => window.clearTimeout(timer);
  }, [job?.downloadExpiresAt]);
  async function download() {
    const signal = lifetime.current?.signal;
    if (
      !job ||
      query.isFetching ||
      active.current ||
      !signal ||
      signal.aborted ||
      !canDownloadReportExport(job)
    )
      return;
    active.current = true;
    setBusy(true);
    setError('');
    setDownloaded(false);
    try {
      await downloadReportExport(job, signal);
      if (!signal.aborted) setDownloaded(true);
    } catch (failure) {
      if (!signal.aborted) setError(reportExportError(failure));
    } finally {
      active.current = false;
      if (!signal.aborted) setBusy(false);
    }
  }
  return (
    <section className="hr-form-section" aria-label="Export job">
      <div className="hr-repeat-heading">
        <h2>Export job</h2>
        <Button
          variant="secondary"
          disabled={query.isFetching || busy}
          onClick={() => {
            setError('');
            setDownloaded(false);
            void query.refetch();
          }}
        >
          <RefreshCw size={16} aria-hidden="true" />
          {query.isFetching ? 'Checking...' : 'Refresh status'}
        </Button>
      </div>
      {query.isError && (
        <p className="hr-message hr-message--error" role="alert">
          {reportExportError(query.error)} Status checks paused.
        </p>
      )}
      {job && (
        <>
          <p style={{ overflowWrap: 'anywhere' }}>
            {selection.title} &middot; {filterContext(selection)}
          </p>
          <dl className="hr-form-grid" style={{ overflowWrap: 'anywhere' }}>
            <div>
              <dt>Job</dt>
              <dd>{job.id}</dd>
            </div>
            <div>
              <dt>Status</dt>
              <dd role="status">
                {
                  {
                    pending: 'Queued',
                    running: 'Processing',
                    completed: 'Completed',
                    failed: 'Failed',
                  }[job.state]
                }
              </dd>
            </div>
            <div>
              <dt>Format</dt>
              <dd>{{ 1: 'CSV', 2: 'XLSX', 3: 'PDF' }[job.format]}</dd>
            </div>
            <div>
              <dt>Requested</dt>
              <dd>{new Date(job.requestedAt).toLocaleString()}</dd>
            </div>
            {job.state === 'completed' && (
              <>
                <div>
                  <dt>Rows</dt>
                  <dd>
                    {job.returnedCount} of {job.totalCount} matching
                  </dd>
                </div>
                <div>
                  <dt>Generated</dt>
                  <dd>{new Date(job.generatedAt!).toLocaleString()}</dd>
                </div>
                <div>
                  <dt>Download expires</dt>
                  <dd>{new Date(job.downloadExpiresAt!).toLocaleString()}</dd>
                </div>
              </>
            )}
          </dl>
          {job.state === 'failed' && (
            <p className="hr-message hr-message--error" role="alert">
              {reportExportFailure(job.failureCode)}
            </p>
          )}
          {job.state === 'completed' && !canDownloadReportExport(job, now) && (
            <p className="hr-notice" role="status">
              Download expired. Request a new export.
            </p>
          )}
          <div className="hr-actions">
            <Button
              disabled={busy || query.isFetching || !canDownloadReportExport(job, now)}
              onClick={() => void download()}
            >
              <Download size={16} aria-hidden="true" />
              {busy ? 'Downloading...' : 'Download full export'}
            </Button>
          </div>
          {downloaded && (
            <p className="hr-message" role="status">
              Download started.
            </p>
          )}
        </>
      )}
      {error && (
        <p className="hr-message hr-message--error" role="alert">
          {error}
        </p>
      )}
    </section>
  );
}

function filterContext(selection: ReportExportSelection) {
  return (
    Object.entries(selection.filter)
      .filter(([key, value]) => key !== 'page' && key !== 'pageSize' && value !== undefined)
      .map(([key, value]) => `${key}: ${value}`)
      .join('; ') || 'Server defaults'
  );
}
