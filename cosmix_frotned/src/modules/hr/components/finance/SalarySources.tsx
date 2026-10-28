import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../../hooks/use-finance-queries';
import { salaryComponent, salaryComponentPage, salaryPolicy } from '../../schemas/payroll-salary';
import { ActionMessage } from '../Feedback';
import { FinanceQueryState } from './FinanceQueryState';
import { FinanceNotice, HasMorePager, TextAreaField } from './FinanceChrome';
import { PayrollEmployeePicker, PayrollSelect } from './PayrollFields';
import { money } from './labels';
const root = '/hr/payroll/salary';
const options = (labels: string[]) =>
  labels.map((label, index) => ({ label, value: String(index + 1) }));
export function SalarySources() {
  const access = useFinanceAccess();
  const action = useFinanceAction();
  const [employeeId, setEmployeeId] = useState('');
  const [page, setPage] = useState(1);
  const [code, setCode] = useState('');
  const [kind, setKind] = useState('');
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('');
  const [effectiveFrom, setFrom] = useState('');
  const [effectiveTo, setTo] = useState('');
  const [prorate, setProrate] = useState(false);
  const [previous, setPrevious] = useState('');
  const [reason, setReason] = useState('');
  const [policyFrom, setPolicyFrom] = useState('');
  const [policyPrevious, setPolicyPrevious] = useState('');
  const [proration, setProration] = useState('');
  const [divisor, setDivisor] = useState('');
  const [rounding, setRounding] = useState('');
  const [gross, setGross] = useState('');
  const policies = useFinanceQuery(
    `${root}/policies`,
    z.array(salaryPolicy),
    access.can('attendance.payroll-salary.read'),
  );
  const components = useFinanceQuery(
    `${root}/components?page=${page}&pageSize=25${employeeId ? `&employeeId=${encodeURIComponent(employeeId)}` : ''}`,
    salaryComponentPage,
    access.can('attendance.payroll-salary.read'),
  );
  async function submitComponent(event: FormEvent) {
    event.preventDefault();
    const result = await action.run(
      `${root}/components`,
      {
        employeeId,
        code,
        kind: Number(kind),
        amount: Number(amount),
        currency: currency.toUpperCase(),
        effectiveFrom,
        effectiveToExclusive: effectiveTo || null,
        prorateWithBase: prorate,
        expectedPreviousId: previous || null,
        reason,
      },
      'POST',
      salaryComponent,
    );
    if (result) void components.refetch();
  }
  async function submitPolicy(event: FormEvent) {
    event.preventDefault();
    const result = await action.run(
      `${root}/policies`,
      {
        effectiveFrom: policyFrom,
        rules: {
          schemaVersion: 1,
          proration: Number(proration),
          fixedDivisorDays: proration === '2' ? Number(divisor) : null,
          rounding: Number(rounding),
          grossTreatment: Number(gross),
        },
        expectedPreviousId: policyPrevious || null,
        reason,
      },
      'POST',
      salaryPolicy,
    );
    if (result) void policies.refetch();
  }
  async function review(
    type: 'components' | 'policies',
    id: string,
    expectedVersion: string,
    decision: number,
  ) {
    if (type === 'components') {
      const result = await action.run(
        `${root}/components/${id}/review`,
        { expectedVersion, decision, reason },
        'POST',
        salaryComponent,
      );
      if (result) void components.refetch();
    } else {
      const result = await action.run(
        `${root}/policies/${id}/review`,
        { expectedVersion, decision, reason },
        'POST',
        salaryPolicy,
      );
      if (result) void policies.refetch();
    }
  }
  return (
    <section className="payroll-section-card">
      <h2>Salary sources and rules</h2>
      <FinanceNotice>
        Monthly, Daily and Hourly rates come from each employee’s approved compensation. Enter and
        review rate changes in the existing employee salary record. Components and policy changes
        below require independent approval.
      </FinanceNotice>
      <PayrollEmployeePicker
        value={employeeId}
        onChange={(value) => {
          setEmployeeId(value);
          setPage(1);
        }}
      />
      {employeeId && (
        <Link to={`/hr/employees/${employeeId}?section=compensation`}>
          Open employee salary master
        </Link>
      )}
      <TextAreaField
        label="Reason for request or review"
        value={reason}
        onChange={setReason}
        required
      />
      <FinanceQueryState query={components}>
        <h3>Approved and proposed components</h3>
        <div className="payroll-table-wrapper">
          <table className="payroll-table">
            <thead>
              <tr>
                <th>Employee / component</th>
                <th>Amount</th>
                <th>Effective dates</th>
                <th>Status</th>
                <th>Review</th>
              </tr>
            </thead>
            <tbody>
              {components.data?.items.map((item) => (
                <tr key={item.id}>
                  <td>
                    {item.employeeId}
                    <br />
                    {item.code}
                  </td>
                  <td>{money(item.amount, item.currency)}</td>
                  <td>
                    {item.effectiveFrom} → {item.effectiveToExclusive ?? 'Ongoing'}
                  </td>
                  <td>{item.state}</td>
                  <td>
                    {item.state.toLowerCase() === 'pending' &&
                      access.can('attendance.payroll-salary.review') && (
                        <>
                          <Button
                            disabled={action.pending || !reason.trim()}
                            onClick={() => void review('components', item.id, item.version, 1)}
                          >
                            Approve
                          </Button>
                          <Button
                            variant="secondary"
                            disabled={action.pending || !reason.trim()}
                            onClick={() => void review('components', item.id, item.version, 3)}
                          >
                            Reject
                          </Button>
                        </>
                      )}
                    <Button
                      variant="secondary"
                      onClick={() => {
                        setEmployeeId(item.employeeId);
                        setCode(item.code);
                        setPrevious(item.id);
                      }}
                    >
                      Propose replacement
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <HasMorePager
          page={page}
          hasMore={components.data?.hasMore ?? false}
          busy={components.isFetching}
          onPrevious={() => setPage(page - 1)}
          onNext={() => setPage(page + 1)}
        />
      </FinanceQueryState>
      {access.can('attendance.payroll-salary.request') && (
        <form onSubmit={(event) => void submitComponent(event)}>
          <h3>Propose earning or deduction</h3>
          <fieldset disabled={action.pending}>
            <div className="hr-form-grid">
              <TextInput
                label="Component code"
                required
                value={code}
                onChange={(event) => setCode(event.target.value)}
              />
              <PayrollSelect
                label="Component type"
                value={kind}
                onChange={setKind}
                options={options([
                  'Fixed earning',
                  'Fixed deduction',
                  'One-time earning',
                  'One-time deduction',
                ])}
              />
              <TextInput
                label="Amount"
                type="number"
                required
                min="0"
                step="0.01"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
              />
              <TextInput
                label="Currency"
                required
                pattern="[A-Z]{3}"
                value={currency}
                onChange={(event) => setCurrency(event.target.value.toUpperCase())}
              />
              <TextInput
                label="Effective from"
                type="date"
                required
                value={effectiveFrom}
                onChange={(event) => setFrom(event.target.value)}
              />
              <TextInput
                label="Effective end (exclusive)"
                type="date"
                value={effectiveTo}
                onChange={(event) => setTo(event.target.value)}
              />
              <TextInput
                label="Replaces component ID (optional)"
                value={previous}
                onChange={(event) => setPrevious(event.target.value)}
              />
              <label>
                <input
                  type="checkbox"
                  checked={prorate}
                  onChange={(event) => setProrate(event.target.checked)}
                />{' '}
                Prorate with base salary
              </label>
            </div>
            <Button type="submit" disabled={!employeeId || !reason.trim()}>
              Submit component for review
            </Button>
          </fieldset>
        </form>
      )}
      <FinanceQueryState query={policies}>
        <h3>Effective salary policies</h3>
        {policies.data?.map((item) => (
          <article key={item.id} className="payroll-batch-card">
            <p>
              Revision {item.revision} · {item.effectiveFrom} · {item.state}
            </p>
            <p>
              Proration:{' '}
              {item.rules.proration === 1
                ? 'Calendar days'
                : `Fixed divisor ${item.rules.fixedDivisorDays}`}{' '}
              · Rounding: {item.rules.rounding === 1 ? 'Half up' : 'Half even'} · Gross:{' '}
              {item.rules.grossTreatment === 1 ? 'Base only' : 'Reconcile approved fixed earnings'}
            </p>
            {item.state.toLowerCase() === 'pending' &&
              access.can('attendance.payroll-salary.review') && (
                <>
                  <Button
                    disabled={action.pending || !reason.trim()}
                    onClick={() => void review('policies', item.id, item.version, 1)}
                  >
                    Approve policy
                  </Button>
                  <Button
                    variant="secondary"
                    disabled={action.pending || !reason.trim()}
                    onClick={() => void review('policies', item.id, item.version, 3)}
                  >
                    Reject policy
                  </Button>
                </>
              )}
            <Button variant="secondary" onClick={() => setPolicyPrevious(item.id)}>
              Use as previous policy
            </Button>
          </article>
        ))}
      </FinanceQueryState>
      {access.can('attendance.payroll-salary.request') && (
        <form onSubmit={(event) => void submitPolicy(event)}>
          <h3>Propose salary policy</h3>
          <fieldset disabled={action.pending}>
            <div className="hr-form-grid">
              <TextInput
                label="Effective from"
                type="date"
                required
                value={policyFrom}
                onChange={(event) => setPolicyFrom(event.target.value)}
              />
              <TextInput
                label="Previous policy ID (optional)"
                value={policyPrevious}
                onChange={(event) => setPolicyPrevious(event.target.value)}
              />
              <PayrollSelect
                label="Monthly proration"
                value={proration}
                onChange={setProration}
                options={options(['Calendar days', 'Fixed earned days'])}
              />
              {proration === '2' && (
                <TextInput
                  label="Approved divisor days"
                  type="number"
                  min="1"
                  max="366"
                  required
                  value={divisor}
                  onChange={(event) => setDivisor(event.target.value)}
                />
              )}
              <PayrollSelect
                label="Money rounding"
                value={rounding}
                onChange={setRounding}
                options={options(['Half up', 'Half even'])}
              />
              <PayrollSelect
                label="Gross treatment"
                value={gross}
                onChange={setGross}
                options={options(['Base only', 'Reconcile approved fixed earnings'])}
              />
            </div>
            <Button type="submit" disabled={!reason.trim()}>
              Submit policy for review
            </Button>
          </fieldset>
        </form>
      )}
      <ActionMessage action={action} />
    </section>
  );
}
