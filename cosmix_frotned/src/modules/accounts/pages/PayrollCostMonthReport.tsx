import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { useAuth } from '@/auth/use-auth';
import { request } from '@/services/api/http-client';

const schema = z.object({
  approvedBatchCount: z.number().int().nonnegative(),
  unallocatedBatchIds: z.array(z.string().uuid()),
  rows: z.array(
    z.object({
      projectId: z.string().uuid().nullable(),
      siteId: z.string().uuid().nullable(),
      projectLabel: z.string(),
      siteLabel: z.string(),
      currency: z.string(),
      amount: z.number(),
      employees: z.number().int().nonnegative(),
    }),
  ),
});

export function PayrollCostMonthReport({ year, month }: { year: number; month: number }) {
  const { user, status } = useAuth();
  const allowed =
    status === 'authenticated' &&
    Boolean(
      user?.grants.some(
        (g) =>
          g.permission === 'finance.payroll-costs.read' && g.scopeKind === 7 && g.targetId === null,
      ),
    );
  const report = useQuery({
    queryKey: ['finance', user?.account?.accountId, 'project-cost-month', year, month],
    queryFn: async ({ signal }) =>
      schema.parse(
        await request(`/finance/payroll-costs/report?year=${year}&month=${month}`, { signal }),
      ),
    enabled: allowed && year > 0 && year < 9999 && month >= 1 && month <= 12,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
  });
  if (!allowed) return null;
  return (
    <section aria-label="Approved monthly payroll costs">
      <h3>Approved project and site salary costs</h3>
      <p>
        Includes the latest independently approved allocation for each finalized payroll batch in
        this salary month.
      </p>
      {report.isError ? (
        <p role="alert">
          Cost evidence could not be verified. Refresh after resolving the source records.
        </p>
      ) : report.isFetching ? (
        <p role="status">Loading approved costs…</p>
      ) : report.data ? (
        <>
          <p>
            {report.data.approvedBatchCount} allocated batches.{' '}
            {report.data.unallocatedBatchIds.length} batches awaiting allocation.
          </p>
          {report.data.unallocatedBatchIds.length > 0 && (
            <details>
              <summary>Unallocated payroll batches</summary>
              <p>
                These batches are excluded from cost totals below. Open each batch in Payroll
                accounting to prepare its allocation.
              </p>
              <ul>
                {report.data.unallocatedBatchIds.map((id) => (
                  <li key={id}>{id}</li>
                ))}
              </ul>
            </details>
          )}
          <div className="payroll-table-wrapper">
            <table className="payroll-table">
              <thead>
                <tr>
                  <th>Project</th>
                  <th>Site</th>
                  <th>Employees</th>
                  <th>Currency</th>
                  <th>Expense</th>
                </tr>
              </thead>
              <tbody>
                {report.data.rows.map((row) => (
                  <tr key={`${row.projectId}:${row.siteId}:${row.currency}`}>
                    <td>{row.projectLabel}</td>
                    <td>{row.siteLabel}</td>
                    <td>{row.employees}</td>
                    <td>{row.currency}</td>
                    <td>
                      {row.amount.toLocaleString('en-PK', {
                        minimumFractionDigits: 2,
                        maximumFractionDigits: 2,
                      })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : null}
    </section>
  );
}
