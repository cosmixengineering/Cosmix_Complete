import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';

const leaveWorkflowErrors: Readonly<Record<string, string>> = {
  leave_policy_year_transition_unavailable:
    'This leave-year change overlaps retained usage. The existing balance history must be reconciled before this transition can be used.',
  leave_benefit_policy_not_configured:
    'An approved benefit policy has not been configured for this employee and period.',
  leave_benefit_finalized_kpi_required:
    'The source KPI year must be finalized before a benefit can be awarded.',
  leave_benefit_kpi_review_pending:
    'The source KPI review must be completed before this award can proceed.',
  leave_benefit_kpi_dispute_unresolved:
    'Resolve the source KPI dispute before awarding the benefit.',
  leave_benefit_kpi_ineligible:
    'The verified KPI result does not meet the selected benefit policy.',
  leave_benefit_source_already_awarded:
    'This KPI source already has a retained award. Review its history before proceeding.',
  leave_benefit_consumed_adjustment_review_required:
    'Usage exists. Review the correction impact and exact consumed allocation instead of revoking as unused.',
  leave_benefit_pending_reservation:
    'A reservation is pending. Complete its existing review workflow before correcting this award.',
  leave_benefit_period_locked:
    'A required source or correction period is locked or not yet available. This action cannot rewrite it.',
  leave_benefit_already_revoked:
    'This award already has a reversal or correction. Reload its retained history.',
  leave_benefit_usage_already_allocated:
    'These source minutes are already allocated to a correction. Review the retained allocations.',
  leave_benefit_allocation_exceeds_award: 'The consumed allocation exceeds the original award.',
  leave_benefit_allocation_fractional_minute:
    'The historical day lengths do not convert these source minutes exactly. Review the allocation; it cannot be rounded.',
  leave_benefit_usage_changed:
    'Usage changed after the impact was loaded. Reload the impact and review the allocation again.',
  leave_recovery_fulfilment_not_configured:
    'An approved recovery fulfilment policy has not been configured.',
  leave_recovery_fulfilment_policy_pending:
    'The recovery fulfilment policy is pending approval or configuration.',
  leave_recovery_verified_session_required:
    'Verified closed attendance evidence is required for this recovery work.',
  leave_recovery_actual_work_unavailable:
    'Qualifying actual-work evidence is unavailable. A recovery promise alone cannot count as completed work.',
  leave_recovery_obligation_overallocated:
    'The claimed work exceeds the remaining recovery obligation.',
  leave_recovery_partial_fulfilment_disabled:
    'The selected policy does not permit partial fulfilment.',
  leave_recovery_scheduled_work_not_qualifying:
    'The selected work is scheduled duty and does not qualify under this recovery policy.',
  leave_recovery_period_locked:
    'The recovery period is locked. This review cannot rewrite finalized history.',
  leave_recovery_work_already_decided:
    'This claim has already been decided. Reload its retained decision.',
  leave_recovery_work_source_changed:
    'The work evidence changed. Reload the claim and review the current evidence before deciding.',
};

export function attendanceError(error: unknown) {
  if (error instanceof z.ZodError)
    return 'The response could not be validated. Check the record before retrying.';
  if (error instanceof ApiError) {
    const workflowMessage =
      (error.status === 400 || error.status === 409) && error.code
        ? leaveWorkflowErrors[error.code]
        : undefined;
    const message =
      workflowMessage ??
      (error.status === 503
        ? 'This service or required database schema is unavailable.'
        : error.status === 409
          ? 'The record or preview changed. Reload and review before submitting again.'
          : error.status === 400
            ? 'Check the dates, required values and approved references.'
            : error.message);
    return message + (error.correlationId ? ` Reference: ${error.correlationId}` : '');
  }
  return 'The result is uncertain. Retry the unchanged action or reload and check the record.';
}
export function businessToday(now = new Date()) {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Karachi',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}
