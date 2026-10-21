import type { ExportJob } from './schemas';

export async function validateReportBlob(blob: Blob, format: ExportJob['format']) {
  const expected = {
    1: 'text/csv',
    2: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    3: 'application/pdf',
  }[format];
  if (
    blob.size === 0 ||
    blob.size > 2_000_000 ||
    (blob.type.split(';')[0] ?? '').toLowerCase() !== expected
  )
    throw new Error('The report attachment is invalid.');
  const header = new Uint8Array(await blob.slice(0, 5).arrayBuffer());
  if (
    (format === 2 &&
      (header[0] !== 80 || header[1] !== 75 || header[2] !== 3 || header[3] !== 4)) ||
    (format === 3 && String.fromCharCode(...header) !== '%PDF-')
  )
    throw new Error('The report attachment signature is invalid.');
}
