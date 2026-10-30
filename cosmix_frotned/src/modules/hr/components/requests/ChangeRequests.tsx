import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import {
  Ban,
  Clock,
  CheckCircle2,
  XCircle,
  RotateCcw,
  Eye,
  FileSearch,
  ExternalLink,
  Calendar,
  Inbox,
  FileText,
  Briefcase,
  ShieldCheck,
  CreditCard,
  HeartPulse,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { Pagination } from '@/components/data-display/Pagination';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import {
  changeRequestSchema,
  changeRequestSummarySchema,
  currentUpdateSchema,
} from '../../schemas/updates';
import { employeeSchema, pageOf, writeResult } from '../../schemas/contracts';
import { approvalLabels, type EmployeeSummary } from '../../types/contracts';
import type { Section } from '../../types/updates';
import { queryString } from '../../api/hr-api';
import { labelDate } from '../../utils/messages';
import { ActionMessage, QueryState } from '../Feedback';
import { RequestDecisionForm, type DecisionGuard } from './RequestDecisionForm';
import { ChangeRequestReviewViewer } from './ChangeRequestReviewViewer';
import '@/styles/hr-requests.css';

export function ChangeRequests({
  employeeId,
  section,
  state,
  search = '',
  onDecided,
}: {
  employeeId?: string;
  section: Section;
  state?: string;
  search?: string;
  onDecided?: () => void;
}) {
  const [page, setPage] = useState(1);
  const [decisionGuard, setDecisionGuard] = useState<DecisionGuard>({
    dirty: false,
    pending: false,
  });
  const [selected, setSelected] = useState<{ employeeId: string; id: string } | null>(null);

  const path = employeeId
    ? `employees/${employeeId}/changes/${section.name}/requests`
    : `employees/change-requests/${section.name}`;

  const query = useHrQuery(
    path + queryString({ page, pageSize: 20, state }),
    pageOf(changeRequestSummarySchema),
    ['hr.employees.read', ...section.read],
  );

  // Fetch employees directory to map IDs to real names & codes
  const employeesQuery = useHrQuery('employees?pageSize=100', pageOf(employeeSchema), [
    'hr.employees.read',
  ]);

  const employeeMap = useMemo(() => {
    const map = new Map<string, EmployeeSummary>();
    employeesQuery.data?.items.forEach((emp) => map.set(emp.id.toLowerCase(), emp));
    return map;
  }, [employeesQuery.data]);

  // Filter items if user typed a search term
  const items = useMemo(() => {
    if (!query.data?.items) return [];
    if (!search.trim()) return query.data.items;

    const term = search.toLowerCase().trim();
    return query.data.items.filter((r) => {
      const emp = employeeMap.get(r.employeeId.toLowerCase());
      const nameMatch = emp?.name?.toLowerCase().includes(term);
      const codeMatch = emp?.employeeNumber?.toString().includes(term);
      const idMatch = r.id.toLowerCase().includes(term);
      return nameMatch || codeMatch || idMatch;
    });
  }, [query.data?.items, search, employeeMap]);

  const renderStatusBadge = (statusState: number) => {
    switch (statusState) {
      case 0:
        return (
          <span className="hr-status-pill hr-status-pill--pending">
            <Clock size={12} /> Pending Review
          </span>
        );
      case 1:
        return (
          <span className="hr-status-pill hr-status-pill--approved">
            <CheckCircle2 size={12} /> Approved
          </span>
        );
      case 2:
        return (
          <span className="hr-status-pill hr-status-pill--returned">
            <RotateCcw size={12} /> Returned
          </span>
        );
      case 3:
        return (
          <span className="hr-status-pill hr-status-pill--rejected">
            <XCircle size={12} /> Rejected
          </span>
        );
      case 4:
        return (
          <span className="hr-status-pill hr-status-pill--cancelled">
            <Ban size={12} /> Cancelled
          </span>
        );
      default:
        return <span className="hr-status-pill">{approvalLabels[statusState] ?? 'Unknown'}</span>;
    }
  };

  const getSectionIcon = () => {
    switch (section.name) {
      case 'employment':
        return <Briefcase size={13} />;
      case 'documents':
        return <FileText size={13} />;
      case 'identity':
        return <ShieldCheck size={13} />;
      case 'bank':
        return <CreditCard size={13} />;
      case 'medical':
        return <HeartPulse size={13} />;
      default:
        return <FileText size={13} />;
    }
  };

  if (employeeId && !items.length) {
    return (
      <div
        className="emp-card"
        style={{
          marginTop: '18px',
          padding: '20px 24px',
          background: '#ffffff',
          border: '1px solid #e2e8f0',
          borderRadius: '10px',
        }}
      >
        <div className="emp-card-header simple" style={{ marginBottom: '8px' }}>
          <div
            className="emp-card-title"
            style={{ fontSize: '15px', fontWeight: 700, color: '#0f172a' }}
          >
            <FileText size={16} className="emp-card-title-icon" style={{ color: '#2563eb' }} />
            <span>Pending {section.label.toLowerCase()} requests</span>
          </div>
        </div>
        <p className="muted" style={{ margin: 0, fontSize: '13px', color: '#64748b' }}>
          No pending change requests for this employee.
        </p>
      </div>
    );
  }

  return (
    <section className="hr-requests-container">
      <div className="hr-requests-count-summary">
        <span>
          Showing {items.length} {section.label.toLowerCase()} change request
          {items.length !== 1 ? 's' : ''}
        </span>
        {state && (
          <span className="hr-requests-meta-badge">
            Filter: {approvalLabels[Number(state)] ?? 'Active'}
          </span>
        )}
      </div>

      <QueryState query={query}>
        {!items.length ? (
          <div className="hr-requests-empty">
            <div className="hr-requests-empty-icon">
              <Inbox size={28} />
            </div>
            <h3 className="hr-requests-empty-title">No {section.label} Requests Found</h3>
            <p className="hr-requests-empty-text">
              There are currently no change requests pending in the {section.label.toLowerCase()}{' '}
              category. When employees or department heads request record updates, they will appear
              here for audit and authorization.
            </p>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
            {items.map((r) => {
              const emp = employeeMap.get(r.employeeId.toLowerCase());
              const empName = emp?.name || `Employee #${r.employeeId.slice(0, 8)}`;
              const empCode = emp?.employeeNumber
                ? `EMP-${String(emp.employeeNumber).padStart(4, '0')}`
                : `ID-${r.employeeId.slice(0, 6)}`;
              const initials = emp?.name
                ? emp.name
                    .split(' ')
                    .filter(Boolean)
                    .map((p: string) => p[0])
                    .slice(0, 2)
                    .join('')
                    .toUpperCase()
                : 'EMP';

              return (
                <article className="hr-request-card" key={r.id}>
                  <div className="hr-request-card-left">
                    <div className="hr-request-avatar">{initials}</div>
                    <div className="hr-request-info">
                      <div className="hr-request-emp-name">
                        <span>{empName}</span>
                        <span className="hr-request-emp-code">{empCode}</span>
                      </div>
                      <div className="hr-request-desc">
                        {getSectionIcon()}
                        <span>{section.label} Data Modification Request</span>
                      </div>
                    </div>
                  </div>

                  <div className="hr-request-card-mid">
                    <span className="hr-request-meta-badge">
                      REQ-{r.id.slice(0, 8).toUpperCase()}
                    </span>
                    <span className="hr-request-date">
                      <Calendar size={13} /> {labelDate(r.requestedAt)}
                    </span>
                  </div>

                  <div className="hr-request-card-right">
                    {renderStatusBadge(r.state)}

                    <button
                      className={`hr-request-action-btn ${
                        r.state === 0
                          ? 'hr-request-action-btn--primary'
                          : 'hr-request-action-btn--outline'
                      }`}
                      onClick={() => setSelected({ employeeId: r.employeeId, id: r.id })}
                    >
                      {r.state === 0 ? (
                        <>
                          <Eye size={14} /> Review & Decide
                        </>
                      ) : (
                        <>
                          <FileSearch size={14} /> Audit Details
                        </>
                      )}
                    </button>

                    {!employeeId && (
                      <Link
                        className="hr-request-link-btn"
                        to={`/hr/employees/${r.employeeId}?section=${section.name}`}
                        title="View employee profile"
                      >
                        <ExternalLink size={13} />
                        Profile
                      </Link>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        )}

        {query.data && query.data.totalCount > 20 && (
          <div style={{ marginTop: '16px' }}>
            <Pagination
              page={page}
              pageSize={20}
              total={query.data.totalCount}
              onChange={setPage}
            />
          </div>
        )}
      </QueryState>

      <Dialog
        open={!!selected}
        onOpenChange={(open) => {
          if (
            !open &&
            !decisionGuard.pending &&
            (!decisionGuard.dirty ||
              window.confirm('Discard unsaved decision and close this request?'))
          )
            setSelected(null);
        }}
        title={`${section.label} Change Request`}
        description="Inspect existing record against proposed changes and submit administrative authorization"
        className="change-evidence-dialog-wide"
      >
        {selected && (
          <ChangeRequestDetail
            key={selected.id}
            employeeId={selected.employeeId}
            employee={employeeMap.get(selected.employeeId.toLowerCase())}
            requestId={selected.id}
            section={section}
            onSaved={() => {
              void query.refetch();
              onDecided?.();
            }}
            onGuardChange={setDecisionGuard}
            onCorrectedRequest={() => setSelected(null)}
            onClose={() => setSelected(null)}
          />
        )}
      </Dialog>
    </section>
  );
}

function ChangeRequestDetail({
  employeeId,
  employee,
  requestId,
  section,
  onSaved,
  onGuardChange,
  onCorrectedRequest,
  onClose,
}: {
  employeeId: string;
  employee?: EmployeeSummary | null;
  requestId: string;
  section: Section;
  onSaved: () => void;
  onGuardChange: (guard: DecisionGuard) => void;
  onCorrectedRequest: () => void;
  onClose: () => void;
}) {
  const access = useHrAccess();
  const action = useHrAction();
  const path = `employees/${employeeId}/changes/${section.name}/requests/${requestId}`;
  const reviewPermissions = [
    'hr.employees.read',
    ...section.read,
    'hr.employees.changes.review',
    ...(section.name === 'identity' || section.name === 'medical'
      ? ['hr.employees.documents.read']
      : []),
  ];
  const query = useHrQuery(path, changeRequestSchema, ['hr.employees.read', ...section.read]);

  const decisionFormSlot = query.data ? (
    <div>
      {query.data.request.state === 0 && (
        <RequestDecisionForm
          key={access.subject + ':' + query.data.request.version}
          request={query.data.request}
          path={path}
          currentPath={`employees/${employeeId}/current/${section.name}`}
          currentSchema={currentUpdateSchema}
          permissions={reviewPermissions}
          onGuardChange={onGuardChange}
          onSaved={() => {
            void query.refetch();
            onSaved();
          }}
        />
      )}

      {query.data.request.state === 2 && access.can(...section.write) && (
        <div style={{ marginTop: '12px' }}>
          <Link
            className="button button--secondary"
            to={`/hr/employees/${employeeId}?section=${section.name}&edit=1`}
            onClick={onCorrectedRequest}
          >
            New corrected request
          </Link>
        </div>
      )}

      {query.data.request.state === 0 &&
        query.data.request.requesterId === access.user?.account?.accountId &&
        access.can(...section.write) && (
          <div style={{ marginTop: '12px' }}>
            <Button
              variant="secondary"
              disabled={action.pending}
              onClick={() => {
                if (window.confirm('Cancel your pending change request?'))
                  void action
                    .run(
                      path + '/cancel',
                      { expectedRequestVersion: query.data!.request.version },
                      'POST',
                      writeResult,
                    )
                    .then((r) => {
                      if (r) {
                        void query.refetch();
                        onSaved();
                      }
                    });
              }}
            >
              <Ban size={16} />
              Cancel request
            </Button>
          </div>
        )}

      <ActionMessage action={action} />
    </div>
  ) : null;

  return (
    <QueryState query={query}>
      {query.data && (
        <ChangeRequestReviewViewer
          request={query.data.request}
          section={section}
          employee={employee}
          reason={query.data.reason}
          effectiveFrom={query.data.effectiveFrom}
          beforeData={query.data.before.data}
          proposedData={query.data.proposed}
          decisions={query.data.decisions}
          decisionFormSlot={decisionFormSlot}
          onClose={onClose}
        />
      )}
    </QueryState>
  );
}
