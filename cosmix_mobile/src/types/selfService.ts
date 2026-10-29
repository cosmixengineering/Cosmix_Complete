export type ServiceKey =
  | 'leaves'
  | 'loans'
  | 'resignation'
  | 'finances'
  | 'payroll'
  | 'payments'
  | 'profile'
  | 'allowances'
  | 'vehicles'
  | 'corrections'
  | 'calendar'
  | 'punches'
  | 'notifications'
  | 'requests'
  | 'shift'
  | 'encashment'
  | 'overtime';
export type RequestKind =
  | 'leave'
  | 'advance'
  | 'loan'
  | 'resignation'
  | 'allowance'
  | 'fuel'
  | 'trip'
  | 'maintenance'
  | 'incident'
  | 'correction'
  | 'encashment'
  | 'overtime';
export type DataRecord = Record<string, unknown>;
export type ServiceData = { root: DataRecord; rows: DataRecord[] };
export type Choice = { value: string; label: string };
export type RequestDraft = {
  version: 1;
  accountId: string;
  sessionId: string;
  service: ServiceKey;
  route: string;
  key: string;
  fileName: string;
  label: string;
  createdAtUtc: string;
};
export type StoredRequest = RequestDraft & { body: unknown };
export type SelectedEvidence = {
  id: string;
  path: string;
  name: string;
  size: number;
  contentType: string;
  kind: number;
};
