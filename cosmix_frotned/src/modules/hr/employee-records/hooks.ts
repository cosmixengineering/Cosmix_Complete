import { useLayoutEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import type { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { hrGet, hrWrite } from '../api/hr-api';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrLifetime } from '../hooks/use-hr-lifetime';
import { errorMessage } from '../utils/messages';
import { captureRecordIntent, type RecordIntent } from './intent';
import { recordWriteSchema } from './contracts';

export function useRecordCache() {
  const [instance] = useState(() => crypto.randomUUID());
  const client = useQueryClient();
  useLayoutEffect(
    () => () => {
      void client.cancelQueries({ queryKey: ['hr', 'employee-records', instance] });
      client.removeQueries({ queryKey: ['hr', 'employee-records', instance] });
    },
    [client, instance],
  );
  return instance;
}
export function useRecordQuery<T extends z.ZodType>(
  instance: string,
  path: string,
  schema: T,
  permissions: string[],
  enabled = true,
) {
  const access = useHrAccess();
  const lifetime = useHrLifetime();
  return useQuery({
    queryKey: ['hr', 'employee-records', instance, path],
    queryFn: ({ signal }) => hrGet(path, schema, AbortSignal.any([signal, lifetime()])),
    enabled: enabled && access.can(...permissions),
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}
const messages: Record<string, string> = {
  employee_record_policy_not_configured:
    'Operational record policy is unavailable. An approved, effective management policy is required.',
  employee_sim_credit_policy_not_configured:
    'Approved SIM credit tiers are unavailable. Contact the policy administrator.',
  invalid_employee_sim_credit_tier: 'This credit tier is not in the current approved list.',
  employee_record_policy_changed: 'Operational policy changed. Reload and review the proposal.',
  employee_sim_credit_policy_changed: 'SIM credit policy changed. Reload and review the proposal.',
  employee_record_request_pending:
    'Another request in this family is pending. Review or cancel it before submitting.',
  employee_record_request_closed: 'This request is already closed. Reload its current state.',
  employee_record_evidence_unavailable:
    'Evidence must belong to the correct employee and be current, clean, verified and available.',
  employee_record_requires_current_business_date:
    'Use an effective time on the current configured business date, at or before server time.',
  employee_record_requires_active_unscheduled_employment:
    'This action requires active, unarchived employment without a scheduled status change.',
  asset_sim_receiver_must_be_active_account:
    'The receiving account must belong to an approved, active employee.',
  suspended_sim_transfer_requires_separate_policy: 'A suspended SIM cannot be transferred.',
  certification_replacement_preserves_validity:
    'Replacement must preserve the certificate number and validity dates.',
  certification_renewal_requires_later_validity:
    'Renewal needs an active certificate, later validity and new evidence.',
  certification_metadata_must_match_current_evidence:
    'Certificate validity dates must match its current verified evidence.',
  employee_record_snapshot_unavailable:
    'The frozen proposal is unavailable. Contact support before deciding.',
  business_timezone_not_configured:
    'The operational business timezone is not configured. Contact the policy administrator.',
};
export function recordError(failure: unknown) {
  if (failure instanceof ApiError && messages[failure.code ?? ''])
    return (
      messages[failure.code!] +
      (failure.correlationId ? ` Reference: ${failure.correlationId}` : '')
    );
  return errorMessage(failure);
}
export function isDefinitiveRecordFailure(failure: unknown) {
  return (
    failure instanceof ApiError &&
    ((failure.status >= 400 && failure.status < 500 && failure.status !== 408) ||
      (failure.status === 503 &&
        [
          'employee_record_policy_not_configured',
          'employee_sim_credit_policy_not_configured',
          'business_timezone_not_configured',
        ].includes(failure.code ?? '')))
  );
}
export type RecordMutation = ReturnType<typeof useRecordMutation>;
export function useRecordMutation(employeeId: string) {
  const lifetime = useHrLifetime();
  const client = useQueryClient();
  const intent = useRef<RecordIntent | null>(null);
  const busy = useRef(false);
  const [pending, setPending] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [reloadRequired, setReloadRequired] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useLayoutEffect(
    () => () => {
      intent.current = null;
    },
    [],
  );
  async function run(path: string, body: unknown, requestId?: string, decision?: number) {
    const signal = lifetime();
    if (busy.current || reloadRequired || signal.aborted) return;
    try {
      intent.current = captureRecordIntent(path, body, intent.current);
    } catch (failure) {
      setError((failure as Error).message);
      return;
    }
    const attempt = intent.current;
    busy.current = true;
    setPending(true);
    setError(null);
    try {
      const result = recordWriteSchema(employeeId, requestId, decision).parse(
        await hrWrite(attempt.path, attempt.body, 'POST', attempt.key, signal),
      );
      if (signal.aborted) return;
      intent.current = null;
      setUncertain(false);
      await client.invalidateQueries({ queryKey: ['hr'], refetchType: 'none' });
      if (!signal.aborted) return result;
    } catch (failure) {
      if (signal.aborted) return;
      const definitive = isDefinitiveRecordFailure(failure);
      if (definitive) {
        intent.current = null;
        setUncertain(false);
        setReloadRequired(
          failure instanceof ApiError && [401, 403, 404, 409].includes(failure.status),
        );
      } else setUncertain(true);
      setError(recordError(failure));
      if (definitive) return null;
    } finally {
      busy.current = false;
      if (!signal.aborted) setPending(false);
    }
  }
  return {
    run,
    pending,
    uncertain,
    reloadRequired,
    error,
    locked: pending || uncertain || reloadRequired,
  };
}
