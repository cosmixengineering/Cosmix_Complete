import { request } from '@/services/api/http-client';
import { id } from '../schemas/contracts';
import { payrollSettlementExport } from '../schemas/payroll-settlement';

export async function decodeSettlementExport(value: unknown, signal: AbortSignal) {
  signal.throwIfAborted();
  const file = payrollSettlementExport.parse(value);
  const bytes = Uint8Array.from(atob(file.base64), (character) => character.charCodeAt(0));
  const hash = [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))]
    .map((part) => part.toString(16).padStart(2, '0'))
    .join('');
  signal.throwIfAborted();
  if (hash !== file.sha256.toLowerCase()) throw new Error('Invalid settlement export');
  return { fileName: file.fileName, contentType: file.contentType, bytes };
}

export async function downloadPayrollSettlement(batchId: string, signal: AbortSignal) {
  id.parse(batchId);
  signal.throwIfAborted();
  const file = await decodeSettlementExport(
    await request(`/hr/payroll-settlement/${batchId}/export?format=csv`, { signal }),
    signal,
  );
  signal.throwIfAborted();
  const url = URL.createObjectURL(new Blob([file.bytes], { type: file.contentType }));
  try {
    const link = document.createElement('a');
    link.href = url;
    link.download = file.fileName;
    link.rel = 'noopener';
    link.click();
  } finally {
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
