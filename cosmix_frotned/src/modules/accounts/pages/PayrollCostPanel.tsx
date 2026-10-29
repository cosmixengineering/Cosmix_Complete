import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/auth/use-auth';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { request } from '@/services/api/http-client';
import { useFinanceCommand } from '../hooks/use-finance-command';
import {
  payrollCostPlanSchema,
  payrollCostWorkspaceSchema,
  type PayrollCostWorkspace,
} from '../schemas/payroll-costs';

const amount = (value: number, currency: string) =>
  `${currency} ${value.toLocaleString('en-PK', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

export function PayrollCostPanel({ batchId }: { batchId: string }) {
  const { user, status } = useAuth();
  const can = (permission: string) =>
    status === 'authenticated' &&
    Boolean(
      user?.grants.some(
        (grant) =>
          grant.permission === permission && grant.scopeKind === 7 && grant.targetId === null,
      ),
    );
  const readable = can('finance.payroll-costs.read');
  const query = useQuery({
    queryKey: ['finance', user?.account?.accountId, 'payroll-costs', batchId],
    queryFn: async ({ signal }) =>
      payrollCostWorkspaceSchema.parse(
        await request(`/finance/payroll-costs/batches/${batchId}`, { signal }),
      ),
    enabled: readable,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
  });
  const command = useFinanceCommand();
  const [reviewReason, setReviewReason] = useState('');
  if (!readable) return null;
  if (query.isFetching) return <p role="status">Loading payroll cost allocation…</p>;
  if (query.isError)
    return (
      <section>
        <h3>Project and site costs</h3>
        <p role="alert">
          Cost allocation could not be loaded. Salary accrual and applicable employer contributions
          must be posted first.
        </p>
        <Button variant="secondary" onClick={() => void query.refetch()}>
          Refresh cost allocation
        </Button>
      </section>
    );
  const data = query.data;
  if (!data) return null;
  const employeeName = (id: string) =>
    data.employees.find((row) => row.employeeId === id)?.employeeName ?? id;
  const targetName = (projectId: string | null, siteId: string | null) =>
    siteId
      ? (data.targets.sites.find((row) => row.id === siteId)?.name ?? siteId)
      : projectId
        ? (data.targets.projects.find((row) => row.id === projectId)?.name ?? projectId)
        : 'General overhead';
  const effective = data.plans.find((plan) => plan.id === data.effectivePlanId);
  const pending = data.plans.find((plan) => plan.state === 'Draft');
  async function decide(approve: boolean) {
    if (!pending || !reviewReason.trim() || !can('finance.payroll-costs.approve')) return;
    const result = await command.run(
      `/finance/payroll-costs/plans/${pending.id}/decision`,
      { expectedVersion: pending.version, approve, reason: reviewReason },
      payrollCostPlanSchema,
    );
    if (result) {
      setReviewReason('');
      await query.refetch();
    }
  }
  return (
    <section className="payroll-section-card">
      <h3>Project and site costs</h3>
      <p>
        Allocate posted payroll expense, including posted employer contributions. This changes cost
        classification only; employee pay stays unchanged.
      </p>
      <p>
        {effective
          ? `Approved allocation: revision ${effective.revision}`
          : 'No approved allocation. These costs remain unallocated.'}
      </p>
      {effective && (
        <div className="payroll-table-wrapper">
          <table className="payroll-table">
            <thead>
              <tr>
                <th>Employee</th>
                <th>Project / site</th>
                <th>Share</th>
                <th>Allocated expense</th>
              </tr>
            </thead>
            <tbody>
              {effective.costs.map((row) => (
                <tr key={`${row.employeeId}:${row.projectId}:${row.siteId}`}>
                  <td>{employeeName(row.employeeId)}</td>
                  <td>{targetName(row.projectId, row.siteId)}</td>
                  <td>{row.basisPoints / 100}%</td>
                  <td>{amount(row.amount, row.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {pending && (
        <div>
          <h4>Proposed revision {pending.revision}</h4>
          <p>{pending.reason}</p>
          <div className="payroll-table-wrapper">
            <table className="payroll-table">
              <thead>
                <tr>
                  <th>Employee</th>
                  <th>Project / site</th>
                  <th>Share</th>
                  <th>Expense</th>
                </tr>
              </thead>
              <tbody>
                {pending.costs.map((row) => (
                  <tr key={`${row.employeeId}:${row.projectId}:${row.siteId}`}>
                    <td>{employeeName(row.employeeId)}</td>
                    <td>{targetName(row.projectId, row.siteId)}</td>
                    <td>{row.basisPoints / 100}%</td>
                    <td>{amount(row.amount, row.currency)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {can('finance.payroll-costs.approve') &&
          pending.createdBy !== user?.account?.accountId &&
          pending.createdByEmployeeId !== user?.account?.employeeId ? (
            <>
              <TextInput
                label="Review reason"
                value={reviewReason}
                maxLength={1000}
                disabled={command.pending || command.uncertain}
                onChange={(event) => setReviewReason(event.target.value)}
              />
              <Button
                disabled={command.pending || !reviewReason.trim()}
                onClick={() => void decide(true)}
              >
                Approve allocation
              </Button>{' '}
              <Button
                variant="secondary"
                disabled={command.pending || !reviewReason.trim()}
                onClick={() => void decide(false)}
              >
                Reject allocation
              </Button>
            </>
          ) : (
            <p>A different authorized employee must review this proposal.</p>
          )}
        </div>
      )}
      {can('finance.payroll-costs.manage') && !pending && (
        <CostDraft
          key={`${data.sourceFingerprint}:${data.effectivePlanId}`}
          data={data}
          onSaved={() => query.refetch()}
        />
      )}
      {data.plans.length > 0 && (
        <details>
          <summary>Allocation revision history ({data.plans.length})</summary>
          <ul>
            {data.plans.map((plan) => (
              <li key={plan.id}>
                Revision {plan.revision} — {plan.state} — {plan.reason}
              </li>
            ))}
          </ul>
        </details>
      )}
      {command.error && <p role="alert">{command.error}</p>}
    </section>
  );
}

function CostDraft({
  data,
  onSaved,
}: {
  data: PayrollCostWorkspace;
  onSaved: () => Promise<unknown>;
}) {
  const [rows, setRows] = useState(() =>
    data.employees.map((employee) => ({
      key: crypto.randomUUID(),
      employeeId: employee.employeeId,
      target: '',
      percent: '100',
    })),
  );
  const [reason, setReason] = useState('');
  const [commonTarget, setCommonTarget] = useState('');
  const command = useFinanceCommand();
  const options = [
    { value: 'general', label: 'General overhead' },
    ...data.targets.projects.map((project) => ({
      value: `project:${project.id}`,
      label: `Project — ${project.code}: ${project.name}`,
    })),
    ...data.targets.sites.map((site) => ({
      value: `site:${site.id}`,
      label: `Site — ${site.code}: ${site.name}`,
    })),
  ];
  const points = (value: string) =>
    /^\d+(\.\d{1,2})?$/.test(value) ? Math.round(Number(value) * 100) : 0;
  const valid =
    rows.every((row) => row.target && points(row.percent) > 0 && points(row.percent) <= 10000) &&
    new Set(rows.map((row) => `${row.employeeId}:${row.target}`)).size === rows.length &&
    data.employees.every(
      (employee) =>
        rows
          .filter((row) => row.employeeId === employee.employeeId)
          .reduce((total, row) => total + points(row.percent), 0) === 10000,
    );
  function targetFields(target: string) {
    if (target.startsWith('site:')) {
      const site = data.targets.sites.find((row) => row.id === target.slice(5));
      return { siteId: site?.id ?? null, projectId: site?.projectId ?? null };
    }
    return { projectId: target.startsWith('project:') ? target.slice(8) : null, siteId: null };
  }
  async function submit() {
    if (!valid || !reason.trim()) return;
    const result = await command.run(
      `/finance/payroll-costs/batches/${data.batchId}`,
      {
        expectedSourceFingerprint: data.sourceFingerprint,
        expectedPreviousPlanId: data.effectivePlanId,
        shares: rows.map((row) => ({
          employeeId: row.employeeId,
          ...targetFields(row.target),
          basisPoints: points(row.percent),
        })),
        reason,
      },
      payrollCostPlanSchema,
    );
    if (result) await onSaved();
  }
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <h4>Propose allocation</h4>
      <fieldset disabled={command.pending || command.uncertain}>
        <label>
          Common destination{' '}
          <select value={commonTarget} onChange={(event) => setCommonTarget(event.target.value)}>
            <option value="">Select destination</option>
            {options.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>{' '}
        <Button
          type="button"
          variant="secondary"
          disabled={!commonTarget}
          onClick={() =>
            setRows(
              data.employees.map((employee) => ({
                key: crypto.randomUUID(),
                employeeId: employee.employeeId,
                target: commonTarget,
                percent: '100',
              })),
            )
          }
        >
          Apply to all employees
        </Button>
        <div className="payroll-table-wrapper">
          <table className="payroll-table">
            <thead>
              <tr>
                <th>Employee</th>
                <th>Expense to allocate</th>
                <th>Destination</th>
                <th>Share %</th>
                <th>Split</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => {
                const employee = data.employees.find((item) => item.employeeId === row.employeeId)!;
                return (
                  <tr key={row.key}>
                    <td>{employee.employeeName ?? employee.employeeId}</td>
                    <td>{amount(employee.amount, employee.currency)}</td>
                    <td>
                      <select
                        aria-label={`Destination for ${employee.employeeName ?? employee.employeeId}`}
                        value={row.target}
                        onChange={(event) =>
                          setRows(
                            rows.map((item) =>
                              item.key === row.key ? { ...item, target: event.target.value } : item,
                            ),
                          )
                        }
                      >
                        <option value="">Select destination</option>
                        {options.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <input
                        aria-label={`Allocation percentage for ${employee.employeeName ?? employee.employeeId}`}
                        type="number"
                        min="0.01"
                        max="100"
                        step="0.01"
                        value={row.percent}
                        onChange={(event) =>
                          setRows(
                            rows.map((item) =>
                              item.key === row.key
                                ? { ...item, percent: event.target.value }
                                : item,
                            ),
                          )
                        }
                      />
                    </td>
                    <td>
                      <Button
                        type="button"
                        variant="secondary"
                        onClick={() =>
                          setRows([
                            ...rows,
                            { ...row, key: crypto.randomUUID(), target: '', percent: '' },
                          ])
                        }
                      >
                        Add split
                      </Button>{' '}
                      {rows.filter((item) => item.employeeId === row.employeeId).length > 1 && (
                        <Button
                          type="button"
                          variant="secondary"
                          onClick={() => setRows(rows.filter((item) => item.key !== row.key))}
                        >
                          Remove
                        </Button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <p>
          Each employee must total 100%, with a different destination for each split. The server
          calculates and reconciles all allocated amounts.
        </p>
        <TextInput
          label="Allocation reason"
          value={reason}
          maxLength={1000}
          onChange={(event) => setReason(event.target.value)}
          required
        />
      </fieldset>
      <Button type="submit" disabled={command.pending || !valid || !reason.trim()}>
        Submit for independent review
      </Button>
      {command.error && <p role="alert">{command.error}</p>}
    </form>
  );
}
