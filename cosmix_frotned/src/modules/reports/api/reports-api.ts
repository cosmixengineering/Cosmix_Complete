import { z } from 'zod';
import { request } from '@/services/api/http-client';
import { queryString } from '../../hr/api/hr-api';
import {
  reportEnvelope,
  reportTypeList,
  reportQuery,
  type ReportFilter,
} from '../schemas/report-contracts';

export { queryString, reportQuery };
// Preserve the current-page helper name through the validated /export-page transport.
export { exportReportPage as exportReport } from './report-export-jobs';

export async function fetchReportTypes(signal?: AbortSignal): Promise<string[]> {
  return reportTypeList.parse(await request('/hr/reports/types', { signal }));
}

export async function fetchReport(reportType: string, filter: ReportFilter, signal?: AbortSignal) {
  return reportEnvelope.parse(
    await request(`/hr/reports/${encodeURIComponent(reportType)}` + reportQuery(filter), {
      signal,
    }),
  );
}

export const reportExportRequestSchema = z.object({
  format: z.number().int().min(1).max(2),
  reason: z.string().min(1).max(1000),
});
