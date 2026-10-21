import { ApiError } from '@/services/api/api-error';

const messages: Record<string, string> = {
  staff_taxonomy_authority_approval_required:
    'The approved taxonomy authority is unavailable or outside its effective window. Contact the deployment administrator.',
  staff_taxonomy_authority_changed_request_again:
    'Approval authority changed. Cancel the pending proposal and request it again under current authority.',
  staff_taxonomy_future_date_required:
    'The effective start must be after the current Pakistan business date.',
  staff_taxonomy_requires_later_effective_date:
    'Use a start later than every retained taxonomy publication.',
  staff_taxonomy_period_locked: 'The proposed period includes locked attendance records.',
  staff_taxonomy_request_pending:
    'A taxonomy proposal is already pending. Review or cancel it first.',
  staff_taxonomy_request_closed: 'This proposal is already closed. Reload its retained state.',
  staff_taxonomy_policy_version_conflict:
    'The policy revision changed. Reload and review current state.',
  staff_taxonomy_publication_changed_request_again:
    'The publication timeline changed. A fresh proposal is required.',
  staff_taxonomy_policy_reference_invalid:
    'A required policy version is unavailable, has the wrong policy use, or does not cover the proposed period.',
  staff_taxonomy_policy_reference_unavailable:
    'A required policy version is unavailable, has the wrong policy use, or does not cover the proposed period.',
  staff_taxonomy_request_version_conflict:
    'The request version changed. Reload and review current state.',
  staff_taxonomy_confirmed_baseline_changed:
    'Retain the confirmed OFFICE and SITE mapping constraints.',
  staff_taxonomy_history_conflict:
    'The retained publication history is inconsistent. Publication is unavailable.',
  staff_taxonomy_evidence_invalid:
    'The retained approval evidence cannot be verified. Contact the administrator.',
  idempotency_key_reused:
    'The action key belongs to a different intent. Reload and review current state.',
  idempotency_result_unavailable:
    'The replay receipt is unavailable. Reload and check whether the action completed.',
};
export function taxonomyError(error: unknown) {
  if (error instanceof ApiError)
    return (
      (messages[error.code ?? ''] ?? error.message) +
      (error.correlationId ? ` Reference: ${error.correlationId}` : '')
    );
  return 'The response could not be confirmed. Reload reads, or retry the retained unchanged action.';
}
export function definitiveTaxonomyFailure(error: unknown) {
  return (
    error instanceof ApiError &&
    ((error.status >= 400 && error.status < 500 && error.status !== 408) ||
      (error.status === 503 && error.code === 'staff_taxonomy_authority_approval_required'))
  );
}
export const resolutionLabels: Record<string, string> = {
  effective: 'Effective approved mapping',
  legacy_baseline: 'Confirmed Office/Site baseline',
  category_not_configured: 'Category mapping unavailable',
  taxonomy_not_effective: 'No effective taxonomy',
  ambiguous_taxonomy: 'Conflicting taxonomy publications',
  invalid_taxonomy_evidence: 'Approval evidence unavailable',
  taxonomy_history_conflict: 'Publication history conflict',
  invalid_taxonomy_rules: 'Unsupported taxonomy rules',
  mapping_changes_during_interval: 'Mapping changes within the interval',
};
