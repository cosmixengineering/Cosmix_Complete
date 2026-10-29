export class AppError extends Error {
  constructor(
    public readonly code: string,
    public readonly status = 0,
    public readonly reference?: string,
  ) {
    super(code);
    this.name = 'AppError';
  }
}

const messages: Record<string, string> = {
  invalid_date: 'Choose a valid date in YYYY-MM-DD format.',
  invalid_time: 'Enter a valid company time in HH:mm format.',
  invalid_date_range: 'Choose an ordered date range of no more than 366 days.',
  invalid_amount:
    'Enter an amount within the displayed limits, with at most two decimal places.',
  invalid_request_reason:
    'Enter a reason within the displayed character limit.',
  evidence_too_large:
    'Choose at most four PNG, JPEG or PDF files, up to 10 MB each and 20 MB combined.',
  invalid_duty_interval:
    'Check that the end follows the start and the selected interval fits the available duty evidence.',
  source_unavailable:
    'Current company evidence or eligibility is unavailable. Refresh the sources or contact HR.',
  leave_policy_unavailable:
    'Select an applicable published policy. Contact HR if none is available.',
  invalid_leave_type: 'Choose an active leave type.',
  leave_evidence_required:
    'Add the required supporting documents and choose a distinct purpose for each.',
  leave_interval_outside_shift:
    'Your leave interval must fit the actual scheduled shift.',
  invalid_requested_minutes:
    'Enter a whole number from 60 to 43,200 leave minutes.',
  invalid_evidence:
    'The selected evidence could not be read safely. Choose a PNG, JPEG or PDF file again.',
  selection_cancelled: 'Document selection was cancelled.',
  pending_service_request:
    'Confirm your previous request before starting another submission.',
  no_pending_service_request:
    'There is no unconfirmed request for this session.',
  service_request_unknown:
    'The server result is not confirmed. Use Confirm previous request to retry the same submission.',
  request_reconciliation_required:
    'This request cannot be safely retried from this device. Review your history and contact HR with the request reference before submitting again.',
  leave_schedule_unavailable:
    'A dated shift is unavailable for one of the selected days. Ask HR to check your schedule.',
  request_source_changed:
    'The source details have changed. Reload the form and review the current details.',
  resignation_effective_time_must_be_future:
    'Choose a future requested effective date and time.',
  employee_lifecycle_request_or_change_outstanding:
    'An employment request or scheduled change is already outstanding. Review your current request or contact HR.',
  protected_owner_requires_separate_handover:
    'This account requires a separate owner handover process. Please contact HR.',
  invalid_currency_pkr_required:
    'This request uses PKR. Please enter an amount in PKR.',
  pending_request_already_exists:
    'A request of this type is already pending. Review your existing requests.',
  invalid_phone:
    'Enter your registered number in international format, starting with +.',
  invalid_pakistan_phone:
    'Enter your registered Pakistan mobile number, for example 0300 1234567.',
  otp_expired: 'This code has expired. Request another code to continue.',
  invalid_otp: 'Enter the six-digit verification code.',
  invalid_authentication:
    'Your code or session is no longer valid. Please sign in again.',
  authentication_required: 'Please sign in to continue.',
  authentication_unavailable:
    'Employee sign-in is not available yet. Please contact your administrator.',
  temporarily_limited: 'Please wait before trying again.',
  device_recovery_required:
    'This device needs administrator approval. Please contact your administrator.',
  access_denied: 'Your account does not have access to this action.',
  invalid_server_response:
    'The service returned an unexpected response. Please try again later.',
  document_open_failed:
    'Could not open this file. Try PDF or check that a compatible viewer is installed.',
  network_error:
    'Could not reach the company server. Check your connection and try again.',
  request_timeout: 'The server took too long to respond. Please try again.',
  secure_storage_unavailable:
    'Secure device storage is unavailable. Unlock your device and try again.',
  session_refresh_uncertain:
    'Your session could not be safely restored. Please sign in again.',
  session_expired: 'Your session has ended. Please sign in again.',
  session_changed: 'Your sign-in changed. Please try again.',
  operation_busy: 'Please wait for the current request to finish.',
  attendance_busy: 'An attendance request is already in progress.',
  attendance_result_unknown:
    'Your attendance result is not confirmed. Retry the same submission below.',
  attendance_not_available:
    'Attendance is not available for your current schedule or account.',
  pending_attendance:
    'Resolve your previous attendance submission before starting another.',
  no_pending_attendance:
    'There is no pending attendance submission for this session.',
  face_enrollment_required:
    'Your face needs to be enrolled by HR before Check In.',
  face_provider_unavailable:
    'Face verification is temporarily unavailable. Please try again later.',
  face_verification_failed:
    'Your face could not be verified. Use good lighting and keep one face in view.',
  face_challenge_expired:
    'The face verification window has expired. Please take a new photo.',
  face_retry_limit_reached:
    'The face verification retry limit has been reached. Please contact your supervisor.',
  face_evidence_use_expired:
    'Your face enrollment has expired. Please contact HR to enroll again.',
  face_capture_expired:
    'The photo verification window has expired. Please take a new photo.',
  camera_permission:
    'Camera access is needed to verify your face for Check In.',
  camera_unavailable:
    'The front camera is unavailable. Please try again on a supported device.',
  camera_cancelled: 'Photo capture was cancelled. No attendance was submitted.',
  invalid_face_image:
    'The photo could not be prepared safely. Please take another photo.',
  location_permission: 'Allow precise location access to submit attendance.',
  location_unavailable:
    'Your current location could not be obtained. Turn on precise location and try again.',
  location_mocked:
    'Attendance cannot use a simulated location. Please use your actual device location.',
  location_stale: 'Your location is no longer current. Please try again.',
  location_consent_required_before_gps_submission:
    'Please review and accept the company location collection policy before submitting attendance.',
  signout_unconfirmed:
    'Signed out on this device. Server sign-out could not be confirmed.',
};

export function errorCode(error: unknown): string {
  if (error instanceof AppError) {
    return error.code;
  }
  return 'unknown_error';
}

export function describeError(error: unknown): string {
  return (
    messages[errorCode(error)] ??
    'This action could not be completed. Please try again or contact your supervisor.'
  );
}

export function messageForCode(code: string): string {
  return (
    messages[code] ?? 'Please contact your supervisor if this issue continues.'
  );
}
