import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Plus, RefreshCw, Wallet, Clock, History, FileText, Coins } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Pagination } from '@/components/data-display/Pagination';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import {
  compensationEntrySchema,
  compensationStateSchema,
  statusHistorySchema,
  statusStateSchema,
} from '../../schemas/requests';
import { pageOf } from '../../schemas/contracts';
import { employmentLabels } from '../../types/contracts';
import { labelDate } from '../../utils/messages';
import { Denied, QueryState } from '../Feedback';
import { SalaryRequestModal } from './SalaryRequestForm';
import { StatusRequestForm } from './StatusRequestForm';
import { WorkflowRequests } from './WorkflowRequests';
import { CompensationView } from './CompensationView';
import { EmployeeArchive } from '../employee/EmployeeArchive';
import { statusRequestChoices } from './lifecycle-status';
import '@/styles/hr-dashboard.css';

export function SalaryWorkspace({
  employeeId,
  edit,
  employeeName,
}: {
  employeeId: string;
  edit: boolean;
  employeeName?: string;
  employeeNumber?: number | string;
}) {
  const access = useHrAccess();
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [isRequestModalOpen, setIsRequestModalOpen] = useState(edit);

  useEffect(() => {
    if (edit) {
      setIsRequestModalOpen(true);
    }
  }, [edit]);

  const current = useHrQuery(`employees/${employeeId}/compensation`, compensationStateSchema, [
    'hr.employees.compensation.read',
  ]);
  const history = useHrQuery(
    `employees/${employeeId}/compensation/history?page=${page}&pageSize=10`,
    pageOf(compensationEntrySchema),
    ['hr.employees.compensation.read'],
  );

  if (!access.can('hr.employees.compensation.read')) return <Denied />;

  return (
    <div className="emp-section-wrapper">
      {/* Section Header */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '8px',
          marginBottom: '10px',
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: '13.5px', fontWeight: 700, color: '#0f172a' }}>
            Salary &amp; Compensation Records
          </h2>
          {current.data && (
            <p
              className="muted"
              style={{ margin: '2px 0 0 0', fontSize: '11px', color: '#64748b' }}
            >
              Active terms as of {current.data.asOf} &middot; Lifecycle Version #
              {current.data.employeeVersion}
            </p>
          )}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          {access.can('hr.employees.compensation.request') && !current.data?.nextScheduled && (
            <Button
              variant="primary"
              onClick={() => setIsRequestModalOpen(true)}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '4px',
                fontSize: '11px',
                height: '26px',
                padding: '0 10px',
              }}
            >
              <Plus size={12} />
              Propose Salary Revision
            </Button>
          )}
          <Button
            variant="secondary"
            title="Reload compensation records"
            onClick={() => {
              void current.refetch();
              void history.refetch();
            }}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '4px',
              fontSize: '11px',
              height: '26px',
              padding: '0 8px',
              borderColor: '#e2e8f0',
            }}
          >
            <RefreshCw size={11} />
            Reload
          </Button>
        </div>
      </div>

      {/* Main Active Compensation Card */}
      <div className="emp-card">
        <div className="emp-card-header">
          <div className="emp-card-title">
            <Wallet size={14} className="emp-card-title-icon" />
            <span>Active Salary Structure</span>
          </div>
          {current.data?.effective ? (
            <span
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '5px',
                fontSize: '11px',
                fontWeight: 600,
                padding: '2px 8px',
                borderRadius: '4px',
                background: '#ecfdf5',
                color: '#059669',
                border: '1px solid #a7f3d0',
              }}
            >
              <span
                style={{
                  width: '6px',
                  height: '6px',
                  borderRadius: '50%',
                  background: '#10b981',
                }}
              />
              Active &amp; Effective
            </span>
          ) : (
            <span
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '4px',
                fontSize: '11px',
                fontWeight: 600,
                padding: '2px 8px',
                borderRadius: '4px',
                background: '#fffbeb',
                color: '#d97706',
                border: '1px solid #fde68a',
              }}
            >
              Unassigned Structure
            </span>
          )}
        </div>

        <QueryState query={current}>
          {current.data?.effective ? (
            <>
              <CompensationView terms={current.data.effective.terms} />
              <div
                style={{
                  marginTop: '10px',
                  paddingTop: '8px',
                  borderTop: '1px solid #f1f5f9',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  flexWrap: 'wrap',
                  gap: '6px',
                  fontSize: '11px',
                  color: '#64748b',
                }}
              >
                <span>
                  Effective Date:{' '}
                  <strong style={{ color: '#0f172a' }}>
                    {current.data.effective.effectiveFrom}
                  </strong>
                </span>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                  Record ID:{' '}
                  <code
                    style={{
                      fontSize: '10px',
                      background: '#f8fafc',
                      border: '1px solid #e2e8f0',
                      padding: '1px 5px',
                      borderRadius: '3px',
                      fontFamily: 'monospace',
                    }}
                  >
                    {current.data.latestRecordId.slice(0, 8)}
                  </code>
                </span>
              </div>
            </>
          ) : (
            <div
              style={{
                textAlign: 'center',
                padding: '24px 16px',
                background: '#f8fafc',
                borderRadius: '6px',
                border: '1px dashed #cbd5e1',
                margin: '4px 0',
              }}
            >
              <Coins size={30} style={{ color: '#94a3b8', margin: '0 auto 8px' }} />
              <h4
                style={{
                  margin: '0 0 4px',
                  fontSize: '12.5px',
                  fontWeight: 650,
                  color: '#334155',
                }}
              >
                No Active Salary Record
              </h4>
              <p
                style={{
                  margin: '0 auto 12px',
                  maxWidth: '380px',
                  fontSize: '11.5px',
                  color: '#64748b',
                  lineHeight: 1.4,
                }}
              >
                This employee has no active wage terms or base salary registered in the system.
              </p>
              {access.can('hr.employees.compensation.request') && !current.data?.nextScheduled && (
                <Button
                  variant="primary"
                  onClick={() => setIsRequestModalOpen(true)}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    fontSize: '11px',
                    height: '26px',
                    padding: '0 12px',
                  }}
                >
                  <Plus size={12} />
                  Propose Salary Structure
                </Button>
              )}
            </div>
          )}
        </QueryState>
      </div>

      {/* Scheduled Salary Revision Card (if present) */}
      {current.data?.nextScheduled && (
        <div
          className="emp-card"
          style={{
            borderLeft: '3px solid #2563eb',
            background: '#f8fafc',
          }}
        >
          <div className="emp-card-header">
            <div className="emp-card-title">
              <Clock size={14} className="emp-card-title-icon" style={{ color: '#2563eb' }} />
              <span>
                Scheduled Salary Revision &middot; Effective{' '}
                {current.data.nextScheduled.effectiveFrom}
              </span>
            </div>
            <span
              style={{
                fontSize: '10.5px',
                fontWeight: 600,
                padding: '2px 8px',
                borderRadius: '4px',
                background: '#eff6ff',
                color: '#1d4ed8',
                border: '1px solid #bfdbfe',
              }}
            >
              Pending Execution
            </span>
          </div>
          <CompensationView terms={current.data.nextScheduled.terms} />
        </div>
      )}

      {/* Salary Adjustment Requests Queue Card */}
      <div className="emp-card" style={{ marginTop: '10px' }}>
        <div className="emp-card-header simple">
          <div className="emp-card-title">
            <FileText size={14} className="emp-card-title-icon" />
            <span>Salary Revision Requests Queue</span>
          </div>
        </div>
        <WorkflowRequests
          key={revision}
          employeeId={employeeId}
          kind="compensation"
          onDecided={() => {
            void current.refetch();
            void history.refetch();
          }}
        />
      </div>

      {/* Salary History Ledger Card */}
      <div className="emp-card" style={{ marginTop: '10px' }}>
        <div className="emp-card-header simple">
          <div className="emp-card-title">
            <History size={14} className="emp-card-title-icon" />
            <span>Salary Revision Ledger &amp; History</span>
          </div>
        </div>
        <QueryState query={history}>
          {!history.data?.items.length ? (
            <p className="muted" style={{ margin: '8px 0', fontSize: '11.5px', color: '#64748b' }}>
              No previous salary revisions recorded.
            </p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '6px' }}>
              {history.data.items.map((r) => (
                <div
                  key={r.id}
                  style={{
                    padding: '8px 12px',
                    background: '#f8fafc',
                    border: '1px solid #e2e8f0',
                    borderRadius: '6px',
                  }}
                >
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      marginBottom: '6px',
                      flexWrap: 'wrap',
                      gap: '6px',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span
                        style={{
                          fontSize: '10px',
                          fontWeight: 700,
                          padding: '1px 6px',
                          borderRadius: '3px',
                          background: '#0f172a',
                          color: '#ffffff',
                        }}
                      >
                        Rev #{r.revision}
                      </span>
                      <span style={{ fontSize: '12px', fontWeight: 650, color: '#1e293b' }}>
                        Effective {r.effectiveFrom} &rarr; {r.effectiveTo ?? 'Current / Ongoing'}
                      </span>
                    </div>
                    <span style={{ fontSize: '11px', color: '#64748b' }}>
                      Recorded {labelDate(r.recordedAt)} &middot; Actor{' '}
                      {r.recordedByUserId.slice(0, 8)}
                    </span>
                  </div>
                  <CompensationView terms={r.terms} />
                </div>
              ))}
              {history.data && history.data.totalCount > 10 && (
                <Pagination
                  page={page}
                  pageSize={10}
                  total={history.data.totalCount}
                  onChange={setPage}
                />
              )}
            </div>
          )}
        </QueryState>
      </div>

      {/* Propose Revision Modal Dialog */}
      {current.data && access.can('hr.employees.compensation.request') && (
        <SalaryRequestModal
          open={isRequestModalOpen}
          onClose={() => setIsRequestModalOpen(false)}
          current={current.data}
          employeeName={employeeName}
          onSaved={() => {
            setIsRequestModalOpen(false);
            setRevision((v) => v + 1);
            void current.refetch();
            void history.refetch();
          }}
        />
      )}
    </div>
  );
}
export function StatusWorkspace({ employeeId, edit }: { employeeId: string; edit: boolean }) {
  const access = useHrAccess();
  return (
    <StatusWorkspaceContent
      key={JSON.stringify([employeeId, access.subject, access.ready, access.user])}
      employeeId={employeeId}
      edit={edit}
    />
  );
}
function StatusWorkspaceContent({ employeeId, edit }: { employeeId: string; edit: boolean }) {
  const access = useHrAccess();
  const lifetime = useHrLifetime();
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [formRevision, setFormRevision] = useState(0);
  const current = useHrQuery(`employees/${employeeId}/lifecycle`, statusStateSchema, [
    'hr.employees.lifecycle.read',
  ]);
  const history = useHrQuery(
    `employees/${employeeId}/lifecycle/history?page=${page}&pageSize=10`,
    pageOf(statusHistorySchema),
    ['hr.employees.lifecycle.read'],
  );
  if (!access.can('hr.employees.lifecycle.read')) return <Denied />;
  const canRequest =
    access.can('hr.employees.lifecycle.request') &&
    current.isFetchedAfterMount &&
    !current.isFetching &&
    !current.isError &&
    current.data?.employeeId === employeeId &&
    statusRequestChoices(current.data).length > 0;
  return (
    <>
      <h2>Employment status</h2>
      <QueryState query={current}>
        {current.data && (
          <>
            <p>
              Current status: <strong>{employmentLabels[current.data.currentStatus]}</strong>
            </p>
            {current.data.scheduledChange && (
              <p className="hr-message">
                Scheduled: {employmentLabels[current.data.scheduledChange.newStatus]} from{' '}
                {labelDate(current.data.scheduledChange.effectiveAt)} (Pakistan)
              </p>
            )}
            {edit ? (
              canRequest ? (
                <StatusRequestForm
                  key={`${current.data.version}:${formRevision}`}
                  current={current.data}
                  onSaved={() => setRevision((v) => v + 1)}
                  onReload={() => {
                    const signal = lifetime();
                    void current.refetch().then((result) => {
                      if (!signal.aborted && result.isSuccess)
                        setFormRevision((value) => value + 1);
                    });
                  }}
                />
              ) : (
                <p className="hr-message hr-message--error">
                  A new status request is not available.
                </p>
              )
            ) : (
              canRequest && (
                <div className="hr-actions">
                  <Link className="button button--secondary" to="?section=lifecycle&edit=1">
                    <Plus size={16} />
                    New status request
                  </Link>
                </div>
              )
            )}
          </>
        )}
      </QueryState>
      {current.data &&
        !current.isError &&
        current.isFetchedAfterMount &&
        current.data.employeeId === employeeId && (
          <EmployeeArchive
            current={current.data}
            currentReady={!current.isFetching}
            onSaved={() => {
              void current.refetch();
              void history.refetch();
              setRevision((value) => value + 1);
            }}
          />
        )}
      <WorkflowRequests
        key={revision}
        employeeId={employeeId}
        kind="lifecycle"
        onDecided={() => {
          void current.refetch();
          void history.refetch();
        }}
      />
      <section className="hr-form-section">
        <h2>Status history</h2>
        <QueryState query={history}>
          {!history.data?.items.length && <p className="muted">No status history.</p>}
          {history.data?.items.map((r) => (
            <article className="hr-repeat" key={r.id}>
              <h3>
                {employmentLabels[r.previousStatus]} to {employmentLabels[r.newStatus]}
              </h3>
              <p>{r.reason}</p>
              {r.lifecyclePolicyVersion && (
                <p className="muted">Lifecycle policy: {r.lifecyclePolicyVersion}</p>
              )}
              <p className="muted">
                Effective {labelDate(r.effectiveAt)} (Pakistan) &middot; {r.actorAccountId}
              </p>
            </article>
          ))}
          {history.data && (
            <Pagination
              page={page}
              pageSize={10}
              total={history.data.totalCount}
              onChange={setPage}
            />
          )}
        </QueryState>
      </section>
      {edit && <Link to="?section=lifecycle">Back to current status</Link>}
    </>
  );
}
