import { useState } from 'react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { request } from '@/services/api/http-client';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { financeError } from '../../api/finance-messages';
import { money } from './labels';
const exportFile = z.object({ fileName: z.string().min(1).max(180), contentType: z.string(), base64: z.string().max(1400000), sha256: z.string().regex(/^[a-f0-9]{64}$/i) });
export function SalaryPayslips({ batchId, currency, employees, available }: { batchId: string; currency: string; employees: { employeeId: string; grossEarnings: number; totalDeductions: number; netPay: number }[]; available: boolean }) {
  const lifetime = useHrLifetime();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function download(employeeId: string, format: 'pdf' | 'xlsx') {
    if (busy || !available) return;
    const signal = lifetime();
    setBusy(true); setError('');
    try {
      const file = exportFile.parse(await request(`/payroll-handoff/batches/${batchId}/payslips/${employeeId}?format=${format}`, { signal }));
      const mime = format === 'pdf' ? 'application/pdf' : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
      if (file.contentType !== mime || /[\\/\x00-\x1f]/.test(file.fileName) || !file.fileName.toLowerCase().endsWith('.' + format) || !file.base64.startsWith(format === 'pdf' ? 'JVBERi0' : 'UEsDB')) throw new Error('Invalid export');
      const bytes = Uint8Array.from(atob(file.base64), value => value.charCodeAt(0));
      const hash = [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))].map(value => value.toString(16).padStart(2, '0')).join('');
      if (hash !== file.sha256.toLowerCase()) throw new Error('Invalid export');
      signal.throwIfAborted();
      const url = URL.createObjectURL(new Blob([bytes], { type: mime }));
      const link = document.createElement('a'); link.href = url; link.download = file.fileName; link.rel = 'noopener'; link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (failure) { if (!signal.aborted) setError(financeError(failure)); }
    finally { if (!signal.aborted) setBusy(false); }
  }
  return <section><h3>Employee salary breakdowns</h3><p>These totals belong to this run version. A locked salary does not confirm payment.</p><div className="payroll-table-wrapper"><table className="payroll-table"><thead><tr><th>Employee</th><th>Gross earnings</th><th>Deductions</th><th>Net salary</th><th>Payslip</th></tr></thead><tbody>{employees.map(employee => <tr key={employee.employeeId}><td>{employee.employeeId}</td><td>{money(employee.grossEarnings, currency)}</td><td>{money(employee.totalDeductions, currency)}</td><td>{money(employee.netPay, currency)}</td><td><Button variant="secondary" disabled={!available || busy} onClick={() => void download(employee.employeeId, 'pdf')}>PDF</Button><Button variant="secondary" disabled={!available || busy} onClick={() => void download(employee.employeeId, 'xlsx')}>Excel</Button></td></tr>)}</tbody></table></div>{!available && <p>Payslips become available after independent approval and Management lock.</p>}{error && <p role="alert">{error}</p>}</section>;
}
