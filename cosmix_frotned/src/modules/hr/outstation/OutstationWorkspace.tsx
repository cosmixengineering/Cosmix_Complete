import { EmployeePicker } from '@/components/employees/EmployeeReference';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Check, Eye, Plus, RefreshCw, Search } from 'lucide-react';
import { useAuth } from '@/auth/use-auth';
import { Button } from '@/components/ui/Button';
import { Denied } from '../components/Feedback';
import { RecordView } from '../components/RecordView';
import { HasMorePager } from '../components/finance/FinanceChrome';
import { useDirtyForm } from '../hooks/use-dirty-form';
import {
  canDeployment,
  deploymentCalculation,
  deploymentId,
  deploymentPath,
  detailFor,
  evidenceFor,
  historyFor,
  type DeploymentClaim,
  type DeploymentDetail,
} from './contracts';
import { canRunDeployment, type FormTarget } from './commands';
import { OutstationSession, useOutstationQuery } from './session';
import { OutstationForm } from './OutstationForm';
import { CalculationEvidence, OutstationRead } from './OutstationEvidence';

export function OutstationWorkspace({ onBusyChange }: { onBusyChange: (busy: boolean) => void }) {
  const { user, status } = useAuth();
  const epoch = useMemo(() => crypto.randomUUID(), [user, status]);
  if (status !== 'authenticated' || !canDeployment(user, 'read')) return <Denied />;
  return (
    <OutstationSession key={epoch}>
      <Workspace onBusyChange={onBusyChange} />
    </OutstationSession>
  );
}
function Workspace({ onBusyChange }: { onBusyChange: (busy: boolean) => void }) {
  const { user } = useAuth();
  const [input, setInput] = useState('');
  const [employeeId, setEmployeeId] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    onBusyChange(busy);
    return () => onBusyChange(false);
  }, [busy, onBusyChange]);
  return (
    <section aria-label="Outstation management" style={{ minWidth: 0, overflowWrap: 'anywhere' }}>
      <h2>Outstation management</h2>
      <form
        className="hr-toolbar"
        onSubmit={(event) => {
          event.preventDefault();
          if (busy) return;
          const parsed = deploymentId.safeParse(input.trim());
          if (!parsed.success) {
            setEmployeeId('');
            setError('Select an employee.');
            return;
          }
          if (!canDeployment(user, 'read', parsed.data)) {
            setEmployeeId('');
            setError('No deployment read grant for this employee.');
            return;
          }
          setError('');
          setEmployeeId(parsed.data.toLowerCase());
        }}
      >
        <EmployeePicker
          label="Employee"
          required
          disabled={busy}
          value={input}
          onChange={(id) => {
            setInput(id);
            setEmployeeId('');
            setError('');
          }}
        />
        <Button type="submit" variant="secondary" disabled={busy}>
          <Search size={16} />
          Load employee
        </Button>
      </form>
      {error && <p role="alert">{error}</p>}
      {!employeeId ? (
        <p role="status">No employee selected.</p>
      ) : (
        <EmployeeDeployments key={employeeId} employeeId={employeeId} onBusyChange={setBusy} />
      )}
    </section>
  );
}
function EmployeeDeployments({
  employeeId,
  onBusyChange,
}: {
  employeeId: string;
  onBusyChange: (busy: boolean) => void;
}) {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [id, setId] = useState('');
  const [form, setForm] = useState<FormTarget | null>(null);
  const [generation, setGeneration] = useState(0);
  const [saved, setSaved] = useState(false);
  // Navigation stays locked for the entire form, including an uncertain outcome.
  const busy = form !== null;
  useDirtyForm(busy);
  useEffect(() => {
    onBusyChange(busy);
    return () => onBusyChange(false);
  }, [busy, onBusyChange]);
  const history = useOutstationQuery(
    deploymentPath(employeeId) + `?page=${page}&pageSize=25`,
    historyFor(employeeId),
    employeeId,
    !form,
  );
  const close = useCallback(
    (success = false) => {
      setForm(null);
      setSaved(success);
      setGeneration((g) => g + 1);
      void history.refetch();
    },
    [history],
  );
  if (form)
    return (
      <OutstationForm
        employeeId={employeeId}
        target={form}
        onSaved={() => close(true)}
        onClose={() => close()}
      />
    );
  return (
    <>
      {saved && (
        <p className="hr-message" role="status">
          Request or decision recorded. Current records are reloaded below.
        </p>
      )}
      <div className="hr-actions">
        {canRunDeployment(user, employeeId, { kind: 'request' }) && (
          <Button
            onClick={() => {
              setSaved(false);
              setForm({ kind: 'request' });
            }}
          >
            <Plus size={16} />
            Request deployment
          </Button>
        )}
        <Button
          variant="secondary"
          disabled={history.isFetching}
          onClick={() => {
            setGeneration((g) => g + 1);
            void history.refetch();
          }}
        >
          <RefreshCw size={16} />
          Reload history
        </Button>
      </div>
      <h3>Deployment history</h3>
      <OutstationRead query={history}>
        {!history.data?.length ? (
          <p role="status">No deployments returned on page {page}.</p>
        ) : (
          <div className="hr-table-scroll">
            <table className="hr-prepared-table">
              <thead>
                <tr>
                  <th>Deployment</th>
                  <th>UTC interval</th>
                  <th>State</th>
                  <th>Revision</th>
                  <th>Review</th>
                </tr>
              </thead>
              <tbody>
                {history.data.map((row) => (
                  <tr key={row.id}>
                    <td>{row.id}</td>
                    <td>
                      {row.startsAtUtc}
                      <br />
                      {row.endsAtUtc}
                    </td>
                    <td>{row.state}</td>
                    <td>{row.revision}</td>
                    <td>
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setId(row.id);
                          setGeneration((g) => g + 1);
                        }}
                      >
                        <Eye size={16} />
                        Open deployment
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <HasMorePager
          page={page}
          hasMore={page < 4096 && history.data?.length === 25}
          busy={history.isFetching}
          onPrevious={() => {
            setId('');
            setPage((p) => p - 1);
          }}
          onNext={() => {
            setId('');
            setPage((p) => p + 1);
          }}
        />
      </OutstationRead>
      {id && (
        <DeploymentReview
          key={`${id}:${generation}`}
          employeeId={employeeId}
          id={id}
          onForm={(target) => {
            setSaved(false);
            setForm(target);
          }}
        />
      )}
    </>
  );
}
function DeploymentReview({
  employeeId,
  id,
  onForm,
}: {
  employeeId: string;
  id: string;
  onForm: (target: FormTarget) => void;
}) {
  const { user } = useAuth();
  const [calculation, setCalculation] = useState(false);
  const [claimId, setClaimId] = useState('');
  const detail = useOutstationQuery(
    deploymentPath(employeeId, id),
    detailFor(employeeId, id),
    employeeId,
  );
  const data = detail.isSuccess && !detail.isFetching ? detail.data : undefined;
  return (
    <section className="hr-form-section">
      <h3>Deployment review</h3>
      <OutstationRead query={detail}>
        {data && (
          <>
            <RecordView value={{ ...data.deployment, ...data.proposal }} />
            {canRunDeployment(user, employeeId, { kind: 'deployment-decision', detail: data }) && (
              <Button onClick={() => onForm({ kind: 'deployment-decision', detail: data })}>
                <Check size={16} />
                Admin deployment decision
              </Button>
            )}
            <section className="hr-form-section">
              <h3>Home-break history</h3>
              {!data.homeBreaks.length && <p>No home breaks recorded.</p>}
              {!!data.homeBreaks.length && (
                <div className="hr-table-scroll">
                  <table className="hr-prepared-table">
                    <thead>
                      <tr>
                        <th>Reference</th>
                        <th>Departed / returned (UTC)</th>
                        <th>State</th>
                        <th>Approval version</th>
                        <th>Review</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.homeBreaks.map((b) => (
                        <tr key={b.id}>
                          <td>{b.id}</td>
                          <td>
                            {b.departedAtUtc}
                            <br />
                            {b.returnedAtUtc ?? 'Return not recorded'}
                          </td>
                          <td>{b.state}</td>
                          <td>{b.approvalVersion}</td>
                          <td>
                            {canRunDeployment(user, employeeId, {
                              kind: 'break-decision',
                              id: b.id,
                              detail: data,
                            }) ? (
                              <Button
                                variant="secondary"
                                onClick={() =>
                                  onForm({ kind: 'break-decision', id: b.id, detail: data })
                                }
                              >
                                <Check size={16} />
                                Admin review
                              </Button>
                            ) : (
                              'No action'
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {canRunDeployment(user, employeeId, { kind: 'break', detail: data }) && (
                <Button variant="secondary" onClick={() => onForm({ kind: 'break', detail: data })}>
                  <Plus size={16} />
                  Request home break / return
                </Button>
              )}
            </section>
            <div className="hr-actions">
              <Button variant="secondary" onClick={() => setCalculation((value) => !value)}>
                <Eye size={16} />
                {calculation ? 'Close eligibility' : 'Inspect eligibility'}
              </Button>
            </div>
            {calculation && <Eligibility employeeId={employeeId} id={id} />}
            <section className="hr-form-section">
              <h3>Retained claim history</h3>
              {!data.claims.length && (
                <p>No retained claim exists. Employee claim submission is mobile-only.</p>
              )}
              {data.claims.map((claim) => (
                <article key={claim.id}>
                  <RecordView value={claim} />
                  <Button
                    variant="secondary"
                    onClick={() => setClaimId(claimId === claim.id ? '' : claim.id)}
                  >
                    <Eye size={16} />
                    {claimId === claim.id ? 'Close retained evidence' : 'Inspect retained evidence'}
                  </Button>
                  {claimId === claim.id && (
                    <RetainedClaim
                      key={`${claim.id}:${claim.approvalVersion}`}
                      employeeId={employeeId}
                      detail={data}
                      claim={claim}
                      onForm={onForm}
                    />
                  )}
                </article>
              ))}
            </section>
          </>
        )}
      </OutstationRead>
    </section>
  );
}
function Eligibility({ employeeId, id }: { employeeId: string; id: string }) {
  const query = useOutstationQuery(
    deploymentPath(employeeId, id) + '/calculation',
    deploymentCalculation,
    employeeId,
  );
  return (
    <OutstationRead query={query}>
      {query.data && <CalculationEvidence value={query.data} />}
    </OutstationRead>
  );
}
function RetainedClaim({
  employeeId,
  detail,
  claim,
  onForm,
}: {
  employeeId: string;
  detail: DeploymentDetail;
  claim: DeploymentClaim;
  onForm: (target: FormTarget) => void;
}) {
  const { user } = useAuth();
  const query = useOutstationQuery(
    deploymentPath(employeeId, detail.deployment.id) + `/claims/${claim.id}/evidence`,
    evidenceFor(employeeId, detail.deployment.id, claim),
    employeeId,
  );
  const evidence = query.isSuccess && !query.isFetching ? query.data : undefined;
  const target: FormTarget = { kind: 'claim-decision', id: claim.id, detail, evidence };
  return (
    <OutstationRead query={query}>
      {evidence && (
        <>
          <RecordView
            value={{
              requesterId: evidence.requesterId,
              ...evidence.proposal,
              deploymentRequesterId: evidence.deployment.requesterId,
            }}
          />
          <CalculationEvidence value={evidence.calculation} retained />
          {canRunDeployment(user, employeeId, target) && (
            <Button onClick={() => onForm(target)}>
              <Check size={16} />
              Admin retained-claim review
            </Button>
          )}
        </>
      )}
    </OutstationRead>
  );
}
