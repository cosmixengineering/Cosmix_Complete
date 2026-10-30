import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { formatEmployeeCode } from '@/utils/employee-code';
import { useRef, useState } from 'react';
import { Button } from '@/components/ui/Button';
import { useFinanceQuery } from '../../hooks/use-finance-queries';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { downloadPayrollSettlement } from '../../api/payroll-settlement-api';
import { financeError } from '../../api/finance-messages';
import { payrollSettlementReport } from '../../schemas/payroll-settlement';
import { labelDate } from '../../utils/messages';
import { FinanceQueryState } from './FinanceQueryState';
import { FinanceNotice } from './FinanceChrome';
import { money } from './labels';

export function PayrollSettlementReports({
  batchId,
  allowed,
  exportAllowed,
}: {
  batchId: string;
  allowed: boolean;
  exportAllowed: boolean;
}) {
  const report = useFinanceQuery(
    `/hr/payroll-settlement/${batchId}`,
    payrollSettlementReport,
    allowed,
    Boolean(batchId),
  );
  const lifetime = useHrLifetime();
  const guard = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const data = report.isError ? undefined : report.data;
  async function download() {
    if (!allowed || !exportAllowed || !data || report.isFetching || guard.current) return;
    const signal = lifetime();
    if (signal.aborted) return;
    guard.current = true;
    setBusy(true);
    setError('');
    try {
      await downloadPayrollSettlement(batchId, signal);
    } catch (failure) {
      if (!signal.aborted) setError(financeError(failure));
    } finally {
      guard.current = false;
      if (!signal.aborted) setBusy(false);
    }
  }
  if (!allowed)
    return (
      <FinanceNotice>
        Payroll settlement report access is required to view this report.
      </FinanceNotice>
    );
  return (
    <section className="payroll-section-card" aria-label="Payroll settlement reports">
      <div className="payroll-header-top">
        <div>
          <h2>Payroll register & payment reconciliation</h2>
          <p>Frozen salary amounts and current payment outcomes for this run.</p>
        </div>
        <div className="payroll-controls-bar">
          <Button
            variant="secondary"
            disabled={busy || report.isFetching}
            onClick={() => void report.refetch()}
          >
            Refresh report
          </Button>
          <Button
            disabled={!exportAllowed || busy || report.isFetching || !data}
            onClick={() => void download()}
          >
            {busy ? 'Preparing CSV…' : 'Download report CSV'}
          </Button>
        </div>
      </div>
      {error && <p role="alert">{error}</p>}
      {!exportAllowed && (
        <FinanceNotice>Report export permission is required to download CSV.</FinanceNotice>
      )}
      {report.isFetching && !report.isPending && <p role="status">Refreshing payment balances…</p>}
      <FinanceQueryState query={report} title="Settlement report unavailable">
        {data && (
          <>
            <h3>{data.batchReference}</h3>
            <p>
              {data.periodStart} to{' '}
              {new Date(Date.parse(data.periodEndExclusive + 'T00:00:00Z') - 86400000)
                .toISOString()
                .slice(0, 10)}{' '}
              · {data.currency} · Generated {labelDate(data.generatedAtUtc)} (Asia/Karachi)
            </p>
            <div className="payroll-kpi-grid">
              {[
                ['Gross earnings', data.grossEarnings],
                ['Deductions', data.deductions],
                ['Net entitlement', data.netEntitlement],
                ['Confirmed paid', data.confirmedPayments],
                ['Posted & pending', data.reservedPayments],
                ['Awaiting confirmed payment', data.outstandingPayments],
                ['Net payment ledger', data.paymentLedgerNet],
                ['Ledger difference', data.paymentLedgerDifference],
              ].map(([label, value]) => (
                <div className="payroll-kpi-card" key={String(label)}>
                  <div className="payroll-kpi-info">
                    <span className="payroll-kpi-label">{label}</span>
                    <strong className="payroll-kpi-value">
                      {money(Number(value), data.currency)}
                    </strong>
                  </div>
                </div>
              ))}
            </div>
            <FinanceNotice>
              Posted and pending payments reserve entitlement. Only confirmed payments count as
              paid. Failed and reversed payments remain in the history.
            </FinanceNotice>
            {data.issues.length > 0 || data.paymentLedgerDifference !== 0 ? (
              <div role="alert">
                <h3>Reconciliation needs review</h3>
                <p>Resolve the reported differences before sign-off.</p>
                <ul>
                  {data.issues.map((issue, index) => (
                    <li key={index}>{issue}</li>
                  ))}
                </ul>
              </div>
            ) : (
              <FinanceNotice>
                No reconciliation differences reported for this snapshot.
              </FinanceNotice>
            )}
            <h3>Employee payroll register · {data.employees.length} employees</h3>
            {!data.employees.length ? (
              <FinanceNotice>No employee records in this report.</FinanceNotice>
            ) : (
              <div className="payroll-table-wrapper">
                <table className="payroll-table">
                  <caption>
                    Frozen earnings, deductions, salary entitlement and current settlement
                  </caption>
                  <thead>
                    <tr>
                      <th scope="col">Employee</th>
                      <th scope="col">Gross</th>
                      <th scope="col">Deductions</th>
                      <th scope="col">Net entitlement</th>
                      <th scope="col">Confirmed paid</th>
                      <th scope="col">Awaiting confirmation</th>
                      <th scope="col">State</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.employees.map((employee) => (
                      <tr key={employee.employeeId}>
                        <th scope="row">
                          {formatEmployeeCode(employee.employeeNumber)} · {employee.employeeName}
                        </th>
                        <td>{money(employee.grossEarnings, data.currency)}</td>
                        <td>{money(employee.deductions, data.currency)}</td>
                        <td>{money(employee.netEntitlement, data.currency)}</td>
                        <td>{money(employee.payments.confirmed, data.currency)}</td>
                        <td>{money(employee.payments.outstanding, data.currency)}</td>
                        <td>{settlementLabel(employee.payments.state)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <details>
              <summary>Payment outcomes by employee</summary>
              <div className="payroll-table-wrapper">
                <table className="payroll-table">
                  <caption>
                    Separate active reservations and historical unsuccessful payments
                  </caption>
                  <thead>
                    <tr>
                      <th scope="col">Employee</th>
                      <th scope="col">Posted</th>
                      <th scope="col">Pending</th>
                      <th scope="col">Failed</th>
                      <th scope="col">Reversed</th>
                      <th scope="col">Available to initiate</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.employees.map((employee) => (
                      <tr key={employee.employeeId}>
                        <th scope="row">
                          {formatEmployeeCode(employee.employeeNumber)} · {employee.employeeName}
                        </th>
                        {[
                          employee.payments.posted,
                          employee.payments.pending,
                          employee.payments.failed,
                          employee.payments.reversed,
                          employee.payments.remainingToInitiate,
                        ].map((value, index) => (
                          <td key={index}>{money(value, data.currency)}</td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </details>
            <details>
              <summary>Frozen payroll component breakdown · {data.components.length} rows</summary>
              <p>Signed component amounts are returned by the server for this frozen run.</p>
              {!data.components.length ? (
                <FinanceNotice>No component rows in this report.</FinanceNotice>
              ) : (
                <div className="payroll-table-wrapper">
                  <table className="payroll-table">
                    <caption>
                      Employee earnings, deductions and included contribution components
                    </caption>
                    <thead>
                      <tr>
                        <th scope="col">Employee</th>
                        <th scope="col">Component</th>
                        <th scope="col">Amount</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.components.map((component, index) => {
                        const employee = data.employees.find(
                          (item) => item.employeeId === component.employeeId,
                        );
                        return (
                          <tr key={index}>
                            <td>
                              <EmployeeReference
                                employeeId={component.employeeId}
                                employeeNumber={employee?.employeeNumber}
                                employeeName={employee?.employeeName}
                                showName
                              />
                            </td>
                            <td>{component.component}</td>
                            <td>{money(component.amount, data.currency)}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </details>
            <details>
              <summary>Source verification</summary>
              <p>
                Frozen source fingerprint: <code>{data.sourceFingerprint}</code>
              </p>
              <p>
                The CSV is generated from a fresh server snapshot and can reflect payment changes
                since this report was loaded.
              </p>
            </details>
          </>
        )}
      </FinanceQueryState>
    </section>
  );
}

function settlementLabel(state: string) {
  return (
    (
      {
        NoPaymentDue: 'No payment due',
        Unpaid: 'Unpaid',
        Pending: 'Pending',
        PartiallyPaid: 'Partially paid',
        Paid: 'Paid · confirmed',
        Failed: 'Failed',
        Reversed: 'Reversed',
      } as Record<string, string>
    )[state] ?? state
  );
}
