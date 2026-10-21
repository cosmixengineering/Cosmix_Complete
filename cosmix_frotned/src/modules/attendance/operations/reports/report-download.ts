import { request } from '@/services/api/http-client';
import type { ExportJob } from './schemas';
import { validateReportBlob } from './report-blob';

export async function downloadReport(job: ExportJob, signal: AbortSignal) {
  if (
    job.state !== 'completed' ||
    !job.downloadExpiresAt ||
    Date.parse(job.downloadExpiresAt) <= Date.now()
  )
    throw new Error('The completed report download has expired or is unavailable.');
  const blob = await request(`/attendance/reports/exports/${job.id}/download`, {
    responseType: 'blob',
    signal,
  });
  if (!(blob instanceof Blob)) throw new Error('The report attachment is invalid.');
  await validateReportBlob(blob, job.format);
  signal.throwIfAborted();
  const url = URL.createObjectURL(blob);
  const revoke = () => URL.revokeObjectURL(url);
  signal.addEventListener('abort', revoke, { once: true });
  const link = document.createElement('a');
  link.href = url;
  link.rel = 'noopener';
  link.download = `attendance-factual-${job.id.replaceAll('-', '')}.${{ 1: 'csv', 2: 'xlsx', 3: 'pdf' }[job.format]}`;
  try {
    link.click();
  } catch (error) {
    revoke();
    throw error;
  } finally {
    setTimeout(() => {
      revoke();
      signal.removeEventListener('abort', revoke);
    }, 1000);
  }
}
