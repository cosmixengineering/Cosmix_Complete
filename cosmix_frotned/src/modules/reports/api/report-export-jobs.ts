import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { request } from '@/services/api/http-client';
import {
  reportEnvelope,
  reportMetadata,
  reportQuery,
  type ReportFilter,
} from '../schemas/report-contracts';

const id = z.uuid().refine((value) => value !== '00000000-0000-0000-0000-000000000000');
const timestamp = z.iso.datetime({ offset: true });
const formatSchema = z.union([z.literal(1), z.literal(2), z.literal(3)]);
const optionalText = z
  .string()
  .nullish()
  .transform((value) => value ?? undefined);
const optionalNumber = z
  .number()
  .int()
  .nullish()
  .transform((value) => value ?? undefined);
const normalizedFilter = z.object({
  from: optionalText,
  to: optionalText,
  date: optionalText,
  year: optionalNumber,
  month: optionalNumber,
  departmentId: optionalText,
  siteId: optionalText,
  branchId: optionalText,
  employeeId: optionalText,
  vehicleId: optionalText,
  zoneId: optionalText,
  leaveTypeId: optionalText,
  status: optionalText,
  category: optionalText,
  documentType: optionalText,
  page: z.number().int().positive(),
  pageSize: z.number().int().min(1).max(1000),
});
const exportablePreview = reportEnvelope
  .extend({
    metadata: reportMetadata.extend({
      filter: normalizedFilter,
      returnedCount: z.number().int().nonnegative(),
      totalCount: z.number().int().nonnegative(),
    }),
  })
  .refine(
    (value) =>
      value.metadata.returnedCount === value.rows.length &&
      value.rows.length <= value.metadata.totalCount,
    'Invalid report counts.',
  );

export const reportExportJobSchema = z
  .object({
    id,
    reportType: z.string().min(1),
    state: z.enum(['pending', 'running', 'completed', 'failed']),
    format: formatSchema,
    requestedAt: timestamp,
    generatedAt: timestamp.nullable(),
    downloadExpiresAt: timestamp.nullable(),
    failureCode: z.string().nullable(),
    totalCount: z.number().int().nonnegative().nullable(),
    returnedCount: z.number().int().nonnegative().nullable(),
  })
  .refine(
    (job) =>
      job.state !== 'completed' ||
      (job.generatedAt !== null &&
        job.downloadExpiresAt !== null &&
        Date.parse(job.downloadExpiresAt) > Date.parse(job.generatedAt) &&
        job.totalCount !== null &&
        job.totalCount === job.returnedCount &&
        job.failureCode === null),
    'The completed export could not be validated.',
  );

export type ReportExportJob = z.infer<typeof reportExportJobSchema>;
export type ReportExportSelection = { reportType: string; title: string; filter: ReportFilter };
export type ReportExportIntent = {
  key: string;
  selection: ReportExportSelection;
  body: { format: ReportExportJob['format']; reason: string; filter: ReportFilter };
};

export function isValidReportExportReason(reason: string) {
  return (
    reason.trim().length > 0 &&
    reason.trim().length <= 1000 &&
    !Array.from(reason).some((character) => {
      const code = character.charCodeAt(0);
      return code < 32 || (code >= 127 && code <= 159);
    })
  );
}

export async function fetchExportableReport(
  reportType: string,
  filter: ReportFilter,
  signal: AbortSignal,
) {
  return exportablePreview
    .refine((value) => value.metadata.reportType === reportType)
    .parse(
      await request(`/hr/reports/${encodeURIComponent(reportType)}` + reportQuery(filter), {
        signal,
      }),
    );
}

export function createReportExportIntent(
  selection: ReportExportSelection,
  format: number,
  reason: string,
): ReportExportIntent {
  const body = Object.freeze({
    format: formatSchema.parse(format),
    reason: z.string().refine(isValidReportExportReason).trim().min(1).max(1000).parse(reason),
    filter: Object.freeze({ ...selection.filter, page: 1, pageSize: 1000 }),
  });
  return Object.freeze({
    key: crypto.randomUUID(),
    selection: Object.freeze({ ...selection, filter: body.filter }),
    body,
  });
}

export async function requestReportExport(intent: ReportExportIntent, signal: AbortSignal) {
  return reportExportJobSchema
    .refine(
      (job) => job.reportType === intent.selection.reportType && job.format === intent.body.format,
    )
    .parse(
      await request(`/hr/reports/${encodeURIComponent(intent.selection.reportType)}/export`, {
        method: 'POST',
        body: intent.body,
        idempotencyKey: intent.key,
        signal,
      }),
    );
}

export async function fetchReportExportJob(expected: ReportExportJob, signal: AbortSignal) {
  return reportExportJobSchema
    .refine(
      (job) =>
        job.id === expected.id &&
        job.reportType === expected.reportType &&
        job.format === expected.format,
    )
    .parse(await request(`/hr/reports/exports/${encodeURIComponent(expected.id)}`, { signal }));
}

