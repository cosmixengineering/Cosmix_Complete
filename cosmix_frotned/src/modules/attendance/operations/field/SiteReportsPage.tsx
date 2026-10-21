import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { ClipboardList, MessageSquare, Search, ShieldCheck, X } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { queryString } from '../../api/attendance-api';
import { Command, Denied, QueryState } from '../../components/SchedulingUi';
import { id } from '../../schemas/common';
import { fieldPermission } from './access';
import {
  reportDetail,
  reportFilter,
  reportHeadAvailable,
  reportReviewAllowed,
  reportsPage,
  returnedReview,
} from './contracts';
import { RecordFields, Reload, SiteWorkspace } from './FieldUi';
import { EvidenceDownload } from './Evidence';
import { ReportDecisionForm, ReportResponseForm } from './ReportForms';
import { FieldWorkspace } from './FieldWorkspace';

function ReportDetailWorkspace({
  reportId,
  siteId,
  own,
  onClose,
}: {
  reportId: string;
  siteId?: string;
  own: boolean;
  onClose: () => void;
}) {
  const access = useAttendanceAccess();
  const canRead = fieldPermission(
    access.user,
    own ? 'attendance.site-reports.self' : 'attendance.site-reports.read',
    siteId,
  );
  const canDecide = !own && fieldPermission(access.user, 'attendance.site-reports.decide', siteId);
  const base = own ? `/site-reports/mine/${reportId}` : `/site-reports/sites/${siteId}/${reportId}`;
  const query = useAttendanceQuery(base, reportDetail, canRead && id.safeParse(reportId).success);
  const [edit, setEdit] = useState(false);
  function close() {
    setEdit(false);
    void query.refetch();
  }
  const detail = query.data;
  const bound =
    detail &&
    detail.report.id === reportId &&
    (own
      ? detail.report.employeeId === access.user?.account?.employeeId
      : detail.report.siteId === siteId);
  return (
    <section className="attendance-section">
      <div className="attendance-heading">
        <h2>Site Report</h2>
        <div className="attendance-commands">
          <Reload
            disabled={edit || query.isFetching}
            onClick={() => {
              void query.refetch();
            }}
          />
          <Command icon={X} variant="secondary" disabled={edit} onClick={onClose}>
            Close report
          </Command>
        </div>
      </div>
      <QueryState query={query} />
      {detail && !query.isError && !bound && (
        <p role="alert">Report identity does not match the requested resource.</p>
      )}
      {detail && !query.isError && bound && (
        <>
          <RecordFields
            values={{
              Report: reportId,
              Employee: detail.report.employeeId,
              Site: detail.report.siteId,
              Kind:
                ['', 'Arrival', 'Departure', 'Delay', 'Access issue', 'Incident'][
                  detail.report.kind
                ] ?? '',
              Received: detail.report.receivedAtUtc,
              'Device capture': detail.report.deviceCapturedAtUtc,
              Offline: detail.report.isOffline,
              Task: detail.report.taskReference,
              Status: detail.report.submissionStatus,
              Evidence: detail.report.evidenceStatus,
              Workflow: detail.reviewWorkflowStatus,
              Latitude: detail.latitude,
              Longitude: detail.longitude,
              'Accuracy (m)': detail.accuracyMeters,
            }}
          />
          <p style={{ whiteSpace: 'pre-wrap' }}>{detail.notes}</p>
          {detail.report.hasAttachment && (
            <EvidenceDownload path={base + '/evidence'} name={`site-report-${reportId}`} />
          )}
          <h3>Review history</h3>
          {detail.reviews.length === 0 && <p>No reviews.</p>}
          {detail.reviews.map((review) => (
            <section className="attendance-section" key={review.sequence}>
              <RecordFields
                values={{
                  Sequence: review.sequence,
                  Outcome:
                    ['', 'Accepted', 'Returned', 'Rejected', 'Historical escalation'][
                      review.outcome
                    ] ?? '',
                  Reviewer: review.reviewer,
                  Role: review.reviewerRole,
                  Reason: review.reason,
                  Reviewed: review.reviewedAtUtc,
                }}
              />
            </section>
          ))}
          <h3>Employee response history</h3>
          {(detail.responses ?? []).length === 0 && <p>No responses.</p>}
          {(detail.responses ?? []).map((response) => (
            <section className="attendance-section" key={response.id}>
              <RecordFields
                values={{
                  Sequence: response.sequence,
                  'Returned decision': response.returnedReviewId,
                  Submitted: response.submittedAtUtc,
                }}
              />
              <p style={{ whiteSpace: 'pre-wrap' }}>{response.writtenResponse}</p>
              {response.hasAttachment && (
                <EvidenceDownload
                  path={`${base}/responses/${response.id}/evidence`}
                  name={`site-report-response-${response.id}`}
                />
              )}
            </section>
          ))}
          {!reportHeadAvailable(detail) && (
            <p role="status">Complete review history is unavailable. Reload before an action.</p>
          )}
          {own && returnedReview(detail) && (
            <Command
              icon={MessageSquare}
              disabled={edit || query.isFetching}
              onClick={() => setEdit(true)}
            >
              Answer and resubmit
            </Command>
          )}
          {canDecide && (
            <Command
              icon={ShieldCheck}
              disabled={
                edit ||
                query.isFetching ||
                !reportReviewAllowed(detail, access.user?.account?.employeeId)
              }
              onClick={() => setEdit(true)}
            >
              Review decision
            </Command>
          )}
          {edit &&
            (own ? (
              <ReportResponseForm detail={detail} close={close} />
            ) : (
              siteId && <ReportDecisionForm detail={detail} siteId={siteId} close={close} />
            ))}
        </>
      )}
    </section>
  );
}

