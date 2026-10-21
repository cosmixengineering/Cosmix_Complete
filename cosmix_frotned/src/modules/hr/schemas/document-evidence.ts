import { z } from 'zod';
import { date, id, time } from './contracts';

const documentType = z.number().int().min(1).max(10);
const distinct = <T>(items: T[]) => new Set(items).size === items.length;
const completeness = z.object({
  employeeId: id,
  asOf: date,
  reminderThrough: date,
  requiredDocuments: z.array(
    z.object({ type: documentType, isMissing: z.boolean(), availableDocumentIds: z.array(id) }),
  ),
  unavailableSelectedDocumentIds: z.array(id),
  expiringDocuments: z.array(z.object({ id, type: documentType, expiresOn: date })),
  certifications: z.array(
    z.object({
      reference: z.string(),
      type: z.string().nullable(),
      expiresOn: date.nullable(),
      documentId: id.nullable(),
      isMissingEvidence: z.boolean(),
      isExpired: z.boolean(),
      isExpiring: z.boolean(),
      isComplete: z.boolean(),
      status: z.number().int().min(1).max(3).nullable(),
    }),
  ),
  activationBlockers: z.array(z.string()),
});
export function documentCompletenessFor(employeeId: string, days: number) {
  return completeness.refine(
    (value) =>
      value.employeeId === employeeId &&
      days >= 1 &&
      days <= 365 &&
      Date.parse(value.reminderThrough) - Date.parse(value.asOf) === days * 86400000 &&
      distinct(value.requiredDocuments.map((row) => row.type)) &&
      value.requiredDocuments.every(
        (row) =>
          row.isMissing === (row.availableDocumentIds.length === 0) &&
          distinct(row.availableDocumentIds),
      ) &&
      distinct(value.unavailableSelectedDocumentIds) &&
      distinct(value.expiringDocuments.map((row) => row.id)) &&
      value.expiringDocuments.every(
        (row) => row.expiresOn >= value.asOf && row.expiresOn <= value.reminderThrough,
      ) &&
      value.certifications.every(
        (row) =>
          row.isExpired === (row.expiresOn !== null && row.expiresOn < value.asOf) &&
          row.isExpiring ===
            (row.expiresOn !== null &&
              row.expiresOn >= value.asOf &&
              row.expiresOn <= value.reminderThrough),
      ),
    'Document completeness does not match the selected employee or expiry window.',
  );
}
const canonicalIds = (values: string[]) => values.map((value) => value.toLowerCase()).sort();
export const documentBatchRequest = z.object({
  employeeIds: z
    .array(id.refine((value) => value !== '00000000-0000-0000-0000-000000000000'))
    .min(1)
    .max(25)
    .refine((values) => distinct(canonicalIds(values))),
  expiringWithinDays: z.number().int().min(1).max(365),
});
export type DocumentBatchRequest = z.infer<typeof documentBatchRequest>;
export function documentCompletenessBatchFor(input: DocumentBatchRequest) {
  const expected = documentBatchRequest.parse(input);
  const selected = canonicalIds(expected.employeeIds);
  return z
    .object({
      metadata: z.object({
        generatedAtUtc: time,
        timeZone: z.string().min(1),
        asOf: date,
        reminderThrough: date,
        filters: documentBatchRequest,
        coverage: z.literal('explicit_selected_employees_only'),
        documentCoverage: z.literal(
          'selected_standard_documents_and_current_or_draft_certifications',
        ),
        dataKind: z.literal('factual_document_completeness'),
        reminderStatus: z.literal('not_scheduled_or_delivered'),
      }),
      rows: z.array(completeness).min(1).max(25),
    })
    .refine(
      ({ metadata, rows }) =>
        metadata.filters.expiringWithinDays === expected.expiringWithinDays &&
        JSON.stringify(canonicalIds(metadata.filters.employeeIds)) === JSON.stringify(selected) &&
        JSON.stringify(canonicalIds(rows.map((row) => row.employeeId))) ===
          JSON.stringify(selected) &&
        Date.parse(metadata.reminderThrough) - Date.parse(metadata.asOf) ===
          expected.expiringWithinDays * 86400000 &&
        rows.every(
          (row) =>
            row.asOf === metadata.asOf &&
            row.reminderThrough === metadata.reminderThrough &&
            documentCompletenessFor(row.employeeId, expected.expiringWithinDays).safeParse(row)
              .success,
        ),
      'Document report does not match the selected employees or expiry window.',
    );
}
export type DocumentBatchResponse = z.infer<ReturnType<typeof documentCompletenessBatchFor>>;
const documentState = z.object({
  lifecycleStatus: z.number().int().min(0).max(3),
  scanStatus: z.number().int().min(0).max(3),
  verificationStatus: z.number().int().min(0).max(2),
  signatureVerified: z.boolean(),
  hasScanConflict: z.boolean(),
  isScanInProgress: z.boolean(),
  activeScanAttemptId: id.nullable(),
  scanStartedAt: time.nullable(),
  scannedAt: time.nullable(),
  scannerVersion: z.string().nullable(),
  verifiedAt: time.nullable(),
  verifiedScanAt: time.nullable(),
  verifiedByUserId: z.string().nullable(),
  verificationReason: z.string().nullable(),
  lifecycleChangedAt: time.nullable(),
  lifecycleChangedByUserId: z.string().nullable(),
  lifecycleReason: z.string().nullable(),
});
export const documentHistoryEntry = z.object({
  id,
  revision: z.number().int().positive(),
  action: z.number().int().min(1).max(9),
  actorUserId: z.string(),
  reason: z.string(),
  recordedAt: time,
  scanAttemptId: id.nullable(),
  before: documentState.nullable(),
  after: documentState,
});
export function documentHistoryPage(afterRevision: number, take: number) {
  return z
    .array(documentHistoryEntry)
    .max(take)
    .refine(
      (rows) =>
        distinct(rows.map((row) => row.id)) &&
        rows.every(
          (row, index) =>
            row.revision > afterRevision &&
            (index === 0 || row.revision > rows[index - 1]!.revision),
        ),
      'Document history is duplicated, stale or out of order.',
    );
}
