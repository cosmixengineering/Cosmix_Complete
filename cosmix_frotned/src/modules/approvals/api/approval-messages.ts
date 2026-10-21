import { ApiError } from '@/services/api/api-error';
import { errorMessage } from '../../hr/utils/messages';

export type ApprovalWriteOperation = 'publishRoute' | 'grantDelegation' | 'revokeDelegation';

// Server codes from ApprovalRoutingService, ApprovalRouteResolver and
// ApprovalRoutingRepository. Unknown codes still fall back to the shared status message.
const codes: Record<string, string> = {
  access_denied:
    'The server refused this action. Approval routing requires an administrator account holding the needed permission; publishing a route version additionally requires the fixed SuperAdmin role.',
  approval_route_unconfigured:
    'No configured route row matches this request type and subject, so routed requests fail with 409 approval_route_unconfigured. Publish the required stages first.',
  fixed_approval_route:
    'This request type uses a fixed approval route and cannot be configured here.',
  unsupported_approval_request_type:
    'Choose one of the supported request types: allowance, fuel, vehicle-maintenance, leave-encashment, advance, loan or policy-exception.',
  approval_route_version_conflict:
    'Another version of this route code is already the latest. Reload the routes, enter the current expected version and publish again.',
  approval_route_invalid_authority:
    'A configured stage does not carry the permission matching its stage number. Contact support.',
  approval_route_ambiguous:
    'Two configured rows claim the same stage for this request type. Contact support.',
  approval_route_incomplete:
    'The configured stages are not a contiguous 1..N sequence, so requests stay unconfigured until every stage is published.',
  approval_history_limit_exceeded:
    'This request type has reached the routing history limit. Contact support.',
  approval_delegation_version_conflict:
    'This delegation changed since you loaded it. Reload the delegations and retry with the latest version.',
  approval_delegation_overlap:
    'An overlapping active delegation already exists for this delegate, request type, permission and scope. Adjust the validity dates.',
  approval_delegation_already_revoked:
    'This delegation is already revoked. Reload the delegations.',
  approval_delegation_not_found: 'This delegation no longer exists. Reload the delegations.',
  invalid_approval_route:
    'The route values are not valid. Check the code pattern, stage, effective dates, amount bounds and currency.',
  invalid_approval_route_effective_date:
    'The effective from date must be in the future, later than the previous version of this code, and the request type must match that version.',
  invalid_approval_route_target:
    'The staff category, department or site reference does not identify an active record.',
  invalid_approval_delegation:
    'The delegation values are not valid. Check the delegate account, request type, stage, scope, target, dates and reason.',
  invalid_approval_delegate_account:
    'The chosen account cannot receive this delegation. It must be your own delegate target with an active, approved and eligible employment record.',
  invalid_approval_delegation_target: 'The delegation scope target does not exist.',
  invalid_approval_delegation_start: 'The delegation start cannot be in the past.',
  invalid_approval_reason:
    'Enter a single-line reason using ordinary text (up to 1000 characters).',
  invalid_approval_version:
    'The record version was missing or malformed. Reload the record and try again.',
  invalid_idempotency_key: 'The action could not be submitted safely. Reload and try again.',
  idempotency_key_reused:
    'This action was already submitted with a different payload. Reload and start again.',
  idempotency_result_unavailable:
    'The earlier result is unconfirmed. Reload to check the record before retrying.',
};

export function approvalError(error: unknown): string {
  const code = error instanceof ApiError ? error.code : undefined;
  const known = code ? codes[code] : undefined;
  if (known && error instanceof ApiError)
    return known + (error.correlationId ? ` Reference: ${error.correlationId}` : '');
  return errorMessage(error);
}

// The shared finance action hook only exposes the HTTP status, so the definitive
// outcomes these endpoints can return are mapped per operation. Non-definitive
// failures keep the shared message, which carries the correlation reference.
const writeHints: Record<ApprovalWriteOperation, Record<number, string>> = {
  publishRoute: {
    400: 'The route values were rejected. Check the code pattern, expected latest version, stage, effective dates, amount bounds, currency and reason.',
    403: 'Publishing a route version requires a fixed SuperAdmin administrator that also holds approvals.routes.manage with full scope. Ask a SuperAdmin to publish this version.',
    409: 'The latest version of this route code changed while you were publishing, or this idempotency key was reused with a different payload. Reload the routes, check the expected latest version and publish again.',
  },
  grantDelegation: {
    400: 'The delegation values were rejected. Check the delegate account id, request type, stage, scope and target, validity dates and reason.',
    403: 'Granting a delegation requires an administrator holding approvals.delegations.manage with full scope and the same stage-review authority being delegated.',
    409: 'An overlapping active delegation already exists for this delegate, request type, permission and scope, or the idempotency key was reused with a different payload. Adjust the validity dates.',
  },
  revokeDelegation: {
    400: 'The revoke values were rejected. Check the reason, then reload the delegations for the latest version.',
    403: 'Only the administrator who granted this delegation can revoke it.',
    409: 'This delegation changed or was already revoked, or the idempotency key was reused with a different payload. Reload the delegations and retry with the latest version.',
  },
};

export function approvalWriteError(
  operation: ApprovalWriteOperation,
  status: number | null,
  fallback: string | null,
): string | null {
  if (!fallback) return null;
  const hint =
    status === 400 || status === 403 || status === 409 ? writeHints[operation][status] : undefined;
  if (!hint) return fallback;
  const reference = / Reference: (\S+)$/.exec(fallback)?.[1];
  return reference ? `${hint} Reference: ${reference}` : hint;
}