function ReportsWorkspace({ siteId, own = false }: { siteId?: string; own?: boolean }) {
  const access = useAttendanceAccess(),
    params = useParams();
  const allowed = fieldPermission(
    access.user,
    own ? 'attendance.site-reports.self' : 'attendance.site-reports.read',
    siteId,
  );
  const [from, setFrom] = useState(''),
    [to, setTo] = useState('');
  const [filters, setFilters] = useState<{ from: string; to: string }>(),
    [page, setPage] = useState(1),
    [selected, setSelected] = useState(''),
    [error, setError] = useState('');
  const reportId = params.reportId ?? selected;
  const validDetail = id.safeParse(reportId).success;
  const path =
    (own ? '/site-reports/mine' : '/site-reports/queue') +
    queryString({ ...filters, siteId: own ? undefined : siteId, page, pageSize: 25 });
  const query = useAttendanceQuery(path, reportsPage, allowed && Boolean(filters) && !validDetail);
  if (!allowed) return <Denied>Site reports require the matching own or site read grant.</Denied>;
  return (
    <>
      <h1>{own ? 'My Site Reports' : 'HR Site Report Queue'}</h1>
      {own && (
        <section className="attendance-section">
          <Command icon={ClipboardList} disabled>
            Submit site report
          </Command>
          <p role="status">
            Initial intake requires your registered mobile device. Web intake is unavailable.
          </p>
        </section>
      )}
      {reportId ? (
        validDetail ? (
          <ReportDetailWorkspace
            key={reportId}
            reportId={reportId}
            siteId={siteId}
            own={own}
            onClose={() => {
              setSelected('');
              if (!params.reportId && filters) void query.refetch();
            }}
          />
        ) : (
          <p role="alert">Invalid report identifier.</p>
        )
      ) : (
        <>
          <form
            className="attendance-commands"
            onSubmit={(e) => {
              e.preventDefault();
              const parsed = reportFilter.safeParse({ from, to });
              if (!parsed.success) {
                setError(parsed.error.issues.map((v) => v.message).join('; '));
                return;
              }
              setError('');
              setPage(1);
              if (filters?.from === from && filters.to === to && page === 1) void query.refetch();
              else setFilters(parsed.data);
            }}
          >
            <TextInput
              label="Received from (Asia/Karachi)"
              type="date"
              required
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
            <TextInput
              label="Received to (inclusive)"
              type="date"
              required
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
            <Command icon={Search} type="submit" disabled={query.isFetching}>
              Read reports
            </Command>
          </form>
          {error && (
            <p role="alert" className="attendance-error">
              {error}
            </p>
          )}
          {filters && (
            <>
              <Reload
                disabled={query.isFetching}
                onClick={() => {
                  void query.refetch();
                }}
              />
              <QueryState query={query} empty={query.data?.items.length === 0} />
              {query.data && !query.isError && (
                <>
                  <div style={{ overflowX: 'auto' }}>
                    <table className="attendance-table">
                      <thead>
                        <tr>
                          <th>Received</th>
                          <th>Employee / site</th>
                          <th>Report</th>
                          <th>Status / evidence</th>
                          <th>History</th>
                        </tr>
                      </thead>
                      <tbody>
                        {query.data.items.map((r) => (
                          <tr key={r.id}>
                            <td>{r.receivedAtUtc}</td>
                            <td>
                              {r.employeeId}
                              <br />
                              {r.siteId}
                            </td>
                            <td>
                              {
                                ['', 'Arrival', 'Departure', 'Delay', 'Access issue', 'Incident'][
                                  r.kind
                                ]
                              }
                              <br />
                              {r.taskReference}
                            </td>
                            <td>
                              {r.submissionStatus}
                              <br />
                              {r.evidenceStatus}
                            </td>
                            <td>
                              <Command
                                icon={ClipboardList}
                                variant="secondary"
                                disabled={query.isFetching}
                                onClick={() => setSelected(r.id)}
                              >
                                Open report
                              </Command>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  <Pagination
                    page={query.data.page}
                    pageSize={query.data.pageSize}
                    total={query.data.totalCount}
                    onChange={setPage}
                    disabled={query.isFetching}
                  />
                </>
              )}
            </>
          )}
        </>
      )}
    </>
  );
}

export function MySiteReportsPage() {
  return (
    <FieldWorkspace>
      <ReportsWorkspace own />
    </FieldWorkspace>
  );
}
export function SiteReportQueuePage() {
  return <SiteWorkspace>{(siteId) => <ReportsWorkspace siteId={siteId} />}</SiteWorkspace>;
}
