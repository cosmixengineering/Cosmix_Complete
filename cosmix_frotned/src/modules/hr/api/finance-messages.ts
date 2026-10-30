import { errorMessage } from '../utils/messages';

// Server conflict/validation codes for allowances, advances, vehicles and payroll handoff.
// Anything unknown still falls back to the shared status-based message.
const codes: Record<string, string> = {
  payroll_reconciliation_batch_required: 'Select a frozen payroll batch for the comparison.',
  payroll_reconciliation_batch_period_mismatch:
    'The frozen batch must cover the selected full month in PKR.',
  payroll_reconciliation_mapping_required:
    'Map all required fields to distinct existing source headers.',
  payroll_reconciliation_single_sheet_required:
    'Choose a source with exactly one nonempty worksheet and at most 5,000 rows.',
  payroll_reconciliation_duplicate_header:
    'The sheet contains duplicate column headers. Correct the source file.',
  payroll_reconciliation_missing_cell:
    'A required cell is missing. Supply an explicit amount, including zero where applicable.',
  payroll_reconciliation_money_invalid:
    'Use nonnegative plain PKR numbers with at most two decimal places in every amount cell.',
  payroll_reconciliation_employee_code_invalid:
    'Use the retained employee code in every source row.',
  payroll_reconciliation_row_totals_invalid:
    'Source gross, deduction, statutory and net amounts do not reconcile. Correct the sheet.',
  payroll_reconciliation_duplicate_employee:
    'The source contains a duplicate employee. Correct the sheet before comparing.',
  payroll_reconciliation_population_mismatch:
    'The sheet and frozen payroll have different employee populations. Resolve missing employees before approval.',
  payroll_reconciliation_active_run_exists:
    'This frozen batch already has an active parallel run. Open the saved run and its review history.',
  payroll_reconciliation_version_conflict:
    'This run changed. Reload the saved evidence and review its latest version.',
  payroll_reconciliation_state_conflict:
    'This review stage is no longer available. Reload the saved run.',
  payroll_reconciliation_source_changed:
    'The frozen payroll source changed. Review the source before proceeding.',
  payroll_reconciliation_accrual_required:
    'The salary accrual must be posted before Finance can approve this comparison.',
  payroll_reconciliation_accrual_mismatch:
    'Retained payroll accruals do not reconcile to the frozen salary. Review the accounting evidence.',
  payroll_reconciliation_account_mismatch:
    'Retained journal accounts do not match approved posting rules. Review the account mappings.',
  payroll_reconciliation_finance_changed:
    'Finance evidence changed since review. Resolve the changed accounting evidence before approval.',
  payroll_reconciliation_explanation_invalid:
    'Explain every variance using a valid root cause and a single paragraph of evidence. A nonzero variance cannot be an exact match.',
  payroll_reconciliation_explanation_membership:
    'The explanations must match every retained employee and component. Reload the run before reviewing.',
  payroll_reconciliation_evidence_invalid:
    'Retained comparison evidence could not be verified. Contact support with the run reference.',
  payroll_reconciliation_not_found: 'The saved parallel payroll run could not be found.',
  salary_import_source_already_uploaded:
    'This file is already retained. Open Retained imports to resume its mapping or view its submission receipt.',
  salary_import_already_submitted:
    'This source has already been submitted. Review its retained submission receipt.',
  salary_import_preview_changed:
    'The approved source records changed. Validate the mapped rows again before submitting.',
  salary_import_validation_failed:
    'The import contains validation issues. Correct the source or column mapping and validate again.',
  salary_import_mapping_invalid: 'Map every required target to one distinct source column.',
  salary_import_mapping_unknown_column:
    'A mapped column is missing from this worksheet. Review the mapping.',
  salary_import_file_size: 'Choose a nonempty source file of at most 4 MiB.',
  salary_import_file_invalid:
    'The source file could not be read as a supported CSV or XLSX document.',
  salary_import_source_unavailable:
    'The retained source is unavailable. Contact support with its source reference.',
  salary_policy_month_boundary_required:
    'The salary policy must start on the first day of a month.',
  salary_policy_source_changed:
    'The previous policy changed. Refresh the policy history before proposing a replacement.',
  salary_component_source_changed:
    'The previous component changed. Refresh the employee components before proposing a replacement.',
  salary_component_request_pending:
    'A component request is already pending. Review that request first.',
  payroll_accounting_period_not_open:
    'The posting date must fall within an independently approved open period for this currency.',
  payroll_accounting_period_overlap:
    'This financial period overlaps an existing period. Check the saved periods.',
  payslip_not_found: 'No approved payslip is available for this employee and run.',
  payslip_totals_invalid:
    'The retained payslip totals could not be verified. Contact support; no document was downloaded.',
  // Shared write-path codes
  concurrent_change_retry:
    'This record changed while you were editing it. Reload the record and repeat the action with the latest values.',
  conflicting_change:
    'Another change was saved first. Reload the record and repeat the action with the latest values.',
  idempotency_key_reused:
    'This action was already submitted with a different payload. Reload and start again.',
  idempotency_key_expired:
    'The retained action expired before it completed. Reload and start again.',
  idempotency_result_unavailable:
    'The earlier result is unconfirmed. Reload to check the record before retrying.',
  invalid_idempotency_key:
    'The action could not be submitted safely. Reload the page and try again.',
  invalid_attendance_reason: 'Enter a reason using ordinary text.',
  access_denied: 'Your permissions do not allow this action.',
  private_evidence_unavailable:
    'The protected evidence for this record is unavailable. Reload or contact support.',
  invalid_expected_version:
    'The record version was missing or malformed. Reload the record and try again.',
  invalid_page: 'Choose a valid page number and page size.',

  // Allowances
  allowance_duplicate_claim:
    'This allowance kind already has a claim for that date. Review the existing claim.',
  allowance_version_conflict:
    'This claim changed since you opened it. Reload the queue and decide the latest version.',
  allowance_claim_closed:
    'Only a pending claim can be changed. Reload the queue to see its current state.',
  allowance_decision_evidence_unavailable:
    'The retained eligibility evidence no longer supports this decision. Reload the claim or contact support.',
  allowance_settlement_state_conflict:
    'This claim is not scheduled by a locked payroll handoff, or its settlement is already recorded.',
  allowance_evidence_unavailable:
    'The retained duty evidence no longer matches this claim. Reload before deciding.',
  allowance_employment_source_unavailable:
    'No employment assignment was found for your employee record on this date. HR/Admin must check the effective assignment and staff category before eligibility can be calculated.',
  overtime_employment_history_conflict:
    'More than one employment assignment covers this date. HR/Admin must resolve the overlapping assignment history.',
  allowance_source_limit: 'Too many sources were supplied for this claim. Contact support.',
  allowance_correction_source_unavailable:
    'The correction source for this claim is unavailable. Contact support.',
  allowance_policy_not_found: 'No allowance policy matches this reference. Reload the policy list.',
  allowance_policy_inactive: 'This allowance policy is no longer active. Reload the policy list.',
  allowance_policy_preview_changed:
    'The policy proposal changed since the preview. Preview the current values again.',
  allowance_policy_publication_invalid:
    'The allowance rules are not valid. Correct the highlighted values and preview again.',
  allowance_policy_version_conflict:
    'This policy changed since you opened it. Reload the policy before publishing.',
  allowance_policy_version_not_publishable:
    'This policy version cannot be published again. Reload the version history.',
  not_met: 'The verified duty cycle does not meet the published continuous-duty threshold yet.',
  inapplicable_staff_category: 'This allowance kind does not apply to the employee staff category.',
  amount_pending_management_decision:
    'Management has not confirmed a money amount for this allowance kind yet.',
  policy_missing: 'No effective allowance policy is published for this employee on this date.',
  deployment_workflow_unavailable:
    'The deployment service is unavailable for this request. No deployment calculation or claim is confirmed.',
  invalid_allowance_range:
    'Choose a range ending after the start date and no longer than 366 days.',
  invalid_allowance_kind: 'Choose a supported allowance kind.',
  invalid_allowance_decision: 'Choose approve, return or reject.',
  invalid_allowance_state: 'Choose a supported approval state.',
  invalid_allowance_page: 'Choose a valid page number and page size.',
  invalid_allowance_queue:
    'The review queue filters are not valid. Check the date range and state.',
  invalid_allowance_settlement:
    'The settlement values are not valid. Check the payment reference and reason.',
  invalid_allowance_text: 'The text contains unsupported characters or is too long.',
  invalid_policy_code: 'Use a policy code of supported length and characters.',
  invalid_policy_page: 'Choose a valid page number and page size.',
  policy_code_exists: 'A policy with this code already exists. Choose another code.',
  policy_future_date_required: 'The effective date must be in the future.',
  policy_target_assignment_overlap:
    'This target already has an overlapping assignment. Adjust the dates.',
  policy_target_invalid: 'The assignment target type is not supported.',
  policy_target_not_found: 'The assignment target was not found. Check the identifier.',
  policy_version_not_found: 'The policy version was not found. Reload the policy.',
  policy_assignment_not_found: 'The assignment was not found. Reload the assignment history.',
  policy_assignment_already_ended: 'This assignment already has an end date.',
  policy_assignment_authority_invalid: 'The recorded authority is not valid for this assignment.',
  policy_assignment_outside_version:
    'The assignment dates fall outside the selected policy version.',
  invalid_policy_assignment:
    'The assignment values are not valid. Check the target, dates and reason.',
  invalid_policy_assignment_end:
    'The assignment end values are not valid. Check the date and reason.',

  // Advances and loans
  request_not_pending: 'Only a pending request can be decided. Reload the request list.',
  request_not_found: 'The request was not found. Reload the list.',
  agreement_not_found: 'The agreement was not found. Reload the list.',
  pending_request_already_exists:
    'A pending request already exists for this employee. Review it before submitting another.',
  advanceloan_self_decision_forbidden: 'You cannot decide a request you submitted for yourself.',
  currency_mismatch: 'The currency does not match the agreement currency.',
  invalid_currency_pkr_required: 'Advances and loans are recorded in PKR only.',
  invalid_requested_amount: 'Enter an amount between 1 and 10,000,000.',
  invalid_approved_principal: 'Enter an approved principal between 1 and 10,000,000.',
  invalid_installments_count: 'Choose between 1 and 60 installments.',
  invalid_first_due_date: 'The first due date must be on or after the effective date.',
  invalid_installment_split: 'The installment amounts do not add up to the approved principal.',
  repayment_must_be_positive: 'Enter an amount greater than zero.',
  repayment_exceeds_outstanding_balance:
    'The repayment exceeds the outstanding balance. Check the ledger first.',
  original_entry_not_found: 'The ledger entry selected for reversal was not found.',
  original_entry_mismatch:
    'The ledger entry selected for reversal does not belong to this agreement.',
  cannot_reverse_a_reversal: 'A reversal entry cannot be reversed again.',

  // Vehicles and fuel
  vehicle_registration_already_exists: 'A vehicle with this registration number already exists.',
  vehicle_not_found: 'The vehicle was not found. Reload the vehicle list.',
  vehicle_inactive: 'This vehicle is inactive and cannot be issued.',
  vehicle_already_in_custody: 'This vehicle already has an active custody holder.',
  employee_already_has_active_custody: 'This employee already holds an active vehicle custody.',
  custody_not_found: 'The custody record was not found. Reload the custody history.',
  custody_already_returned: 'This custody has already been returned.',
  trip_not_found: 'The trip was not found. Reload the queue.',
  trip_not_pending: 'Only a pending trip can be approved. Reload the queue.',
  fuel_claim_not_found: 'The fuel claim was not found. Reload the queue.',
  claim_not_pending: 'Only a pending record can be approved. Reload the queue.',
  service_record_not_found: 'The maintenance record was not found. Reload the queue.',
  service_record_not_pending:
    'Only a pending maintenance record can be approved. Reload the queue.',
  trip_must_be_approved_before_fuel_claim:
    'The trip must be approved before a fuel claim can be submitted for it.',
  fuel_claim_already_submitted_for_this_trip: 'A fuel claim already exists for this trip.',
  vehicle_fuel_policy_not_configured:
    'The active vehicle fuel policy is not configured, so this amount cannot be calculated yet.',
  trip_end_must_follow_start: 'The trip end time must be after the start time.',
  odometer_end_must_be_greater_than_start:
    'The ending odometer must be greater than the starting odometer.',
  incident_outside_custody_window:
    'The incident time falls outside the custody window of this record.',

  // Payroll handoff
  payroll_handoff_month_not_locked:
    'This attendance month is not locked yet. Lock the month before preparing a payroll handoff.',
  payroll_handoff_period_already_prepared:
    'A payroll handoff batch already exists for this period.',
  payroll_handoff_employee_already_prepared:
    'An employee in this selection already belongs to an active payroll batch for this month. Review the existing batches and select the remaining employees.',
  payroll_handoff_employee_not_in_month:
    'A selected employee is not in this month’s authoritative salary preview. Refresh the preview and select again.',
  invalid_payroll_handoff_employee_selection:
    'Select at least one employee, without duplicates, for this payroll group.',
  payroll_handoff_preview_changed:
    'The preview changed since it was loaded. Reload the preview and prepare again.',
  payroll_handoff_lines_unavailable: 'No payroll lines are available for this period.',
  payroll_handoff_population_unavailable: 'The locked population for this period is unavailable.',
  payroll_handoff_currency_conflict: 'An allowance claim in this period is not recorded in PKR.',
  payroll_handoff_component_invalid:
    'A projected line carries an unsupported component. Contact support.',
  payroll_handoff_allowance_claim_missing:
    'An allowance line in this batch no longer exists. Reload the batch.',
  payroll_handoff_allowance_claim_already_settled:
    'An allowance line in this batch is already settled. Reload the batch before locking.',
  payroll_handoff_allowance_evidence_unavailable:
    'The allowance evidence for a line in this batch is unavailable. Reload the batch.',
  payroll_handoff_batch_not_found:
    'The payroll handoff batch was not found. Reload the batch list.',
  payroll_handoff_batch_locked: 'This batch is already locked.',
  payroll_handoff_batch_not_locked:
    'This batch must be locked before processing evidence can be recorded.',
  payroll_handoff_self_lock_forbidden:
    'The preparer cannot lock their own batch. A separate approver must lock it.',
  payroll_handoff_processing_actor_forbidden:
    'This account cannot record processing evidence for this run. Review the required Finance role and separation of duties.',
  payroll_handoff_processing_reference_exists:
    'This external processing reference has already been recorded for this batch.',
  invalid_payroll_handoff_request:
    'The handoff values are not valid. Check the period, fingerprint and reason.',
};

export function financeError(error: unknown): string {
  const known =
    typeof error === 'object' && error !== null && 'code' in error
      ? codes[String((error as { code?: unknown }).code ?? '')]
      : undefined;
  return known ? known : errorMessage(error);
}
