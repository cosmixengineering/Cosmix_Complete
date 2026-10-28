import { useState, type FormEvent } from 'react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../../hooks/use-finance-queries';
import {
  account,
  accountingPeriod,
  accountingSetup,
  postingProfile,
  statutorySchemeCode,
} from '../../schemas/payroll-accounting';
import { ActionMessage } from '../Feedback';
import { FinanceQueryState } from './FinanceQueryState';
import { FinanceNotice, TextAreaField } from './FinanceChrome';
import { PayrollSelect } from './PayrollFields';
const root = '/payroll-accounting';
const deductionKinds: Record<string, string[]> = {
  AttendanceDeduction: ['Expense'],
  LoanRepayment: ['Asset'],
  TaxDeduction: ['Liability'],
  LeaveAdjustment: ['Expense', 'Asset', 'Liability'],
  SalaryAdjustment: ['Expense', 'Asset', 'Liability'],
  OtherApprovedAdjustment: ['Expense', 'Asset', 'Liability'],
};
const deductionComponents = [
  'BaseCompensation',
  'AttendanceDeduction',
  'Overtime',
  'Allowance',
  'LeaveAdjustment',
  'LoanRepayment',
  'FuelClaim',
  'OtherApprovedAdjustment',
  'FixedAllowance',
  'TaxDeduction',
  'SalaryAdjustment',
];
export function PayrollAccountingSetup() {
  const access = useFinanceAccess();
  const action = useFinanceAction();
  const data = useFinanceQuery(
    `${root}/setup`,
    accountingSetup,
    access.can('payroll.accounting.configure') ||
      access.can('payroll.accounting.configure.approve') ||
      access.can('payroll.accounting.accrue'),
  );
  const [reason, setReason] = useState('');
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [kind, setKind] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [currency, setCurrency] = useState('');
  const [profileName, setProfileName] = useState('');
  const [expense, setExpense] = useState('');
  const [payable, setPayable] = useState('');
  const [deductions, setDeductions] = useState<{ component: string; creditAccountCode: string }[]>(
    [],
  );
  const [statutory, setStatutory] = useState<
    { scheme: string; employerExpenseAccountCode: string; payableAccountCode: string }[]
  >([]);
  const choices = (accountKind: string) =>
    (data.data?.accounts ?? [])
      .filter((item) => item.isActive && item.kind === accountKind)
      .map((item) => ({ value: item.code, label: `${item.code} · ${item.name}` }));
  const deductionChoices = (component: string) =>
    (data.data?.accounts ?? [])
      .filter(
        (item) =>
          item.isActive &&
          (deductionKinds[component] ?? ['Liability', 'Asset']).includes(item.kind) &&
          ![expense.toLowerCase(), payable.toLowerCase()].includes(item.code.toLowerCase()),
      )
      .map((item) => ({ value: item.code, label: `${item.code} · ${item.name} (${item.kind})` }));
  async function write(path: string, body: unknown) {
    const result = await action.run(
      `${root}/${path}`,
      body,
      'POST',
      z.union([account, accountingPeriod, postingProfile]),
    );
    if (result) void data.refetch();
  }
  function submit(event: FormEvent, path: string, body: unknown) {
    event.preventDefault();
    void write(path, body);
  }
  return (
    <section className="payroll-section-card">
      <h2>Payroll accounting configuration</h2>
      <FinanceNotice>
        Accounts, periods and posting mappings must be entered from company-approved records.
        Periods and profiles require an independent approver before posting.
      </FinanceNotice>
      <TextAreaField
        label="Configuration / review reason"
        value={reason}
        onChange={setReason}
        required
      />
      <FinanceQueryState query={data}>
        <h3>Accounts</h3>
        {data.data?.accounts.map((item) => (
          <p key={item.id}>
            {item.code} · {item.name} · {item.kind} · {item.isActive ? 'Active' : 'Inactive'}{' '}
            {access.can('payroll.accounting.configure') && (
              <Button
                variant="secondary"
                disabled={action.pending || !reason.trim()}
                onClick={() =>
                  void write(`accounts/${item.id}/status`, {
                    isActive: !item.isActive,
                    expectedVersion: item.version,
                    reason,
                  })
                }
              >
                {item.isActive ? 'Deactivate' : 'Activate'}
              </Button>
            )}
          </p>
        ))}
        <h3>Financial periods</h3>
        {data.data?.periods.map((item) => (
          <p key={item.id}>
            {item.periodStart} → {item.periodEndExclusive} · {item.currency} · {item.state}{' '}
            {access.can('payroll.accounting.configure.approve') &&
              item.state === 'Draft' &&
              item.createdByUserId !== access.user?.account?.accountId && (
                <Button
                  disabled={action.pending || !reason.trim()}
                  onClick={() =>
                    void write(`periods/${item.id}/approve`, {
                      expectedVersion: item.version,
                      reason,
                    })
                  }
                >
                  Approve period
                </Button>
              )}
            {access.can('payroll.accounting.configure.approve') && item.state === 'Open' && (
              <Button
                variant="secondary"
                disabled={action.pending || !reason.trim()}
                onClick={() =>
                  void write(`periods/${item.id}/close`, { expectedVersion: item.version, reason })
                }
              >
                Close period
              </Button>
            )}
          </p>
        ))}
        <h3>Posting profiles</h3>
        {data.data?.profiles.map((item) => (
          <article className="payroll-batch-card" key={item.id}>
            <p>
              {item.name} · {item.currency} · {item.state} · {item.effectiveFrom} →{' '}
              {item.effectiveToExclusive ?? 'Ongoing'}
            </p>
            <p>
              Salary: {item.salaryExpenseAccountCode} → {item.salaryPayableAccountCode}
            </p>
            {item.deductions.map((line) => (
              <p key={line.component}>
                {line.component}: {line.creditAccountCode}
              </p>
            ))}
            {item.statutory.map((line) => (
              <p key={line.scheme}>
                {line.scheme}: {line.employerExpenseAccountCode} → {line.payableAccountCode}
              </p>
            ))}
            {access.can('payroll.accounting.configure.approve') &&
              item.state === 'Draft' &&
              item.createdByUserId !== access.user?.account?.accountId && (
                <Button
                  disabled={action.pending || !reason.trim()}
                  onClick={() =>
                    void write(`profiles/${item.id}/approve`, {
                      expectedVersion: item.version,
                      reason,
                    })
                  }
                >
                  Approve profile
                </Button>
              )}
          </article>
        ))}
      </FinanceQueryState>
      {access.can('payroll.accounting.configure') && (
        <fieldset disabled={action.pending}>
          <form
            onSubmit={(event) =>
              submit(event, 'accounts', { code, name, kind: Number(kind), reason })
            }
          >
            <h3>Add account</h3>
            <div className="hr-form-grid">
              <TextInput
                label="Account code"
                required
                value={code}
                onChange={(event) => setCode(event.target.value)}
              />
              <TextInput
                label="Account name"
                required
                value={name}
                onChange={(event) => setName(event.target.value)}
              />
              <PayrollSelect
                label="Account type"
                value={kind}
                onChange={setKind}
                options={['Expense', 'Liability', 'Asset', 'Bank'].map((label, index) => ({
                  value: String(index + 1),
                  label,
                }))}
              />
            </div>
            <Button type="submit" disabled={!reason.trim()}>
              Save account
            </Button>
          </form>
          <h3>Period / profile dates</h3>
          <div className="hr-form-grid">
            <TextInput
              label="Starts on"
              type="date"
              required
              value={start}
              onChange={(event) => setStart(event.target.value)}
            />
            <TextInput
              label="Ends before"
              type="date"
              value={end}
              onChange={(event) => setEnd(event.target.value)}
            />
            <TextInput
              label="Currency"
              required
              pattern="[A-Z]{3}"
              value={currency}
              onChange={(event) => setCurrency(event.target.value.toUpperCase())}
            />
          </div>
          <Button
            disabled={!reason.trim() || !start || !end || !currency}
            onClick={() =>
              void write('periods', {
                periodStart: start,
                periodEndExclusive: end,
                currency,
                reason,
              })
            }
          >
            Submit financial period
          </Button>
          <form
            onSubmit={(event) =>
              submit(event, 'profiles', {
                name: profileName,
                currency,
                effectiveFrom: start,
                effectiveToExclusive: end || null,
                salaryExpenseAccountCode: expense,
                salaryPayableAccountCode: payable,
                deductions,
                statutory: statutory.map((line) => ({
                  ...line,
                  scheme: statutorySchemeCode(line.scheme),
                })),
                reason,
              })
            }
          >
            <h3>New posting profile</h3>
            <div className="hr-form-grid">
              <TextInput
                label="Profile name"
                required
                value={profileName}
                onChange={(event) => setProfileName(event.target.value)}
              />
              <PayrollSelect
                label="Salary expense account"
                value={expense}
                onChange={setExpense}
                options={choices('Expense')}
              />
              <PayrollSelect
                label="Net salary payable account"
                value={payable}
                onChange={setPayable}
                options={choices('Liability')}
              />
            </div>
            <p>
              Attendance deductions credit a separate Expense account; loan repayments credit an
              Asset account; tax deductions credit a Liability account. Adjustment mappings use the
              approved Expense, Asset or Liability account. Deduction accounts must differ from
              salary expense and net salary payable.
            </p>
            {deductions.map((line, index) => (
              <div className="hr-form-grid" key={index}>
                <PayrollSelect
                  label="Deduction component name"
                  value={line.component}
                  options={deductionComponents.map((value) => ({
                    value,
                    label: value.replace(/([a-z])([A-Z])/g, '$1 $2'),
                  }))}
                  onChange={(value) =>
                    setDeductions((rows) =>
                      rows.map((row, i) =>
                        i === index ? { ...row, component: value, creditAccountCode: '' } : row,
                      ),
                    )
                  }
                />
                <PayrollSelect
                  label="Deduction account"
                  value={line.creditAccountCode}
                  options={deductionChoices(line.component)}
                  onChange={(value) =>
                    setDeductions((rows) =>
                      rows.map((row, i) =>
                        i === index ? { ...row, creditAccountCode: value } : row,
                      ),
                    )
                  }
                />
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => setDeductions((rows) => rows.filter((_, i) => i !== index))}
                >
                  Remove deduction mapping
                </Button>
              </div>
            ))}
            <Button
              type="button"
              variant="secondary"
              onClick={() =>
                setDeductions((rows) => [...rows, { component: '', creditAccountCode: '' }])
              }
            >
              Add deduction mapping
            </Button>
            {statutory.map((line, index) => (
              <div className="hr-form-grid" key={index}>
                <PayrollSelect
                  label="Scheme"
                  value={line.scheme}
                  options={['EOBI', 'PESSI', 'SESSI'].map((value) => ({ value, label: value }))}
                  onChange={(value) =>
                    setStatutory((rows) =>
                      rows.map((row, i) => (i === index ? { ...row, scheme: value } : row)),
                    )
                  }
                />
                <PayrollSelect
                  label="Employer expense"
                  value={line.employerExpenseAccountCode}
                  options={choices('Expense')}
                  onChange={(value) =>
                    setStatutory((rows) =>
                      rows.map((row, i) =>
                        i === index ? { ...row, employerExpenseAccountCode: value } : row,
                      ),
                    )
                  }
                />
                <PayrollSelect
                  label="Scheme payable"
                  value={line.payableAccountCode}
                  options={choices('Liability').filter(
                    (item) => item.value.toLowerCase() !== payable.toLowerCase(),
                  )}
                  onChange={(value) =>
                    setStatutory((rows) =>
                      rows.map((row, i) =>
                        i === index ? { ...row, payableAccountCode: value } : row,
                      ),
                    )
                  }
                />
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => setStatutory((rows) => rows.filter((_, i) => i !== index))}
                >
                  Remove scheme mapping
                </Button>
              </div>
            ))}
            <Button
              type="button"
              variant="secondary"
              onClick={() =>
                setStatutory((rows) => [
                  ...rows,
                  { scheme: '', employerExpenseAccountCode: '', payableAccountCode: '' },
                ])
              }
            >
              Add statutory mapping
            </Button>
            <Button type="submit" disabled={!reason.trim() || !start || !currency}>
              Submit posting profile
            </Button>
          </form>
        </fieldset>
      )}
      <ActionMessage action={action} />
    </section>
  );
}
