import { request } from '@/services/api/http-client';
import { id } from '../schemas/contracts';
export async function downloadPayrollBankExport(
  exportId: string,
  signal: AbortSignal,
  expectedSha256?: string,
) {
  id.parse(exportId);
  signal.throwIfAborted();
  const blob = await request(`/payroll-accounting/bank/exports/${exportId}/download`, {
    responseType: 'blob',
    signal,
  });
  signal.throwIfAborted();
  if (!(blob instanceof Blob) || !blob.size || blob.size > 32 * 1024 * 1024)
    throw new Error('Invalid bank export');
  const mime = blob.type.toLowerCase();
  const xlsx = mime === 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
  if (!xlsx && mime !== 'text/csv' && mime !== 'text/csv; charset=utf-8')
    throw new Error('Invalid bank export type');
  const bytes = await blob.arrayBuffer();
  if (xlsx && new Uint8Array(bytes).slice(0, 4).join(',') !== '80,75,3,4')
    throw new Error('Invalid bank workbook');
  if (expectedSha256) {
    const hash = [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))]
      .map((value) => value.toString(16).padStart(2, '0'))
      .join('');
    if (hash !== expectedSha256.toLowerCase()) throw new Error('Bank export checksum mismatch');
  }
  signal.throwIfAborted();
  const url = URL.createObjectURL(blob);
  try {
    const link = document.createElement('a');
    link.href = url;
    link.download = `payroll-bank-${exportId.replace(/-/g, '')}.${xlsx ? 'xlsx' : 'csv'}`;
    link.rel = 'noopener';
    link.click();
  } finally {
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
