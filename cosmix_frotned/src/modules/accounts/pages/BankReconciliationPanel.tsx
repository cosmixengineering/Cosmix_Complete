import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { useAuth } from '@/auth/use-auth';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { request } from '@/services/api/http-client';
import { useFinanceCommand } from '../hooks/use-finance-command';
import { bankStatementSchema, bankWorkspaceSchema } from '../schemas/bank-reconciliation';
import type { BankStatement } from '../schemas/bank-reconciliation';
import { BankStatementImport } from './BankStatementImport';
import { BankStatementReview } from './BankStatementReview';

const root = '/finance/bank-reconciliation';
export function BankReconciliationPanel() {
  const { user, status } = useAuth();
  const can = (permission: string) =>
    status === 'authenticated' &&
    Boolean(
      user?.grants.some(
        (g) => g.permission === permission && g.scopeKind === 7 && g.targetId === null,
      ),
    );
  const readable = can('finance.bank-reconciliation.read');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState('');
  const [importing, setImporting] = useState(false);
  const [replace, setReplace] = useState<BankStatement | undefined>();
  const subject = user?.account?.accountId ?? 'none';
  const list = useQuery({
    queryKey: ['finance', subject, 'bank-statements', page],
    queryFn: async ({ signal }) =>
      z.array(bankStatementSchema).parse(await request(`${root}?page=${page}`, { signal })),
    enabled: readable,
    gcTime: 0,
    retry: false,
  });
  const workspace = useQuery({
    queryKey: ['finance', subject, 'bank-statement', selected],
    queryFn: async ({ signal }) =>
      bankWorkspaceSchema.parse(await request(`${root}/${selected}`, { signal })),
    enabled: readable && Boolean(selected),
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
  });
  const accounts = useQuery({
    queryKey: ['finance', subject, 'bank-accounts'],
    queryFn: async ({ signal }) =>
      z
        .array(z.object({ code: z.string(), name: z.string() }))
        .parse(await request(`${root}/accounts`, { signal })),
    enabled: readable,
    gcTime: 0,
    retry: false,
  });
  const command = useFinanceCommand();
  const [reason, setReason] = useState('');
  const data = workspace.data;
  if (!readable) return null;
  return (
    <section aria-label="Bank statement reconciliation">
      <h2>Bank statement reconciliation</h2>
      <p>
        Compare the complete monthly bank statement with posted payroll payments and reversals.
        Non-payroll rows must be identified for separate Finance review. This does not confirm or
        authorize salary payments.
      </p>
      {can('finance.bank-reconciliation.manage') && (
        <Button
          onClick={() => {
            setReplace(undefined);
            setImporting(true);
          }}
        >
          Import monthly statement
        </Button>
      )}
      {importing && (
        <BankStatementImport
          key={`${subject}:${replace?.id ?? 'new'}`}
          previous={replace}
          accounts={accounts.data ?? []}
          onClose={() => setImporting(false)}
          onImported={(id) => {
            setImporting(false);
            setSelected(id);
            void list.refetch();
          }}
        />
      )}
      {list.isError ? (
        <p role="alert">Statements could not be loaded.</p>
      ) : list.isFetching ? (
        <p role="status">Loading statements…</p>
      ) : (
        <>
          <div className="payroll-table-wrapper">
            <table className="payroll-table">
              <thead>
                <tr>
                  <th>Bank ledger account</th>
                  <th>Month</th>
                  <th>Currency</th>
                  <th>Revision</th>
                  <th>Status</th>
                  <th>Review</th>
                </tr>
              </thead>
              <tbody>
                {list.data?.slice(0, 50).map((row) => (
                  <tr key={row.id}>
                    <td>
                      {accounts.data?.find((a) => a.code === row.accountCode)?.name ??
                        row.accountCode}
                    </td>
                    <td>{row.month.slice(0, 7)}</td>
                    <td>{row.currency}</td>
                    <td>{row.revision}</td>
                    <td>{row.closed ? 'Closed' : 'Open'}</td>
                    <td>
                      <Button onClick={() => setSelected(row.id)}>Open</Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Button disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
            Previous
          </Button>
          <Button disabled={(list.data?.length ?? 0) <= 50} onClick={() => setPage((p) => p + 1)}>
            Next
          </Button>
        </>
      )}
      {selected &&
        (workspace.isError ? (
          <p role="alert">
            Statement evidence could not be verified. Refresh after resolving the source records.
          </p>
        ) : workspace.isFetching ? (
          <p role="status">Loading protected statement…</p>
        ) : (
          data && (
            <>
              <h3>
                {data.statement.accountCode} · {data.statement.month.slice(0, 7)} ·{' '}
                {data.statement.currency}
              </h3>
              <p>
                Opening: {data.openingBalance.toFixed(2)} · Closing:{' '}
                {data.closingBalance.toFixed(2)}
              </p>
              <p>
                {data.matching.unmatchedRows.length} unmatched bank rows ·{' '}
                {data.matching.unmatchedLedgerIds.length} unmatched payroll ledger entries.
              </p>
              {data.statement.closed ? (
                <p>
                  Month closed by {data.closedBy} on {data.closedAt}. Evidence is retained;
                  backdated bank postings are blocked.
                </p>
              ) : (
                <>
                  {can('finance.bank-reconciliation.manage') && (
                    <Button
                      variant="secondary"
                      onClick={() => {
                        setReplace(data.statement);
                        setImporting(true);
                      }}
                    >
                      Replace incorrect statement
                    </Button>
                  )}
                  <BankStatementReview
                    key={`${subject}:${data.statement.id}:${data.proposal?.id ?? 'new'}:${data.ledgerFingerprint}`}
                    data={data}
                    manageable={can('finance.bank-reconciliation.manage')}
                    onSaved={() => void workspace.refetch()}
                  />
                  {can('finance.bank-reconciliation.close') && (
                    <div>
                      <TextInput
                        label="Independent closing review reason"
                        value={reason}
                        onChange={(e) => setReason(e.target.value)}
                        maxLength={1000}
                        disabled={command.pending || command.uncertain}
                      />
                      <p>
                        Close only after reviewing every match and non-payroll classification. The
                        importer and proposal maker cannot close their own work. The statement month
                        must have ended.
                      </p>
                      <Button
                        disabled={
                          !data.matching.canClose ||
                          !data.proposal ||
                          data.proposal.ledgerFingerprint !== data.ledgerFingerprint ||
                          !reason.trim() ||
                          command.pending
                        }
                        onClick={async () => {
                          if (!data.proposal) return;
                          const result = await command.run(
                            `${root}/${data.statement.id}/close`,
                            {
                              expectedProposalId: data.proposal.id,
                              expectedLedgerFingerprint: data.ledgerFingerprint,
                              reason,
                            },
                            bankStatementSchema,
                          );
                          if (result) {
                            void workspace.refetch();
                            void list.refetch();
                          }
                        }}
                      >
                        {command.uncertain ? 'Retry close request' : 'Approve and close month'}
                      </Button>
                      {command.error && <p role="alert">{command.error}</p>}
                    </div>
                  )}
                </>
              )}
              <details>
                <summary>Payroll bank ledger entries ({data.ledger.length})</summary>
                <div className="payroll-table-wrapper">
                  <table className="payroll-table">
                    <thead>
                      <tr>
                        <th>Date</th>
                        <th>Reference</th>
                        <th>Amount</th>
                        <th>Matched</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.ledger.map((x) => (
                        <tr key={x.journalId}>
                          <td>{x.date}</td>
                          <td>{x.reference}</td>
                          <td>{x.amount.toFixed(2)}</td>
                          <td>
                            {data.matching.unmatchedLedgerIds.includes(x.journalId) ? 'No' : 'Yes'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </details>
              {data.statement.closed && (
                <details>
                  <summary>Retained bank statement rows</summary>
                  <ul>
                    {data.rows.map((x) => (
                      <li key={x.row}>
                        {x.date} · {x.reference} · {x.amount.toFixed(2)}
                      </li>
                    ))}
                  </ul>
                </details>
              )}
            </>
          )
        ))}
    </section>
  );
}
