import { ApiError } from '@/services/api/api-error';
import { z } from 'zod';

export function errorMessage(error: unknown): string {
  if (error instanceof z.ZodError)
    return 'The response could not be validated. Reload or contact support.';
  if (error instanceof ApiError) {
    const known: Record<string, string> = {
      face_employee_ineligible:
        'This employee needs approved hiring, active employment, a valid joining date and an active account before biometric registration.',
      face_device_ineligible:
        'Choose a verified, non-revoked mobile device registered to this employee.',
      face_capture_not_configured:
        'Camera enrollment is not enabled on the server. The approved biometric configuration must be completed first.',
      face_policy_not_confirmed:
        'The company biometric authority and retention configuration have not been confirmed on the server.',
      face_provider_not_configured:
        'The self-hosted face provider is not configured for enrollment.',
      face_provider_unavailable:
        'The face provider could not complete registration. Restore provider connectivity, then check enrollment status and take a new capture.',
      face_template_storage_unavailable:
        'Private biometric storage is unavailable. The face registration has not been confirmed.',
      face_capture_expired:
        'The protected capture expired. Check enrollment history, then start a new capture.',
      face_enrollment_version_conflict:
        'This employee’s enrollment changed. Check the latest enrollment before replacing it.',
      face_enrollment_reconciliation_pending:
        'The server is still reconciling this registration. Check enrollment history before starting another request.',
      ineligible_reporting_manager:
        'The selected Reporting Manager is ineligible. Please select an active and approved employee (e.g. Engr. Imran Saeed or Engr. Tariq Mehmood).',
      invalid_employee_phone:
        'Personal phone number must follow Pakistani format (+92 followed by 10 digits, e.g. +923001234567).',
      invalid_employee_identity_document:
        'Identity document number is invalid (CNIC must be 13 digits: 42101-1234567-1, Passport min 6 chars).',
      invalid_employee_email:
        'Please provide a valid email format for Official Email or Personal Email.',
      invalid_employee_date_of_birth: 'Date of birth must be a valid date in the past.',
      invalid_employee_date_range:
        'Date range is invalid: End date / Probation date cannot be earlier than Start / Joining date.',
      invalid_employee_date: 'Please enter a valid date.',
      invalid_employee_reference:
        'A selected reference (Department, Designation, City, or Manager) is invalid or empty.',
      invalid_employee_address:
        'Address details are incomplete. Please provide address line 1, province, and city.',
      invalid_employee_emergency_contact:
        'Emergency contact details are incomplete or emergency phone format is invalid.',
      employee_login_phone_required:
        'Proposed mobile login phone is required when Mobile Access is checked.',
      employee_login_phone_conflict:
        'This login phone number is already registered to another user account.',
      site_staff_cannot_select_office:
        'Site / Field Operations staff cannot be assigned to an office branch. Please clear the branch selection.',
      select_staff_category_before_office:
        'Please select Workplace Placement (Office vs Site) before selecting an office branch.',
      unsupported_staff_category: 'Selected workplace category is invalid or inactive.',
      inactive_or_unknown_address_city: 'The selected address city is inactive or not found.',
      inactive_or_unknown_employee_master:
        'One of the selected items (Department, Designation, Employment Type, Branch, or Shift) is inactive.',
      employee_identity_or_email_conflict:
        'An employee with this CNIC/Passport or Official Email is already registered.',
      employee_phone_conflict: 'This phone number is already registered to another employee.',
      invalid_employee_document_reference:
        'One or more documents selected are invalid or not verified.',
      invalid_employee_previous_employment:
        'Previous employment entries have missing fields or invalid dates.',
      invalid_employee_certification: 'Certification entries have missing fields or invalid dates.',
      invalid_employee_asset_proposal: 'Asset proposal entries have missing fields.',
      employee_asset_custody_conflict:
        'This asset reference is already assigned to another active employee.',
      employee_company_sim_conflict:
        'This company SIM number is already assigned to another employee.',
      employee_form_incomplete_or_documents_unavailable:
        'Complete the missing fields and required verified documents before submitting.',
      employee_document_unavailable:
        'This document is not available. Check its scan and verification status.',
      employee_document_scan_unavailable:
        'Document scanning is unavailable. The file cannot be used until scanning succeeds.',
      employee_change_has_no_changes: 'No changed values were provided.',
      employee_basic_details_incomplete:
        'Complete both addresses and the emergency contact details.',
      employee_change_request_outstanding: 'A change request is already awaiting a decision.',
      duplicate_organization_code: 'This code already exists.',
      invalid_expected_version:
        'This employee record was modified by another action. Please refresh the page.',
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