export function canDownloadReportExport(job: ReportExportJob, now = Date.now()) {
  return (
    job.state === 'completed' &&
    job.downloadExpiresAt !== null &&
    Date.parse(job.downloadExpiresAt) > now
  );
}

async function validateExportBlob(
  body: unknown,
  format: ReportExportJob['format'],
  maximumBytes: number,
): Promise<Blob> {
  const contentType = {
    1: 'text/csv',
    2: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    3: 'application/pdf',
  }[format];
  if (
    !(body instanceof Blob) ||
    !body.size ||
    body.size > maximumBytes ||
    body.type.split(';')[0]?.toLowerCase() !== contentType
  )
    throw new Error('Invalid report attachment.');
  const header = new Uint8Array(await body.slice(0, 5).arrayBuffer());
  if (
    (format === 2 &&
      (header[0] !== 80 || header[1] !== 75 || header[2] !== 3 || header[3] !== 4)) ||
    (format === 3 && String.fromCharCode(...header) !== '%PDF-')
  )
    throw new Error('Invalid report signature.');
  return body;
}

export async function exportReportPage(
  reportType: string,
  format: number,
  reason: string,
  filter: ReportFilter,
  signal: AbortSignal,
) {
  const pageFormat = z.union([z.literal(1), z.literal(2)]).parse(format);
  const body = await request(`/hr/reports/${encodeURIComponent(reportType)}/export-page`, {
    method: 'POST',
    body: { format: pageFormat, reason, filter },
    responseType: 'blob',
    signal,
  });
  const blob = await validateExportBlob(body, pageFormat, Number.POSITIVE_INFINITY);
  signal.throwIfAborted();
  return blob;
}

export function saveReportBlob(blob: Blob, filename: string, signal: AbortSignal) {
  signal.throwIfAborted();
  const url = URL.createObjectURL(blob);
  const revoke = () => URL.revokeObjectURL(url);
  signal.addEventListener('abort', revoke, { once: true });
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.rel = 'noopener';
  try {
    link.click();
  } catch (failure) {
    revoke();
    throw failure;
  } finally {
    setTimeout(() => {
      revoke();
      signal.removeEventListener('abort', revoke);
    }, 1000);
  }
}

export async function downloadReportExport(job: ReportExportJob, signal: AbortSignal) {
  reportExportJobSchema.parse(job);
  if (!canDownloadReportExport(job))
    throw new ApiError(410, undefined, 'hr_report_download_expired');
  const body = await request(`/hr/reports/exports/${encodeURIComponent(job.id)}/download`, {
    responseType: 'blob',
    signal,
  });
  const blob = await validateExportBlob(body, job.format, 8_000_000);
  signal.throwIfAborted();
  if (!canDownloadReportExport(job))
    throw new ApiError(410, undefined, 'hr_report_download_expired');
  saveReportBlob(
    blob,
    `hr-report-${job.id}.${{ 1: 'csv', 2: 'xlsx', 3: 'pdf' }[job.format]}`,
    signal,
  );
}

export function reportExportFailure(code: string | null) {
  const messages: Record<string, string> = {
    hr_report_export_worker_not_enabled:
      'Export processing is unavailable. Contact your administrator or retry later.',
    hr_report_scope_revoked: 'The export is no longer within your current permissions.',
    hr_report_export_limit:
      'The complete result exceeds the export limit. Narrow the filters and request a new export.',
    hr_report_output_limit:
      'The complete file exceeds the export limit. Narrow the filters and request a new export.',
    hr_report_not_ready: 'The export is not ready. Refresh its status.',
    hr_report_download_expired: 'This download has expired. Request a new export.',
    hr_report_evidence_conflict:
      'The report source could not be verified. Refresh the preview before requesting a new export.',
    idempotency_key_reused:
      'The request key conflicts with an earlier export. Check the export before requesting another.',
    idempotency_key_expired:
      'The request key has expired. Check the earlier export before requesting another.',
  };
  return messages[code ?? ''] ?? 'The export failed. Refresh its status or request a new export.';
}

export function reportExportError(failure: unknown) {
  if (failure instanceof z.ZodError)
    return 'The server response could not be validated. Retry to check the export.';
  if (failure instanceof ApiError) {
    const message =
      failure.status === 410
        ? reportExportFailure('hr_report_download_expired')
        : failure.code?.startsWith('hr_report_') || failure.code?.startsWith('idempotency_')
          ? reportExportFailure(failure.code)
          : failure.status === 503
            ? 'Export service is temporarily unavailable. Retry later.'
            : failure.message;
    return message + (failure.correlationId ? ` Reference: ${failure.correlationId}` : '');
  }
  return 'The export result could not be confirmed. Check your connection and retry.';
}
