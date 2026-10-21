import { useState } from 'react';
import { Link, useSearchParams, useLocation } from 'react-router-dom';
import { Eye, KeyRound, RefreshCw, Search, UserRound } from 'lucide-react';
import { Dialog } from '@/components/ui/Dialog';
import { Button } from '@/components/ui/Button';
import { SelectInput } from '@/components/forms/SelectInput';
import { Pagination } from '@/components/data-display/Pagination';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { pageOf } from '../schemas/contracts';
import { submissionSummarySchema, type SubmissionSummary } from '../schemas/approvals';
import { onboardingLabels } from '../types/contracts';
import { queryString } from '../api/hr-api';
import { labelDate } from '../utils/messages';
import { Denied, QueryState } from '../components/Feedback';
import { HiringSubmissionReview } from '../components/approvals/HiringSubmissionReview';
import '../components/approvals/hiring-review.css';

export function HiringApprovalsPage() {
  const access = useHrAccess();
  const [params] = useSearchParams();
  return (
    <HiringQueue
      key={JSON.stringify([
        access.subject,
        access.ready,
        access.user?.grants,
        params.get('search'),
      ])}
      initialSearch={params.get('search') ?? ''}
    />
  );
}

function HiringQueue({ initialSearch }: { initialSearch: string }) {
  const access = useHrAccess();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
  const [search, setSearch] = useState(initialSearch.slice(0, 100));
  const [status, setStatus] = useState('1');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<SubmissionSummary | null>(null);
  const [message, setMessage] = useState('');
  const [reviewDirty, setReviewDirty] = useState(false);
  const [reviewBusy, setReviewBusy] = useState(false);
  const term = useDebouncedValue(search);
  const query = useHrQuery(
    'employees/submissions' + queryString({ page, pageSize: 15, search: term, status }),
    pageOf(submissionSummarySchema),
    ['hr.employees.read', 'hr.employees.submissions.review'],
  );
  if (!access.can('hr.employees.read', 'hr.employees.submissions.review')) return <Denied />;
  return (
    <div className="hiring-queue">
      <div className="hr-page-heading">
        <div>
          <p className="hr-kicker">EMPLOYEE ONBOARDING</p>
          <h1>Hiring &amp; employee setup</h1>
        </div>
        <Button
          variant="secondary"
          disabled={query.isFetching || !!selected}
          onClick={() => void query.refetch()}
        >
          <RefreshCw size={16} /> Refresh
        </Button>
      </div>
      {message && (
        <p role="status" className="hr-message">
          {message}
        </p>
      )}
      <div className="hr-toolbar">
        <label className="hr-search">
          <Search size={17} />
          <input
            aria-label="Search hiring submissions"
            placeholder="Search employee name"
            maxLength={100}
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <SelectInput
          label="Hiring review status"
          value={status}
          options={[1, 2, 3, 4].map((i) => ({
            value: String(i),
            label: onboardingLabels[i] ?? 'Unknown',
          }))}
          onChange={(e) => {
            setStatus(e.target.value);
            setPage(1);
          }}
        />
      </div>
      <QueryState query={query}>
        {!query.data?.items.length && <p className="hr-notice">No submissions match this view.</p>}
        {query.data?.items.map((s) => (
          <article key={s.id} className="hr-request-row">
            <div>
              <button
                className="hr-text-button"
                disabled={!access.can('hr.employees.documents.read')}
                onClick={() => {
                  setMessage('');
                  setSelected(s);
                }}
              >
                {s.name ?? 'Employee'} &middot; #{s.employeeNumber}
              </button>
              <p className="muted">
                Revision {s.version} &middot; {labelDate(s.submittedAt)}
              </p>
            </div>
            <div className="hiring-row-actions">
              <span className="hr-status hr-status--neutral">{onboardingLabels[s.status]}</span>
              <Button
                variant="secondary"
                disabled={!access.can('hr.employees.documents.read')}
                onClick={() => {
                  setMessage('');
                  setSelected(s);
                }}
              >
                <Eye size={16} /> Review details
              </Button>
              {s.status === 2 && (
                <>
                  <Link className="button button--secondary" to={`${basePath}/employees/${s.employeeId}`}>
                    <UserRound size={16} /> Employee details
                  </Link>
                  {access.can('access.accounts.read') && (
                    <Link
                      className="button button--secondary"
                      to={`${basePath}/employees/${s.employeeId}?section=access`}
                    >
                      <KeyRound size={16} /> Roles &amp; software access
                    </Link>
                  )}
                </>
              )}
            </div>
          </article>
        ))}
        {query.data && (
          <Pagination page={page} pageSize={15} total={query.data.totalCount} onChange={setPage} />
        )}
      </QueryState>
      {!access.can('hr.employees.documents.read') && (
        <p className="hr-notice">Document access is required to review a submission.</p>
      )}
      <Dialog
        open={!!selected}
        onOpenChange={(open) => {
          if (
            !open &&
            !reviewBusy &&
            (!reviewDirty || window.confirm('Discard the unsaved hiring decision?'))
          ) {
            setSelected(null);
            setReviewDirty(false);
          }
        }}
        title="Hiring submission"
        className="hiring-review-dialog"
        description={selected ? `${selected.name ?? 'Employee'} · #${selected.employeeNumber}` : ''}
      >
        {selected && (
          <HiringSubmissionReview
            key={selected.id}
            submission={selected}
            onDirty={setReviewDirty}
            onBusy={setReviewBusy}
            onSaved={(decision) => {
              setReviewDirty(false);
              setMessage(`Hiring decision recorded: ${decision}.`);
              setSelected(null);
              void query.refetch();
            }}
          />
        )}
      </Dialog>
    </div>
  );
}
