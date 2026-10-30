import { formatEmployeeCode } from '@/utils/employee-code';
import { EmployeeReference } from '@/components/employees/EmployeeReference';
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
  UserCheck,
  Banknote,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { Pagination } from '@/components/data-display/Pagination';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import {
  workflowRequestSchema,
  salaryRequestDetailSchema,
  statusRequestDetailSchema,
  compensationStateSchema,
  statusStateSchema,
} from '../../schemas/requests';
import { employeeSchema, pageOf, writeResult } from '../../schemas/contracts';
import { approvalLabels, employmentLabels, type EmployeeSummary } from '../../types/contracts';
import type { RequestKind } from '../../types/requests';
import { queryString } from '../../api/hr-api';
import { labelDate } from '../../utils/messages';
import { ActionMessage, QueryState } from '../Feedback';
import { CompensationView } from './CompensationView';
import { RequestDecisionForm, type DecisionGuard } from './RequestDecisionForm';
import '@/styles/hr-requests.css';

export function WorkflowRequests({
  employeeId,
  kind,
  state,
  search = '',
  onDecided,
}: {
  employeeId?: string;
  kind: RequestKind;
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
    ? `employees/${employeeId}/${kind}/requests`
    : `employees/${kind}-requests`;

  const query = useHrQuery(
    path + queryString({ page, pageSize: 20, state }),
    pageOf(workflowRequestSchema),
    [`hr.employees.${kind}.read`],
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

  const name = kind === 'compensation' ? 'Salary' : 'Employment status';

  // Filter items if user provided quick search
  const items = useMemo(() => {
    if (!query.data?.items) return [];
    if (!search.trim()) return query.data.items;

    const term = search.toLowerCase().trim();
    return query.data.items.filter((r) => {
      const emp = employeeMap.get(r.employeeId.toLowerCase());
      const nameMatch = emp?.name?.toLowerCase().includes(term);
      const codeMatch =
        formatEmployeeCode(emp?.employeeNumber).toLowerCase().includes(term) ||
        emp?.employeeNumber?.toString().includes(term);
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

  return (
    <section className="hr-requests-container">
      <div className="hr-requests-count-summary">
        <span>
          Showing {items.length} {name.toLowerCase()} request{items.length !== 1 ? 's' : ''}
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
            <h3 className="hr-requests-empty-title">No {name} Requests Found</h3>
            <p className="hr-requests-empty-text">
              There are currently no {name.toLowerCase()} requests matching the active filter. When
              employees or HR admins submit lifecycle transitions or salary adjustments, they will
              appear here for audit and approval.
            </p>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
            {items.map((r) => {
              const emp = employeeMap.get(r.employeeId.toLowerCase());
              const empName = emp?.name || 'Employee name unavailable';
              const empCode = (
                <EmployeeReference employeeId={r.employeeId} employeeNumber={emp?.employeeNumber} />
              );
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
                        {kind === 'compensation' ? (
                          <>
                            <Banknote size={13} />
                            <span>Salary & Wage Revision Proposal</span>
                          </>
                        ) : (
                          <>
                            <UserCheck size={13} />
                            <span>Employment Lifecycle Status Transition</span>
                          </>
                        )}
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
                        to={`/hr/employees/${r.employeeId}?section=${kind}`}
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
        title={`${name} Approval Request`}
        description="Verify submitted records, audit change delta, and record administrative decision"
      >
        {selected && (
          <WorkflowRequestDetail
            key={selected.id}
            kind={kind}
            employeeId={selected.employeeId}
            requestId={selected.id}
            onSaved={() => {
              void query.refetch();
              onDecided?.();
            }}
            onGuardChange={setDecisionGuard}
            onCorrectedRequest={() => setSelected(null)}
          />
        )}
      </Dialog>
    </section>
  );
}

function WorkflowRequestDetail({
  kind,
  employeeId,
  requestId,
  onSaved,
  onGuardChange,
  onCorrectedRequest,
}: {
  kind: RequestKind;
  employeeId: string;
  requestId: string;
  onSaved: () => void;
  onGuardChange: (guard: DecisionGuard) => void;
  onCorrectedRequest: () => void;
}) {
  const access = useHrAccess();
  const action = useHrAction();
  const path = `employees/${employeeId}/${kind}/requests/${requestId}`;
  const reviewPermissions = [`hr.employees.${kind}.read`, `hr.employees.${kind}.review`];
  const query = useHrQuery(
    path,
    kind === 'compensation' ? salaryRequestDetailSchema : statusRequestDetailSchema,
    [`hr.employees.${kind}.read`],
  );

  return (
    <QueryState query={query}>
      {query.data && (
        <div className="hr-req-modal-content">
          <div className="hr-req-modal-header-strip">
            <span className="hr-request-meta-badge">
              ID: {query.data.request.id.slice(0, 8).toUpperCase()}
            </span>
            <span
              className={`hr-status-pill ${
                query.data.request.state === 1
                  ? 'hr-status-pill--approved'
                  : query.data.request.state === 3
                    ? 'hr-status-pill--rejected'
                    : query.data.request.state === 2
                      ? 'hr-status-pill--returned'
                      : 'hr-status-pill--pending'
              }`}
            >
              {approvalLabels[query.data.request.state]}
            </span>
          </div>

          {query.data.reason && (
            <div className="hr-req-reason-quote">
              <strong>Stated Justification / Reason:</strong>
              <p style={{ margin: '4px 0 0' }}>{query.data.reason}</p>
            </div>
          )}

          {'proposed' in query.data ? (
            <div className="hr-req-diff-grid">
              <div className="hr-req-diff-card hr-req-diff-card--before">
                <div className="hr-req-diff-title">Current / Before Record</div>
                {query.data.before ? (
                  <CompensationView terms={query.data.before.terms} />
                ) : (
                  <p className="muted">No previous compensation on record.</p>
                )}
              </div>
              <div className="hr-req-diff-card hr-req-diff-card--proposed">
                <div className="hr-req-diff-title">Proposed New Compensation</div>
                <CompensationView terms={query.data.proposed} />
                <div
                  style={{ marginTop: '10px', fontSize: '12px', color: '#059669', fontWeight: 600 }}
                >
                  Effective from: {query.data.effectiveFrom}
                </div>
              </div>
            </div>
          ) : (
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                gap: '12px',
                background: '#f8fafc',
                border: '1px solid #e2e8f0',
                borderRadius: '10px',
                padding: '16px',
              }}
            >
              <div>
                <span style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>
                  Previous Status
                </span>
                <p style={{ margin: '4px 0 0', fontWeight: 700, color: '#0f172a' }}>
                  {employmentLabels[query.data.previousStatus]}
                </p>
              </div>
              <div>
                <span style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>
                  Proposed New Status
                </span>
                <p style={{ margin: '4px 0 0', fontWeight: 700, color: '#059669' }}>
                  {employmentLabels[query.data.newStatus]}
                </p>
              </div>
              <div>
                <span style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>
                  Requested Effective Time
                </span>
                <p style={{ margin: '4px 0 0', fontWeight: 600, color: '#0f172a' }}>
                  {query.data.requestedEffectiveAt
                    ? labelDate(query.data.requestedEffectiveAt) + ' (PKT)'
                    : 'Immediately on Approval'}
                </p>
              </div>
            </div>
          )}

          <div>
            <h4 style={{ margin: '0 0 10px', fontSize: '13px', fontWeight: 700, color: '#0f172a' }}>
              Audit Trail & Recorded Decisions
            </h4>
            {!query.data.decisions.length ? (
              <p className="muted" style={{ fontSize: '13px', margin: 0 }}>
                No prior decisions recorded yet. Request is currently awaiting administrator review.
              </p>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                {query.data.decisions.map((d) => (
                  <div
                    key={d.id}
                    style={{
                      padding: '12px',
                      background: '#f8fafc',
                      border: '1px solid #e2e8f0',
                      borderRadius: '8px',
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                      }}
                    >
                      <strong style={{ fontSize: '13px', color: '#0f172a' }}>
                        {approvalLabels[d.decision]}
                      </strong>
                      <span style={{ fontSize: '11px', color: '#64748b' }}>
                        {labelDate(d.decidedAt)}
                      </span>
                    </div>
                    {d.reason && (
                      <p style={{ margin: '6px 0', fontSize: '13px', color: '#334155' }}>
                        {d.reason}
                      </p>
                    )}
                    <span style={{ fontSize: '11px', color: '#94a3b8' }}>
                      Decided by {d.actorRoleCode} &bull; {d.actorAccountId}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {query.data.request.state === 0 && (
            <RequestDecisionForm
              key={access.subject + ':' + query.data.request.version}
              request={{
                ...query.data.request,
                requesterId: query.data.request.requesterAccountId,
              }}
              path={path}
              currentPath={`employees/${employeeId}/${kind}`}
              currentSchema={kind === 'compensation' ? compensationStateSchema : statusStateSchema}
              permissions={reviewPermissions}
              onGuardChange={onGuardChange}
              onSaved={() => {
                void query.refetch();
                onSaved();
              }}
            />
          )}

          {query.data.request.state === 2 && access.can(`hr.employees.${kind}.request`) && (
            <Link
              className="button button--secondary"
              to={`/hr/employees/${employeeId}?section=${kind}&edit=1`}
              onClick={onCorrectedRequest}
            >
              New corrected request
            </Link>
          )}

          {query.data.request.state === 0 &&
            query.data.request.requesterAccountId === access.user?.account?.accountId &&
            access.can(`hr.employees.${kind}.request`) && (
              <Button
                variant="secondary"
                disabled={action.pending}
                onClick={() => {
                  if (window.confirm('Cancel your pending request?'))
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
            )}

          <ActionMessage action={action} />
        </div>
      )}
    </QueryState>
  );
}
