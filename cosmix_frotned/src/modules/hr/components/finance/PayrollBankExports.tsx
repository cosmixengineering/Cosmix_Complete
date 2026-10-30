import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useRef, useState } from 'react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceQuery } from '../../hooks/use-finance-queries';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { bankExport, bankFormat } from '../../schemas/payroll-bank';
import type { PayrollPayment } from '../../schemas/payroll-accounting';
import { date } from '../../schemas/contracts';
import { downloadPayrollBankExport } from '../../api/payroll-bank-api';
import { bankExportReason } from '../../utils/payroll-bank-form';
import { financeError } from '../../api/finance-messages';
import { labelDate } from '../../utils/messages';
import { FinanceNotice, TextAreaField } from './FinanceChrome';
import { FinanceQueryState } from './FinanceQueryState';
import { PayrollSelect } from './PayrollFields';
import { ReviewedFinanceCommand } from './ReviewedFinanceCommand';
import { PayrollBankFormats } from './PayrollBankFormats';
import { money } from './labels';

export function PayrollBankExports({
  obligationId,
  currency,
  sourceFingerprint,
  payments,
  onChanged,
}: {
  obligationId: string;
  currency: string;
  sourceFingerprint: string | null | undefined;
  payments: PayrollPayment[];
  onChanged(): void;
}) {
  const access = useFinanceAccess();
  const allowed = access.can('payroll.accounting.bank.export');
  const formats = useFinanceQuery('/payroll-accounting/bank/formats', z.array(bankFormat), allowed);
  const exports = useFinanceQuery(
    `/payroll-accounting/bank/exports?obligationId=${obligationId}`,
    z.array(bankExport),
    allowed,
  );
  const lifetime = useHrLifetime();
  const guard = useRef(false);
  const [downloading, setDownloading] = useState('');
  const [error, setError] = useState('');
  const [formatId, setFormatId] = useState('');
  const [valueDate, setValueDate] = useState('');
  const [reason, setReason] = useState('');
  const [selected, setSelected] = useState<string[]>([]);
  const [created, setCreated] = useState<z.infer<typeof bankExport> | null>(null);
  const eligible = payments.filter(
    (item) =>
      item.state === 'Posted' && item.employeeId && !item.bankExportId && !item.reversalRequestedAt,
  );
  const knownExports = [
    ...new Map(
      [...(exports.data ?? []), ...(created ? [created] : [])].map((item) => [item.id, item]),
    ).values(),
  ];
  async function download(exportId: string) {
    if (!allowed || guard.current) return;
    const signal = lifetime();
    if (signal.aborted) return;
    guard.current = true;
    setDownloading(exportId);
    setError('');
    try {
      await downloadPayrollBankExport(
        exportId,
        signal,
        knownExports.find((item) => item.id === exportId)?.sha256,
      );
    } catch (failure) {
      if (!signal.aborted) setError(financeError(failure));
    } finally {
      guard.current = false;
      if (!signal.aborted) setDownloading('');
    }
  }
  if (!allowed) return null;
  return (
    <section className="payroll-section-card" aria-label="Bank payroll export">
      <h3>Bank payroll file</h3>
      <FinanceNotice>
        Generate a file from posted, employee-bound payments using an independently approved bank
        format. Downloading a file does not submit a transfer or confirm payment. The server
        rechecks current verified banking details before releasing it.
      </FinanceNotice>
      <FinanceQueryState query={formats}>
        <ReviewedFinanceCommand
          label="Generate bank file"
          schema={bankExport}
          disabled={!sourceFingerprint || formats.isFetching || !eligible.length}
          prepare={() => {
            const profile = formats.data?.find(
              (item) =>
                item.id === formatId && item.state === 'Approved' && item.currency === currency,
            );
            if (
              !profile ||
              !sourceFingerprint ||
              !selected.length ||
              selected.length > 500 ||
              new Set(selected).size !== selected.length ||
              selected.some((paymentId) => !eligible.some((item) => item.id === paymentId))
            )
              throw Error();
            return {
              path: '/payroll-accounting/bank/exports',
              body: {
                obligationId,
                formatProfileId: profile.id,
                valueDate: date.parse(valueDate),
                paymentIds: [...selected],
                expectedSourceFingerprint: sourceFingerprint,
                reason: bankExportReason(reason),
              },
              summary: `Generate ${profile.rules.outputFormat} using ${profile.name} v${profile.formatVersion} for ${selected.length} selected payments, value date ${valueDate}, currency ${currency}. This creates a protected file only.`,
            };
          }}
          onSaved={(result) => {
            setCreated(result);
            setSelected([]);
            setReason('');
            onChanged();
            void exports.refetch();
          }}
        >
          {() => (
            <>
              <PayrollSelect
                label="Approved format for this currency"
                value={formatId}
                onChange={setFormatId}
                options={(formats.data ?? [])
                  .filter((item) => item.state === 'Approved' && item.currency === currency)
                  .map((item) => ({
                    value: item.id,
                    label: `${item.name} · v${item.formatVersion} · ${item.rules.outputFormat}`,
                  }))}
              />
              <TextInput
                type="date"
                label="Bank value date"
                value={valueDate}
                onChange={(event) => setValueDate(event.target.value)}
                required
              />
              <fieldset>
                <legend>Select existing posted payments · maximum 500</legend>
                <div className="payroll-table-wrapper">
                  <table className="payroll-table">
                    <thead>
                      <tr>
                        <th scope="col">Select</th>
                        <th scope="col">Payment reference</th>
                        <th scope="col">Employee</th>
                        <th scope="col">Amount</th>
                      </tr>
                    </thead>
                    <tbody>
                      {eligible.map((item) => (
                        <tr key={item.id}>
                          <td>
                            <input
                              type="checkbox"
                              aria-label={`Select payment ${item.paymentReference}`}
                              checked={selected.includes(item.id)}
                              onChange={(event) =>
                                setSelected((current) =>
                                  event.target.checked
                                    ? [...current, item.id]
                                    : current.filter((paymentId) => paymentId !== item.id),
                                )
                              }
                            />
                          </td>
                          <td>{item.paymentReference}</td>
                          <td>
                            <EmployeeReference employeeId={item.employeeId} />
                          </td>
                          <td>{money(item.amount, currency)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </fieldset>
              <TextAreaField
                label="Export reason · maximum 500 characters"
                value={reason}
                onChange={setReason}
                required
              />
            </>
          )}
        </ReviewedFinanceCommand>
        {!eligible.length && (
          <FinanceNotice>
            No eligible unexported posted payments. Existing exports are listed below.
          </FinanceNotice>
        )}
        {!sourceFingerprint && (
          <FinanceNotice>
            A frozen source fingerprint is required before generating a bank file.
          </FinanceNotice>
        )}
        <Button
          variant="secondary"
          disabled={formats.isFetching}
          onClick={() => void formats.refetch()}
        >
          Refresh bank formats
        </Button>
        <PayrollBankFormats
          currency={currency}
          formats={formats.data ?? []}
          onChanged={() => void formats.refetch()}
        />
      </FinanceQueryState>
      {created && (
        <p role="status">
          File retained for {created.paymentCount} payments · {money(created.totalAmount, currency)}{' '}
          · Download expires {labelDate(created.expiresAt)} (Asia/Karachi).
        </p>
      )}
      <FinanceQueryState query={exports} title="Retained bank files unavailable">
        {knownExports.length > 0 && (
          <>
            <h4>Retained bank files</h4>
            {knownExports.map((item) => (
              <div key={item.id}>
                <p>
                  Export {item.id} · {item.paymentCount} payments ·{' '}
                  {money(item.totalAmount, currency)} · Expires {labelDate(item.expiresAt)}{' '}
                  (Asia/Karachi)
                </p>
                <Button
                  variant="secondary"
                  disabled={Boolean(downloading)}
                  onClick={() => void download(item.id)}
                >
                  {downloading === item.id
                    ? 'Checking protected file…'
                    : 'Download protected bank file'}
                </Button>
                <RenewBankExport
                  record={item}
                  onSaved={(result) => {
                    setCreated(result);
                    void exports.refetch();
                  }}
                />
              </div>
            ))}
            <p>
              Expired files or files whose payment/banking sources changed cannot be downloaded.
            </p>
          </>
        )}
        {!knownExports.length && (
          <FinanceNotice>No retained bank files for this obligation.</FinanceNotice>
        )}
        <Button
          variant="secondary"
          disabled={exports.isFetching}
          onClick={() => void exports.refetch()}
        >
          Refresh retained files
        </Button>
      </FinanceQueryState>
      {error && <p role="alert">{error}</p>}
    </section>
  );
}

function RenewBankExport({
  record,
  onSaved,
}: {
  record: z.infer<typeof bankExport>;
  onSaved(result: z.infer<typeof bankExport>): void;
}) {
  const [reason, setReason] = useState('');
  return (
    <details>
      <summary>Renew download availability</summary>
      <FinanceNotice>
        Renewal rechecks the original file and its unchanged sources. It does not create another
        transfer or payment.
      </FinanceNotice>
      <ReviewedFinanceCommand
        label="Renew bank file download"
        schema={bankExport}
        prepare={() => ({
          path: `/payroll-accounting/bank/exports/${record.id}/renew`,
          body: { expectedSha256: record.sha256, reason: bankExportReason(reason) },
          summary: `Renew download availability for export ${record.id}, containing ${record.paymentCount} payments. The original file and checksum must remain unchanged.`,
        })}
        onSaved={(result) => {
          setReason('');
          onSaved(result);
        }}
      >
        {() => (
          <TextAreaField
            label="Renewal reason · maximum 500 characters"
            value={reason}
            onChange={setReason}
            required
          />
        )}
      </ReviewedFinanceCommand>
    </details>
  );
}
