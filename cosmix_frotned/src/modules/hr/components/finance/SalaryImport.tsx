import { formatEmployeeCode } from '@/utils/employee-code';
import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useState } from 'react';
import type { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { useFinanceAction, useFinanceQuery } from '../../hooks/use-finance-queries';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import {
  importApplied,
  importHistory,
  importInspection,
  importPreview,
} from '../../schemas/payroll-salary';
import { financeGet } from '../../api/hr-finance-api';
import { financeError } from '../../api/finance-messages';
import { ActionMessage } from '../Feedback';
import { FinanceNotice, HasMorePager, TextAreaField } from './FinanceChrome';
import { FinanceQueryState } from './FinanceQueryState';
import { PayrollSelect } from './PayrollFields';

export function SalaryImport() {
  const access = useFinanceAccess();
  const canSubmit = access.can(
    'attendance.payroll-imports.submit',
    'attendance.payroll-salary.request',
    'hr.employees.compensation.request',
  );
  const action = useFinanceAction();
  const lifetime = useHrLifetime();
  const [source, setSource] = useState<z.infer<typeof importInspection> | null>(null);
  const [preview, setPreview] = useState<z.infer<typeof importPreview> | null>(null);
  const [applied, setApplied] = useState<z.infer<typeof importApplied> | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [sheet, setSheet] = useState('');
  const [mapping, setMapping] = useState<Record<string, string>>({});
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [reading, setReading] = useState(false);
  const [page, setPage] = useState(1);
  const history = useFinanceQuery(
    `/hr/payroll-imports?page=${page}&pageSize=25`,
    importHistory,
    true,
  );
  async function reopen(sourceId: string, submitted: boolean) {
    if (reading || action.pending) return;
    const signal = lifetime();
    setReading(true);
    setError('');
    try {
      const inspection = await financeGet(
        `/hr/payroll-imports/${sourceId}`,
        importInspection,
        signal,
      );
      const submission = submitted
        ? await financeGet(`/hr/payroll-imports/${sourceId}/submission`, importApplied, signal)
        : null;
      signal.throwIfAborted();
      setSource(inspection);
      setSheet(inspection.sheets[0]?.name ?? '');
      setMapping({});
      setPreview(null);
      setApplied(submission);
      setFile(null);
    } catch (failure) {
      if (!signal.aborted) setError(financeError(failure));
    } finally {
      if (!signal.aborted) setReading(false);
    }
  }
  async function inspect() {
    if (!canSubmit || !file || reading) return;
    setError('');
    if (
      !/\.(csv|xlsx)$/i.test(file.name) ||
      file.size === 0 ||
      file.size > 4 * 1024 * 1024 ||
      file.name.length > 150
    ) {
      setError(
        'Choose a nonempty CSV or XLSX file of at most 4 MiB with a filename of at most 150 characters.',
      );
      return;
    }
    const signal = lifetime();
    setReading(true);
    try {
      const bytes = new Uint8Array(await file.arrayBuffer());
      signal.throwIfAborted();
      let binary = '';
      for (let i = 0; i < bytes.length; i += 8192)
        binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
      const result = await action.run(
        '/hr/payroll-imports/inspect',
        {
          fileName: file.name,
          contentType: file.name.toLowerCase().endsWith('.xlsx')
            ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
            : 'text/csv',
          base64: btoa(binary),
          reason,
        },
        'POST',
        importInspection,
        undefined,
        'Source uploaded. Map its columns and validate the rows before submission.',
      );
      if (result && !signal.aborted) {
        setSource(result);
        setSheet(result.sheets[0]?.name ?? '');
        setMapping({});
        setPreview(null);
        setApplied(null);
        void history.refetch();
      }
    } catch {
      if (!signal.aborted) setError('The selected file could not be read. Choose the file again.');
    } finally {
      if (!signal.aborted) setReading(false);
    }
  }
  async function validate() {
    if (
      !source ||
      source.supportedTargets.some((target) => target.required && !mapping[target.key])
    )
      return;
    setPreview(null);
    const result = await action.run(
      '/hr/payroll-imports/preview',
      { sourceId: source.sourceId, sheetName: sheet, mapping, reason },
      'POST',
      importPreview,
      undefined,
      'Validation completed. Review the row results below; no salary request was submitted.',
    );
    if (result) setPreview(result);
  }
  async function apply() {
    if (
      !canSubmit ||
      !source ||
      !preview?.canApply ||
      preview.sourceId !== source.sourceId ||
      applied
    )
      return;
    const result = await action.run(
      '/hr/payroll-imports/apply',
      {
        sourceId: source.sourceId,
        sheetName: sheet,
        mapping,
        expectedPreviewFingerprint: preview.previewFingerprint,
        reason,
      },
      'POST',
      importApplied,
      undefined,
      'Validated rows were submitted for independent review.',
    );
    if (result) {
      setApplied(result);
      void history.refetch();
    }
  }
  return (
    <section className="payroll-section-card">
      <h2>Import salary sheet</h2>
      <FinanceNotice>
        Upload CSV or XLSX, map columns and review every reported error before submitting.
        Compensation rows support Monthly, Daily and Hourly rates. Accepted rows create independent
        salary or component review requests; they do not approve a salary.
      </FinanceNotice>
      {!canSubmit && (
        <FinanceNotice>
          Your read permissions allow retained imports, validation previews and submission receipts.
          Uploading and submitting also require import submission, salary request and compensation
          request permissions.
        </FinanceNotice>
      )}
      <FinanceQueryState query={history}>
        <details>
          <summary>Retained imports and submission receipts</summary>
          <p>Resume a retained source after refresh, or inspect its submitted request receipt.</p>
          {history.data?.items.map((item) => (
            <p key={item.sourceId}>
              {item.fileName} · {item.state}{' '}
              <Button
                variant="secondary"
                disabled={reading || action.pending}
                onClick={() => void reopen(item.sourceId, item.state === 'Submitted')}
              >
                {item.state === 'Submitted' ? 'View submission receipt' : 'Resume mapping'}
              </Button>
            </p>
          ))}
          <HasMorePager
            page={page}
            hasMore={history.data?.hasMore ?? false}
            busy={history.isFetching || reading || action.pending}
            onPrevious={() => setPage(page - 1)}
            onNext={() => setPage(page + 1)}
          />
        </details>
      </FinanceQueryState>
      <fieldset disabled={reading || action.pending || Boolean(applied)}>
        <label className="field">
          Salary sheet
          <input
            type="file"
            disabled={!canSubmit}
            accept=".csv,.xlsx"
            onChange={(event) => {
              setFile(event.target.files?.[0] ?? null);
              setSource(null);
              setPreview(null);
            }}
          />
        </label>
        <TextAreaField
          label="Import reason"
          required
          value={reason}
          onChange={(value) => {
            setReason(value);
            setPreview(null);
          }}
        />
        <Button disabled={!canSubmit || !file || !reason.trim()} onClick={() => void inspect()}>
          Inspect uploaded file
        </Button>
        {source && (
          <>
            <p>
              Source: <code>{source.sourceId}</code> · SHA256 <code>{source.sourceSha256}</code>
            </p>
            <PayrollSelect
              label="Worksheet"
              value={sheet}
              options={source.sheets.map((item) => ({
                value: item.name,
                label: `${item.name} (${item.rowCount} rows)`,
              }))}
              onChange={(value) => {
                setSheet(value);
                setMapping({});
                setPreview(null);
              }}
            />
            <div className="hr-form-grid">
              {source.supportedTargets.map((target) => (
                <PayrollSelect
                  key={target.key}
                  label={target.label}
                  required={target.required}
                  value={mapping[target.key] ?? ''}
                  options={(source.sheets.find((item) => item.name === sheet)?.columns ?? []).map(
                    (column) => ({ value: column, label: column }),
                  )}
                  onChange={(value) => {
                    setMapping((previous) => {
                      const next = { ...previous };
                      if (value) next[target.key] = value;
                      else delete next[target.key];
                      return next;
                    });
                    setPreview(null);
                  }}
                />
              ))}
            </div>
            <Button
              onClick={() => void validate()}
              disabled={
                !sheet ||
                !reason.trim() ||
                source.supportedTargets.some((target) => target.required && !mapping[target.key])
              }
            >
              Validate mapped rows
            </Button>
          </>
        )}
        {preview && (
          <>
            <h3>Validation preview</h3>
            <p>
              {preview.summary.validRowCount} valid of {preview.summary.rowCount} rows ·{' '}
              {preview.summary.errorCount} issues
            </p>
            <div className="payroll-table-wrapper">
              <table className="payroll-table">
                <thead>
                  <tr>
                    <th>Row</th>
                    <th>Employee</th>
                    <th>Mapped values</th>
                    <th>Validation</th>
                  </tr>
                </thead>
                <tbody>
                  {preview.rows.map((row) => (
                    <tr key={row.rowNumber}>
                      <td>{row.rowNumber}</td>
                      <td>
                        <EmployeeReference
                          employeeId={row.employeeId}
                          employeeNumber={row.employeeLabel}
                        />
                      </td>
                      <td>
                        {Object.entries(row.values).map(([key, value]) => (
                          <div key={key}>
                            {key}: {key === 'employeeNumber' ? formatEmployeeCode(value) : value}
                          </div>
                        ))}
                      </td>
                      <td>
                        {row.issues.length
                          ? row.issues.map((issue, index) => (
                              <div key={index}>
                                {issue.column ? `${issue.column}: ` : ''}
                                {issue.code.replaceAll('_', ' ')}
                              </div>
                            ))
                          : 'Valid'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Button
              disabled={!canSubmit || !preview.canApply || !reason.trim()}
              onClick={() => void apply()}
            >
              Submit validated rows for review
            </Button>
          </>
        )}
      </fieldset>
      {applied && (
        <FinanceNotice>
          {applied.requests.length} review requests submitted. Source {applied.sourceId}. Review
          compensation requests in Employee salary and component requests in Salary sources.
        </FinanceNotice>
      )}
      {error && <p role="alert">{error}</p>}
      <ActionMessage action={action} />
    </section>
  );
}
