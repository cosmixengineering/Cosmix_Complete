import { ApiError } from '@/services/api/api-error';
import { z } from 'zod';

export function errorMessage(error: unknown): string {
  if (error instanceof z.ZodError)
    return 'The response could not be validated. Reload or contact support.';
  if (error instanceof ApiError) {
    const known: Record<string, string> = {
      employee_form_incomplete_or_documents_unavailable:
        'Complete the missing fields and required verified documents before submitting.',
      employee_document_unavailable:
        'This document is not available. Check its scan and verification status.',
      employee_document_scan_unavailable:
        'Document scanning is unavailable. The file cannot be used until scanning succeeds.',
      employee_identity_or_email_conflict:
        'This identity or official email is already in use. Review the entered details.',
      employee_change_has_no_changes: 'No changed values were provided.',
      employee_basic_details_incomplete:
        'Complete both addresses and the emergency contact details.',
      employee_change_request_outstanding: 'A change request is already awaiting a decision.',
      duplicate_organization_code: 'This code already exists.',
    };
    const text =
      known[error.code ?? ''] ??
      (error.status === 503
        ? 'The service is temporarily unavailable. Retry later.'
        : error.status === 413
          ? 'The file or request is too large.'
          : error.status === 400
            ? 'Some values could not be accepted. Check the required fields, formats and references.'
            : error.message);
    return text + (error.correlationId ? ` Reference: ${error.correlationId}` : '');
  }
  return 'The result could not be confirmed. Check your connection and retry the unchanged action, or reload to check the record.';
}
export const labelDate = (value?: string | null) =>
  value
    ? value.length === 10
      ? value
      : new Date(value).toLocaleString('en-GB', { timeZone: 'Asia/Karachi' })
    : 'Not set';
