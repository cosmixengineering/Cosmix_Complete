import { z } from 'zod';

// HrReportMetadata / HrReportEnvelope from HrReportContracts.cs. Row shapes differ per
// report type; the server is the source of truth, so rows stay loosely typed records and
// the table renders the columns the server actually returns.
export const reportMetadata = z.object({
  reportType: z.string(),
  reportTitle: z.string(),
  generatedAtUtc: z.string(),
  timeZone: z.string(),
  totalCount: z.number().int(),
  sourceLockStatus: z.string(),
});
export type ReportMetadata = z.infer<typeof reportMetadata>;

export const reportEnvelope = z.object({
  metadata: reportMetadata,
  rows: z.array(z.record(z.string(), z.unknown())),
});
export type ReportEnvelope = z.infer<typeof reportEnvelope>;

export const reportTypeList = z.array(z.string());

// ReportFormat enum: Csv = 1, Xlsx = 2, Pdf = 3. Pdf is not offered because the export
// service currently implements only Csv and Xlsx.
export const exportFormatOptions = [
  { value: '1', label: 'CSV' },
  { value: '2', label: 'XLSX (Excel)' },
];

export interface ReportFilter {
  from?: string;
  to?: string;
  date?: string;
  year?: number;
  month?: number;
  departmentId?: string;
  siteId?: string;
  branchId?: string;
  employeeId?: string;
  vehicleId?: string;
  zoneId?: string;
  leaveTypeId?: string;
  status?: string;
  category?: string;
  documentType?: string;
  page: number;
  pageSize: number;
}

export function reportQuery(filter: ReportFilter) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(filter)) {
    if (value === undefined || value === '' || value === null) continue;
    query.set(key, String(value));
  }
  return '?' + query.toString();
}
