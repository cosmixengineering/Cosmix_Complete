import { useRef, useState } from 'react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { request } from '@/services/api/http-client';
import { useFinanceCommand } from '../hooks/use-finance-command';
import {
  bankRowSchema,
  bankStatementSchema,
  type BankStatement,
} from '../schemas/bank-reconciliation';

const inspection = z.array(z.object({ name: z.string(), columns: z.array(z.string()) }));
const previewSchema = z.object({ fileSha256: z.string(), rows: z.array(bankRowSchema) });
const root = '/finance/bank-reconciliation';
export function BankStatementImport({
  previous,
  accounts,
  onClose,
  onImported,
}: {
  previous?: BankStatement;
  accounts: { code: string; name: string }[];
  onClose: () => void;
  onImported: (id: string) => void;
}) {
  const [file, setFile] = useState<{ fileName: string; fileBase64: string }>();
  const [sheets, setSheets] = useState<z.infer<typeof inspection>>([]);
  const [mapping, setMapping] = useState({
    sheet: '',
    dateColumn: '',
    referenceColumn: '',
    debitColumn: '',
    creditColumn: '',
    dateFormat: 'yyyy-MM-dd',
  });
  const [accountCode, setAccount] = useState(previous?.accountCode ?? '');
  const [currency, setCurrency] = useState(previous?.currency ?? '');
  const [month, setMonth] = useState(previous?.month.slice(0, 7) ?? '');
  const [opening, setOpening] = useState('');
  const [closing, setClosing] = useState('');
  const [reason, setReason] = useState('');
  const [preview, setPreview] = useState<{
    digest: string;
    value: z.infer<typeof previewSchema>;
  }>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const generation = useRef(0);
  const command = useFinanceCommand();
  const body = {
    accountCode,
    currency,
    month: `${month}-01`,
    openingBalance: Number(opening),
    closingBalance: Number(closing),
    ...file,
    mapping,
    expectedPreviousStatementId: previous?.id ?? null,
    reason,
  };
  const digest = JSON.stringify(body);
  const validated = preview?.digest === digest;
  const frozen = command.pending || command.uncertain || busy;
  const columns = sheets.find((x) => x.name === mapping.sheet)?.columns ?? [];
  async function readFile(selected?: File) {
    const run = ++generation.current;
    setFile(undefined);
    setSheets([]);
    setPreview(undefined);
    setError('');
    if (!selected) return;
    if (selected.size > 4 * 1024 * 1024 || !/\.(csv|xlsx)$/i.test(selected.name)) {
      setError('Choose a CSV or XLSX statement up to 4 MB.');
      return;
    }
    setBusy(true);
    try {
      const bytes = new Uint8Array(await selected.arrayBuffer());
      let binary = '';
      for (let i = 0; i < bytes.length; i += 8192)
        binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
      const upload = { fileName: selected.name, fileBase64: btoa(binary) };
      const found = inspection.parse(
        await request(`${root}/inspect`, { method: 'POST', body: upload }),
      );
      if (run !== generation.current) return;
      setFile(upload);
      setSheets(found);
      setMapping({
        sheet: found[0]?.name ?? '',
        dateColumn: '',
        referenceColumn: '',
        debitColumn: '',
        creditColumn: '',
        dateFormat: 'yyyy-MM-dd',
      });
    } catch {
      if (run === generation.current)
        setError(
          'The statement could not be inspected. Check the file and your access, then retry.',
        );
    } finally {
      if (run === generation.current) setBusy(false);
    }
  }
  return (
    <section aria-label="Import bank statement" className="hr-form-section">
      <h3>
        {previous
          ? 'Replace statement with a retained revision'
          : 'Import complete monthly statement'}
      </h3>
      <p>
        Use separate debit and credit columns, a transaction reference, and the statement
        opening/closing balances. Imports are limited to 1,000 transactions; duplicate rows,
        formulas and unbalanced statements are rejected.
      </p>
      <fieldset disabled={frozen}>
        <label>
          Statement file{' '}
          <input
            type="file"
            accept=".csv,.xlsx"
            onChange={(e) => void readFile(e.target.files?.[0])}
          />
        </label>
        <div className="hr-form-grid">
          <label>
            Bank ledger account
            <select
              value={accountCode}
              disabled={Boolean(previous)}
              onChange={(e) => setAccount(e.target.value)}
            >
              <option value="">Select account</option>
              {accounts.map((x) => (
                <option key={x.code} value={x.code}>
                  {x.code} · {x.name}
                </option>
              ))}
            </select>
          </label>
          <TextInput
            label="Currency"
            value={currency}
            maxLength={3}
            disabled={Boolean(previous)}
            onChange={(e) => setCurrency(e.target.value.toUpperCase())}
          />
          <TextInput
            label="Statement month"
            type="month"
            value={month}
            disabled={Boolean(previous)}
            onChange={(e) => setMonth(e.target.value)}
          />
          <TextInput
            label="Opening balance"
            type="number"
            step="0.01"
            value={opening}
            onChange={(e) => setOpening(e.target.value)}
          />
          <TextInput
            label="Closing balance"
            type="number"
            step="0.01"
            value={closing}
            onChange={(e) => setClosing(e.target.value)}
          />
          <label>
            Worksheet
            <select
              value={mapping.sheet}
              onChange={(e) =>
                setMapping((m) => ({
                  ...m,
                  sheet: e.target.value,
                  dateColumn: '',
                  referenceColumn: '',
                  debitColumn: '',
                  creditColumn: '',
                }))
              }
            >
              {sheets.map((x) => (
                <option key={x.name}>{x.name}</option>
              ))}
            </select>
          </label>
          {(['dateColumn', 'referenceColumn', 'debitColumn', 'creditColumn'] as const).map(
            (key, index) => (
              <label key={key}>
                {
                  ['Transaction date', 'Transaction reference', 'Debit amount', 'Credit amount'][
                    index
                  ]
                }
                <select
                  value={mapping[key]}
                  onChange={(e) => setMapping((m) => ({ ...m, [key]: e.target.value }))}
                >
                  <option value="">Select column</option>
                  {columns.map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              </label>
            ),
          )}
          <label>
            Date format
            <select
              value={mapping.dateFormat}
              onChange={(e) => setMapping((m) => ({ ...m, dateFormat: e.target.value }))}
            >
              {['yyyy-MM-dd', 'dd/MM/yyyy', 'MM/dd/yyyy', 'ExcelSerial'].map((x) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </label>
        </div>
        <TextInput
          label="Import / replacement reason"
          value={reason}
          maxLength={1000}
          onChange={(e) => setReason(e.target.value)}
        />
      </fieldset>
      <Button
        disabled={
          frozen ||
          !file ||
          !accountCode ||
          !month ||
          !currency ||
          !opening ||
          !closing ||
          !reason.trim()
        }
        onClick={async () => {
          setBusy(true);
          setError('');
          try {
            const value = previewSchema.parse(
              await request(`${root}/preview`, { method: 'POST', body }),
            );
            setPreview({ digest, value });
          } catch {
            setError(
              'Preview validation failed. Check dates, unique references, debit/credit amounts and opening plus movements equals closing.',
            );
            setPreview(undefined);
          } finally {
            setBusy(false);
          }
        }}
      >
        Validate preview
      </Button>
      {validated && preview && (
        <>
          <p>
            Validated {preview.value.rows.length} transactions. Review the first 20 before
            importing.
          </p>
          <div className="payroll-table-wrapper">
            <table className="payroll-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Reference</th>
                  <th>Bank movement</th>
                </tr>
              </thead>
              <tbody>
                {preview.value.rows.slice(0, 20).map((x) => (
                  <tr key={x.row}>
                    <td>{x.date}</td>
                    <td>{x.reference}</td>
                    <td>{x.amount.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
      <Button
        disabled={!validated || command.pending || busy}
        onClick={async () => {
          const result = await command.run(root, body, bankStatementSchema);
          if (result) onImported(result.id);
        }}
      >
        {command.uncertain ? 'Retry import' : 'Import validated statement'}
      </Button>
      <Button variant="secondary" disabled={command.pending || command.uncertain} onClick={onClose}>
        Close import
      </Button>
      {(error || command.error) && <p role="alert">{error || command.error}</p>}
    </section>
  );
}
