import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/auth/use-auth';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { request } from '@/services/api/http-client';
import { ledgerPageSchema, trialBalanceSchema } from '../schemas/ledger';
import { PayrollCostMonthReport } from './PayrollCostMonthReport';

const amount = (value: number) =>
  value.toLocaleString('en-PK', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function FinanceLedgerPanel() {
  const { user, status } = useAuth();
  const now = new Date();
  const [month, setMonth] = useState(
    `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`,
  );
  const [page, setPage] = useState(1);
  const [accountCode, setAccountCode] = useState('');
  const [currency, setCurrency] = useState('');
  const allowed =
    status === 'authenticated' &&
    Boolean(
      user?.grants.some(
        (grant) =>
          grant.permission === 'finance.ledger.read' &&
          grant.scopeKind === 7 &&
          grant.targetId === null,
      ),
    );
  const validMonth =
    /^\d{4}-(0[1-9]|1[0-2])$/.test(month) &&
    Number(month.slice(0, 4)) > 0 &&
    Number(month.slice(0, 4)) < 9999;
  const validCurrency = currency === '' || /^[A-Z]{3}$/.test(currency);
  const year = Number(month.slice(0, 4)),
    selectedMonth = Number(month.slice(5));
  const until = `${selectedMonth === 12 ? year + 1 : year}-${String(selectedMonth === 12 ? 1 : selectedMonth + 1).padStart(2, '0')}-01`;
  const period = new URLSearchParams({ from: `${month}-01`, untilExclusive: until });
  const ledgerParams = new URLSearchParams(period);
  ledgerParams.set('page', String(page));
  ledgerParams.set('pageSize', '50');
  if (accountCode) ledgerParams.set('accountCode', accountCode);
  if (currency) ledgerParams.set('currency', currency);
  const subject = user?.account?.accountId ?? 'none';
  const balance = useQuery({
    queryKey: ['finance', subject, 'trial-balance', period.toString()],
    queryFn: async ({ signal }) =>
      trialBalanceSchema.parse(await request(`/finance/trial-balance?${period}`, { signal })),
    enabled: allowed && validMonth,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
  });
  const ledger = useQuery({
    queryKey: ['finance', subject, 'ledger', ledgerParams.toString()],
    queryFn: async ({ signal }) =>
      ledgerPageSchema.parse(await request(`/finance/ledger?${ledgerParams}`, { signal })),
    enabled: allowed && validMonth && validCurrency,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
  });
  if (!allowed) return <p>You do not have permission to read the Finance ledger.</p>;
  return (
    <section aria-label="Finance ledger and trial balance">
      <h2>Finance ledger and trial balance</h2>
      <p>
        Connected source: payroll, including statutory contributions, salary payments and reversals.
        Other Finance modules are not connected yet.
      </p>
      <TextInput
        label="Accounting month"
        type="month"
        value={month}
        onChange={(event) => {
          setMonth(event.target.value);
          setPage(1);
          setAccountCode('');
          setCurrency('');
        }}
      />
      {!validMonth ? <p role="alert">Select a valid accounting month.</p> : null}
      {validMonth && <PayrollCostMonthReport year={year} month={selectedMonth} />}
      <div className="hr-form-grid">
        <TextInput
          label="Account code (optional)"
          value={accountCode}
          maxLength={50}
          onChange={(event) => {
            setAccountCode(event.target.value);
            setPage(1);
          }}
        />
        <TextInput
          label="Currency (optional)"
          value={currency}
          maxLength={3}
          onChange={(event) => {
            setCurrency(event.target.value.toUpperCase());
            setPage(1);
          }}
        />
      </div>
      {!validCurrency && <p role="alert">Use a three-letter currency code, such as PKR.</p>}
      {balance.isError || ledger.isError ? (
        <div role="alert">
          <p>
            The ledger could not be loaded. No estimated balances are shown. Finance should check
            posting dates and ledger readiness if retrying does not resolve this.
          </p>
          <Button
            onClick={() => {
              void balance.refetch();
              void ledger.refetch();
            }}
          >
            Retry
          </Button>
        </div>
      ) : balance.isFetching || ledger.isFetching ? (
        <p role="status">Loading committed ledger records…</p>
      ) : (
        <>
          {balance.data && (
            <div className="payroll-table-wrapper">
              <table className="payroll-table">
                <caption>
                  Trial balance — debit balances are positive; credit balances are negative
                </caption>
                <thead>
                  <tr>
                    <th>Account</th>
                    <th>Currency</th>
                    <th>Opening</th>
                    <th>Debits</th>
                    <th>Credits</th>
                    <th>Closing</th>
                  </tr>
                </thead>
                <tbody>
                  {balance.data.accounts.map((row) => (
                    <tr key={`${row.currency}:${row.accountCode}`}>
                      <td>{row.accountCode}</td>
                      <td>{row.currency}</td>
                      <td>{amount(row.openingBalance)}</td>
                      <td>{amount(row.debit)}</td>
                      <td>{amount(row.credit)}</td>
                      <td>{amount(row.closingBalance)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {!balance.data.accounts.length && (
                <p>No committed postings in or before this month.</p>
              )}
            </div>
          )}
          <h3>Journal entries</h3>
          {ledger.data && (
            <>
              <div className="payroll-table-wrapper">
                <table className="payroll-table">
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Account</th>
                      <th>Currency</th>
                      <th>Debit</th>
                      <th>Credit</th>
                      <th>Source reference</th>
                    </tr>
                  </thead>
                  <tbody>
                    {ledger.data.items.map((row) => (
                      <tr key={`${row.journalId}:${row.accountCode}`}>
                        <td>{row.postingDate}</td>
                        <td>{row.accountCode}</td>
                        <td>{row.currency}</td>
                        <td>{amount(row.debit)}</td>
                        <td>{amount(row.credit)}</td>
                        <td>{row.sourceKey}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {!ledger.data.items.length && <p>No postings match this period and filter.</p>}
              </div>
              <Button variant="secondary" disabled={page === 1} onClick={() => setPage(page - 1)}>
                Previous
              </Button>{' '}
              <span>Page {page}</span>{' '}
              <Button
                variant="secondary"
                disabled={!ledger.data.hasMore}
                onClick={() => setPage(page + 1)}
              >
                Next
              </Button>
            </>
          )}
        </>
      )}
    </section>
  );
}
