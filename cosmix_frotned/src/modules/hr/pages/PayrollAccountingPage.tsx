import { useState } from 'react';
import { FinanceLedgerPanel, PayrollCostPanel, BankReconciliationPanel } from '@/modules/accounts';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { Denied, ActionMessage } from '../components/Feedback';
import { FinanceQueryState } from '../components/finance/FinanceQueryState';
import {
  FinanceNotice,
  FinanceTabs,
  TextAreaField,
  HasMorePager,
} from '../components/finance/FinanceChrome';
import { PayrollSelect } from '../components/finance/PayrollFields';
import { PayrollAccountingSetup } from '../components/finance/PayrollAccountingSetup';
import { PayrollSettlementReports } from '../components/finance/PayrollSettlementReports';
import { PaymentLifecycleControls } from '../components/finance/PaymentLifecycleControls';
import { PayrollBankExports } from '../components/finance/PayrollBankExports';
import { PayrollSettlementBrowser } from '../components/finance/PayrollSettlementBrowser';
import { money } from '../components/finance/labels';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../hooks/use-finance-queries';
import {
  accountingSetup,
  obligation,
  journal,
  payment,
  entitlement,
} from '../schemas/payroll-accounting';
import { payrollPage, payrollBatchDetail } from '../schemas/finance';
import '@/styles/payroll-handoff.css';

export function PayrollAccountingPage() {
  const access = useFinanceAccess();
  const [tab, setTab] = useState('runs');
  const tabs = [
    ...(access.can('finance.bank-reconciliation.read')
      ? [{ id: 'bank', label: 'Bank reconciliation' }]
      : []),
    ...(access.can('finance.ledger.read') ? [{ id: 'ledger', label: 'Finance ledger' }] : []),
    ...(access.can('payroll.reports.read') ? [{ id: 'reports', label: 'Reports' }] : []),
    ...(access.can('payroll.accounting.accrue')
      ? [{ id: 'runs', label: 'Salary obligations and journals' }]
      : []),
    ...(access.can('payroll.accounting.configure') ||
    access.can('payroll.accounting.configure.approve') ||
    access.can('payroll.accounting.accrue')
      ? [{ id: 'setup', label: 'Accounts, periods and profiles' }]
      : []),
  ];
  if (!tabs.length) return <Denied />;
  const current = tabs.some((item) => item.id === tab) ? tab : tabs[0]!.id;
  return (
    <div className="payroll-handoff-container">
      <header className="payroll-header-banner">
        <h1>Payroll Accounting</h1>
        <p>Review payroll reports and manage the accounting actions available to your account.</p>
        <FinanceTabs tabs={tabs} active={current} onChange={setTab} label="Payroll accounting" />
      </header>
      {current === 'bank' ? (
        <BankReconciliationPanel />
      ) : current === 'ledger' ? (
        <FinanceLedgerPanel />
      ) : current === 'reports' ? (
        <PayrollSettlementBrowser />
      ) : current === 'runs' ? (
        <PayrollObligations />
      ) : (
        <PayrollAccountingSetup />
      )}
    </div>
  );
}

