import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Plus } from 'lucide-react';
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
import { SalaryRequestForm } from './SalaryRequestForm';
import { StatusRequestForm } from './StatusRequestForm';
import { WorkflowRequests } from './WorkflowRequests';
import { CompensationView } from './CompensationView';
import { EmployeeArchive } from '../employee/EmployeeArchive';
import { statusRequestChoices } from './lifecycle-status';

export function SalaryWorkspace({ employeeId, edit }: { employeeId: string; edit: boolean }) {
  const access = useHrAccess();
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
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
    <>
      <h2>Salary</h2>
      <QueryState query={current}>
        {current.data && (
          <>
            <p className="muted">As of {current.data.asOf}</p>
            {edit ? (
              access.can('hr.employees.compensation.request') && !current.data.nextScheduled ? (
                <SalaryRequestForm
                  key={current.data.employeeVersion}
                  current={current.data}
                  onSaved={() => setRevision((v) => v + 1)}
                />
              ) : (
                <p className="hr-message hr-message--error">
                  A new salary request is not available.
                </p>
              )
            ) : (
              <>
                {current.data.effective ? (
                  <CompensationView terms={current.data.effective.terms} />
                ) : (
                  <p>No current salary record.</p>
                )}
                {current.data.nextScheduled && (
                  <section className="hr-form-section">
                    <h3>Scheduled for {current.data.nextScheduled.effectiveFrom}</h3>
                    <CompensationView terms={current.data.nextScheduled.terms} />
                  </section>
                )}
                {access.can('hr.employees.compensation.request') && !current.data.nextScheduled && (
                  <div className="hr-actions">
                    <Link className="button button--secondary" to="?section=compensation&edit=1">
                      <Plus size={16} />
                      New salary request
                    </Link>
                  </div>
                )}
              </>
            )}
          </>
        )}
      </QueryState>
      <WorkflowRequests
        key={revision}
        employeeId={employeeId}
        kind="compensation"
        onDecided={() => {
          void current.refetch();
          void history.refetch();
        }}
      />
      <section className="hr-form-section">
        <h2>Salary history</h2>
        <QueryState query={history}>
          {!history.data?.items.length && <p className="muted">No salary history.</p>}
          {history.data?.items.map((r) => (
            <article className="hr-repeat" key={r.id}>
              <h3>
                Revision {r.revision} &middot; {r.effectiveFrom} to {r.effectiveTo ?? 'ongoing'}
              </h3>
              <CompensationView terms={r.terms} />
              <p className="muted">
                Recorded {labelDate(r.recordedAt)} &middot; {r.recordedByUserId}
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
      {edit && <Link to="?section=compensation">Back to current salary</Link>}
    </>
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
