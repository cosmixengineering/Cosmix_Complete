import { useState } from 'react';
import type { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { useFinanceAction } from '../../hooks/use-finance-queries';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { importApplied, importInspection, importPreview } from '../../schemas/payroll-salary';
import { ActionMessage } from '../Feedback';
import { FinanceNotice, TextAreaField } from './FinanceChrome';
import { PayrollSelect } from './PayrollFields';

export function SalaryImport() {
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
  async function inspect() {
    if (!file || reading) return;
    setError('');
    if (!/\.(csv|xlsx)$/i.test(file.name) || file.size > 5 * 1024 * 1024) { setError('Choose a CSV or XLSX file of at most 5 MiB.'); return; }
    const signal = lifetime();
    setReading(true);
    try {
      const bytes = new Uint8Array(await file.arrayBuffer());
      signal.throwIfAborted();
      let binary = '';
      for (let i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
      const result = await action.run('/hr/payroll-imports/inspect', { fileName: file.name, contentType: file.name.toLowerCase().endsWith('.xlsx') ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : 'text/csv', base64: btoa(binary), reason }, 'POST', importInspection);
      if (result && !signal.aborted) { setSource(result); setSheet(result.sheets[0]?.name ?? ''); setMapping({}); setPreview(null); setApplied(null); }
    } catch { if (!signal.aborted) setError('The selected file could not be read. Choose the file again.'); }
    finally { if (!signal.aborted) setReading(false); }
  }
  async function validate() {
    if (!source) return;
    setPreview(null);
    const result = await action.run('/hr/payroll-imports/preview', { sourceId: source.sourceId, sheetName: sheet, mapping, reason }, 'POST', importPreview);
    if (result) setPreview(result);
  }
  async function apply() {
    if (!source || !preview?.canApply || applied) return;
    const result = await action.run('/hr/payroll-imports/apply', { sourceId: source.sourceId, sheetName: sheet, mapping, expectedPreviewFingerprint: preview.previewFingerprint, reason }, 'POST', importApplied);
    if (result) setApplied(result);
  }
  return <section className="payroll-section-card"><h2>Import salary sheet</h2><FinanceNotice>Upload CSV or XLSX, map columns and review every reported error before submitting. Compensation rows support Monthly, Daily and Hourly rates. Accepted rows create independent salary or component review requests; they do not approve a salary.</FinanceNotice>
    <fieldset disabled={reading || action.pending || Boolean(applied)}><label className="field">Salary sheet<input type="file" accept=".csv,.xlsx" onChange={event => { setFile(event.target.files?.[0] ?? null); setSource(null); setPreview(null); }} /></label><TextAreaField label="Import reason" required value={reason} onChange={setReason} /><Button disabled={!file || !reason.trim()} onClick={() => void inspect()}>Inspect uploaded file</Button>
    {source && <><p>Source: <code>{source.sourceId}</code> · SHA256 <code>{source.sourceSha256}</code></p><PayrollSelect label="Worksheet" value={sheet} options={source.sheets.map(item => ({ value: item.name, label: `${item.name} (${item.rowCount} rows)` }))} onChange={value => { setSheet(value); setMapping({}); setPreview(null); }} /><div className="hr-form-grid">{source.supportedTargets.map(target => <PayrollSelect key={target.key} label={target.label} required={target.required} value={mapping[target.key] ?? ''} options={(source.sheets.find(item => item.name === sheet)?.columns ?? []).map(column => ({ value: column, label: column }))} onChange={value => { setMapping(previous => { const next = { ...previous }; if (value) next[target.key] = value; else delete next[target.key]; return next; }); setPreview(null); }} />)}</div><Button onClick={() => void validate()} disabled={!sheet || !reason.trim()}>Validate mapped rows</Button></>}
    {preview && <><h3>Validation preview</h3><p>{preview.summary.validRowCount} valid of {preview.summary.rowCount} rows · {preview.summary.errorCount} issues</p><div className="payroll-table-wrapper"><table className="payroll-table"><thead><tr><th>Row</th><th>Employee</th><th>Mapped values</th><th>Validation</th></tr></thead><tbody>{preview.rows.map(row => <tr key={row.rowNumber}><td>{row.rowNumber}</td><td>{row.employeeLabel ?? row.employeeId ?? 'Unresolved'}</td><td>{Object.entries(row.values).map(([key, value]) => <div key={key}>{key}: {value}</div>)}</td><td>{row.issues.length ? row.issues.map((issue, index) => <div key={index}>{issue.column ? `${issue.column}: ` : ''}{issue.code.replaceAll('_', ' ')}</div>) : 'Valid'}</td></tr>)}</tbody></table></div><Button disabled={!preview.canApply || !reason.trim()} onClick={() => void apply()}>Submit validated rows for review</Button></>}
    </fieldset>{applied && <FinanceNotice>{applied.requests.length} review requests submitted. Source {applied.sourceId}. Review compensation requests in Employee salary and component requests in Salary sources.</FinanceNotice>}{error && <p role="alert">{error}</p>}<ActionMessage action={action} />
  </section>;
}