function PayrollObligations() {
  const access = useFinanceAccess();
  const action = useFinanceAction();
  const [page, setPage] = useState(1);
  const [batchPage, setBatchPage] = useState(1);
  const [batchId, setBatchId] = useState('');
  const [selectedId, setSelectedId] = useState('');
  const [reason, setReason] = useState('');
  const [profileId, setProfileId] = useState('');
  const [postingDate, setPostingDate] = useState('');
  const obligations = useFinanceQuery(
    `/payroll-accounting/obligations?page=${page}&pageSize=25`,
    z.array(obligation),
    true,
  );
  const setup = useFinanceQuery('/payroll-accounting/setup', accountingSetup, true);
  const batches = useFinanceQuery(
    `/payroll-handoff/batches?page=${batchPage}&pageSize=25`,
    payrollPage,
    access.can('attendance.payroll-handoff.read'),
  );
  const detail = useFinanceQuery(
    `/payroll-handoff/batches/${batchId}`,
    payrollBatchDetail,
    access.can('attendance.payroll-handoff.read'),
    Boolean(batchId),
  );
  const selected = obligations.data?.find((item) => item.id === selectedId);
  const batch = !detail.isError && !detail.isFetching ? detail.data?.batch : undefined;
  const canIngest =
    batch?.calculationVersion === 'salary-v2' &&
    batch.salaryComplete === true &&
    batch.state === 'finance_handed_off' &&
    Boolean(batch.frozenFingerprint);
  async function ingest() {
    if (!canIngest || !batch) return;
    const result = await action.run(
      '/payroll-accounting/obligations',
      { batchId: batch.id, expectedSourceFingerprint: batch.frozenFingerprint, reason },
      'POST',
      obligation,
    );
    if (result) {
      setSelectedId(result.id);
      void obligations.refetch();
    }
  }
  async function accrue() {
    if (!selected) return;
    const result = await action.run(
      '/payroll-accounting/accrual-journal',
      {
        obligationId: selected.id,
        postingProfileId: profileId,
        postingDate,
        description: `Salary accrual for ${selected.batchId}`,
        reason,
      },
      'POST',
      journal,
    );
    if (result) void obligations.refetch();
  }
  return (
    <>
      <section className="payroll-section-card">
        <h2>Import Finance handoff</h2>
        <FinanceNotice>
          A separate Finance actor must record the handoff after Management locks the salary run.
          Gross earnings, deductions and net salary are derived from that immutable run. Missing
          source, policy or account configuration blocks posting.
        </FinanceNotice>
        <TextAreaField label="Accounting reason" value={reason} onChange={setReason} required />
        {access.can('attendance.payroll-handoff.read') ? (
          <FinanceQueryState query={batches}>
            <PayrollSelect
              label="Salary run handed off to Finance"
              value={batchId}
              onChange={setBatchId}
              options={(batches.data?.items ?? [])
                .filter(
                  (item) =>
                    item.calculationVersion === 'salary-v2' && item.state === 'finance_handed_off',
                )
                .map((item) => ({
                  value: item.id,
                  label: `${item.batchReference} · ${item.periodStart}`,
                }))}
            />
            <HasMorePager
              page={batchPage}
              hasMore={batches.data?.hasMore ?? false}
              busy={batches.isFetching}
              onPrevious={() => setBatchPage(batchPage - 1)}
              onNext={() => setBatchPage(batchPage + 1)}
            />
          </FinanceQueryState>
        ) : (
          <FinanceNotice>
            Payroll handoff read permission is required to select and verify a source run.
          </FinanceNotice>
        )}
        {batchId && (
          <FinanceQueryState query={detail}>
            {batch && (
              <>
                <p>
                  {batch.batchReference} · {batch.state} ·{' '}
                  {money(batch.totals.netAmount, batch.currency)}
                </p>
                <Button
                  disabled={action.pending || !canIngest || !reason.trim()}
                  onClick={() => void ingest()}
                >
                  Create obligation from salary run
                </Button>
              </>
            )}
          </FinanceQueryState>
        )}
      </section>
      <section className="payroll-section-card">
        <h2>Salary obligations</h2>
        <FinanceQueryState query={obligations}>
          {!obligations.data?.length && <FinanceNotice>No obligations on this page.</FinanceNotice>}
          {obligations.data?.map((item) => (
            <article className="payroll-batch-card" key={item.id}>
              <Button variant="secondary" onClick={() => setSelectedId(item.id)}>
                {item.batchId}
              </Button>
              <p>
                {item.state} · Gross {money(item.totalGrossAmount, item.currency)} · Deductions{' '}
                {item.totalDeductions === undefined
                  ? 'Not recorded'
                  : money(item.totalDeductions, item.currency)}{' '}
                · Net {money(item.totalNetAmount, item.currency)}
              </p>
            </article>
          ))}
          <HasMorePager
            page={page}
            hasMore={obligations.data?.length === 25}
            busy={obligations.isFetching}
            onPrevious={() => {
              setPage(page - 1);
              setSelectedId('');
            }}
            onNext={() => {
              setPage(page + 1);
              setSelectedId('');
            }}
          />
        </FinanceQueryState>
        {selected && (
          <>
            <FinanceQueryState query={setup}>
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  void accrue();
                }}
              >
                <h3>Post salary accrual</h3>
                <fieldset disabled={action.pending}>
                  <PayrollSelect
                    label="Approved posting profile"
                    value={profileId}
                    onChange={setProfileId}
                    options={(setup.data?.profiles ?? [])
                      .filter(
                        (item) => item.state === 'Approved' && item.currency === selected.currency,
                      )
                      .map((item) => ({ value: item.id, label: item.name }))}
                  />
                  <TextInput
                    type="date"
                    label="Posting date within an open period"
                    required
                    value={postingDate}
                    onChange={(event) => setPostingDate(event.target.value)}
                  />
                  <Button type="submit" disabled={!reason.trim()}>
                    Post complete salary journal
                  </Button>
                </fieldset>
              </form>
            </FinanceQueryState>
            <ObligationRecords
              key={selected.id}
              obligationId={selected.id}
              currency={selected.currency}
              sourceFingerprint={selected.sourceFingerprint}
            />
            <PayrollSettlementReports
              key={selected.batchId}
              batchId={selected.batchId}
              allowed={access.can('payroll.reports.read')}
              exportAllowed={access.can('payroll.reports.export')}
            />
            <PayrollCostPanel key={`costs:${selected.batchId}`} batchId={selected.batchId} />
          </>
        )}
        <ActionMessage action={action} />
      </section>
    </>
  );
}

function ObligationRecords({
  obligationId,
  currency,
  sourceFingerprint,
}: {
  obligationId: string;
  currency: string;
  sourceFingerprint?: string | null;
}) {
  const access = useFinanceAccess();
  const action = useFinanceAction();
  const journals = useFinanceQuery(
    `/payroll-accounting/obligations/${obligationId}/journals`,
    z.array(journal),
    true,
  );
  const employees = useFinanceQuery(
    `/payroll-accounting/obligations/${obligationId}/employees`,
    z.array(entitlement),
    true,
  );
  const payments = useFinanceQuery(
    `/payroll-accounting/obligations/${obligationId}/payments`,
    z.array(payment),
    access.can('payroll.accounting.payment'),
  );
  const setup = useFinanceQuery('/payroll-accounting/setup', accountingSetup, true);
  const [employeeId, setEmployeeId] = useState('');
  const [amount, setAmount] = useState('');
  const [reference, setReference] = useState('');
  const [payable, setPayable] = useState('');
  const [bank, setBank] = useState('');
  const [date, setDate] = useState('');
  const [reason, setReason] = useState('');
  const accrual = journals.data?.find(
    (item) =>
      item.kind === 'SalaryAccrual' &&
      item.sourceKey === `ACCRUAL-SALARY-${obligationId.replaceAll('-', '').toLowerCase()}`,
  );
  async function recordPayment() {
    if (!accrual) return;
    const result = await action.run(
      '/payroll-accounting/payments',
      {
        obligationId,
        accrualJournalEntryId: accrual.id,
        paymentReference: reference,
        debitAccountCode: payable,
        creditAccountCode: bank,
        amount: Number(amount),
        employeeId,
        postingDate: date,
        reason,
      },
      'POST',
      payment,
    );
    if (result) {
      void payments.refetch();
      const updatedEmployees = await employees.refetch();
      void journals.refetch();
      setReference('');
      if (employeeId && updatedEmployees.data) {
        const match = updatedEmployees.data.find((e) => e.employeeId === employeeId);
        setAmount(match && match.remainingBalance > 0 ? match.remainingBalance.toFixed(2) : '');
      } else {
        setAmount('');
      }
    }
  }
  function onEmployeeSelect(id: string) {
    setEmployeeId(id);
    const match = employees.data?.find((e) => e.employeeId === id);
    if (match) {
      setAmount(match.remainingBalance > 0 ? match.remainingBalance.toFixed(2) : '');
    }
  }
  return (
    <>
      <h3>Posted journal rows</h3>
      <Button variant="secondary" onClick={() => void journals.refetch()}>
        Refresh journals
      </Button>
      <FinanceQueryState query={journals}>
        <div className="payroll-table-wrapper">
          <table className="payroll-table">
            <thead>
              <tr>
                <th>Kind / reference</th>
                <th>Debit</th>
                <th>Credit</th>
                <th>Amount</th>
                <th>Period / source</th>
              </tr>
            </thead>
            <tbody>
              {journals.data?.map((item) => (
                <tr key={item.id}>
                  <td>
                    {item.kind}
                    <br />
                    {item.id}
                  </td>
                  <td>{item.debitAccountCode}</td>
                  <td>{item.creditAccountCode}</td>
                  <td>{money(item.amount, item.currency)}</td>
                  <td>
                    {item.accountingPeriodId}
                    <br />
                    {item.sourceKey}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </FinanceQueryState>
      <FinanceQueryState query={employees}>
        <h3>Employee entitlements</h3>
        {employees.data?.map((item) => (
          <p key={item.employeeId}>
            {item.employeeId}: {money(item.netEntitlement, currency)} entitlement ·{' '}
            {money(item.disbursedAmount, currency)} allocated / reserved ·{' '}
            {money(item.remainingBalance, currency)} available to initiate
          </p>
        ))}
      </FinanceQueryState>
      {access.can('payroll.accounting.payment') && (
        <>
          <TextAreaField
            label="Payment recording reason"
            value={reason}
            onChange={setReason}
            required
          />
          <FinanceQueryState query={payments}>
            <h3>Recorded payment states</h3>
            {!payments.data?.length && (
              <FinanceNotice>No recorded payments for this obligation.</FinanceNotice>
            )}
            {payments.data?.map((item) => (
              <PaymentLifecycleControls
                key={item.id}
                record={item}
                currency={currency}
                onChanged={() => {
                  void payments.refetch();
                  void employees.refetch();
                  void journals.refetch();
                }}
              />
            ))}
          </FinanceQueryState>
          <FinanceQueryState query={setup}>
            <form
              onSubmit={(event) => {
                event.preventDefault();
                void recordPayment();
              }}
            >
              <h3>Record disbursement evidence</h3>
              <fieldset disabled={action.pending || !accrual}>
                <PayrollSelect
                  label="Employee entitlement"
                  value={employeeId}
                  onChange={onEmployeeSelect}
                  options={(employees.data ?? []).map((item) => ({
                    value: item.employeeId,
                    label: `${item.employeeId} · available to initiate ${money(item.remainingBalance, currency)}`,
                  }))}
                />
                <TextInput
                  label="Actual disbursement amount"
                  type="number"
                  min="0.01"
                  step="0.01"
                  required
                  value={amount}
                  onChange={(event) => setAmount(event.target.value)}
                />
                <TextInput
                  label="Payment reference"
                  required
                  value={reference}
                  onChange={(event) => setReference(event.target.value)}
                />
                <PayrollSelect
                  label="Salary payable account"
                  value={payable}
                  onChange={setPayable}
                  options={(setup.data?.accounts ?? [])
                    .filter((item) => item.isActive && item.kind === 'Liability')
                    .map((item) => ({ value: item.code, label: item.name }))}
                />
                <PayrollSelect
                  label="Disbursing account"
                  value={bank}
                  onChange={setBank}
                  options={(setup.data?.accounts ?? [])
                    .filter((item) => item.isActive && item.kind === 'Bank')
                    .map((item) => ({ value: item.code, label: item.name }))}
                />
                <TextInput
                  label="Posting date"
                  type="date"
                  required
                  value={date}
                  onChange={(event) => setDate(event.target.value)}
                />
                <Button type="submit" disabled={!reason.trim()}>
                  Record payment evidence
                </Button>
              </fieldset>
            </form>
          </FinanceQueryState>
        </>
      )}
      {access.can('payroll.accounting.bank.export') && payments.data && !payments.isError && (
        <PayrollBankExports
          obligationId={obligationId}
          currency={currency}
          sourceFingerprint={sourceFingerprint}
          payments={payments.data}
          onChanged={() => {
            void payments.refetch();
          }}
        />
      )}
      <ActionMessage action={action} />
    </>
  );
}
