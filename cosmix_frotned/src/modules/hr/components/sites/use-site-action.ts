import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { hrWrite } from '../../api/hr-api';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { errorMessage } from '../../utils/messages';

const messages: Record<string, string> = {
  active_city_required: 'Select an active city.',
  active_project_required: 'The project reference must identify an active project.',
  approved_active_supervisor_required: 'Select an approved, active supervisor.',
  invalid_site_details: 'Check the required site details and date range.',
  site_not_found: 'This site no longer exists. Reload the list.',
  employee_not_approved_active:
    'Only an approved, active employee with eligible employment history can be assigned.',
  site_assignment_cannot_be_backdated:
    'The effective date cannot precede today or the joining date.',
  employee_assignment_state_conflict:
    'The assignment intervals conflict. Reload and review current assignments.',
  site_assignment_requires_later_start:
    'The effective date must be later than the open assignment start.',
  employee_not_site_staff:
    'The employee must have an active SITE staff category and no office assignment.',
  employee_already_assigned_to_site: 'The employee is already assigned to that site.',
  inactive_or_unknown_assignment_site:
    'Select an active site whose dates cover the effective date.',
  idempotency_key_reused: 'This retry key was already used. Reload and check the record.',
  idempotency_key_expired: 'The retry window expired. Reload and check the record.',
  idempotency_result_unavailable:
    'The previous result is unavailable. Reload and check the record.',
};

// Keep the exact request and key until an uncertain outcome can be resolved.
export function useSiteAction() {
  const lifetime = useHrLifetime();
  const cache = useQueryClient();
  const request = useRef<{
    path: string;
    body: unknown;
    method: 'POST' | 'PUT';
    key: string;
  } | null>(null);
  const busy = useRef(false);
  const [pending, setPending] = useState(false);
  const [locked, setLocked] = useState(false);
  const [reloadRequired, setReloadRequired] = useState(false);
  const [error, setError] = useState<string | null>(null);
  async function run<T>(path: string, body: unknown, method: 'POST' | 'PUT', schema: z.ZodType<T>) {
    const signal = lifetime();
    if (busy.current || signal.aborted || reloadRequired) return;
    if (!request.current) request.current = { path, body, method, key: crypto.randomUUID() };
    const attempt = request.current;
    busy.current = true;
    setPending(true);
    setLocked(true);
    setError(null);
    try {
      const result = schema.parse(
        await hrWrite(attempt.path, attempt.body, attempt.method, attempt.key, signal),
      );
      if (signal.aborted) return;
      request.current = null;
      await cache.invalidateQueries({ queryKey: ['hr'], refetchType: 'none' });
      if (signal.aborted) return;
      setLocked(false);
      return result;
    } catch (failure) {
      if (signal.aborted) return;
      if (failure instanceof ApiError && failure.status >= 400 && failure.status < 500) {
        request.current = null;
        setLocked(false);
        setReloadRequired(
          [
            'organization_version_conflict',
            'employee_version_conflict',
            'invalid_expected_version',
            'idempotency_key_reused',
            'idempotency_key_expired',
            'idempotency_result_unavailable',
          ].includes(failure.code ?? ''),
        );
      }
      const message = failure instanceof ApiError ? messages[failure.code ?? ''] : undefined;
      setError(
        message
          ? message +
              (failure instanceof ApiError && failure.correlationId
                ? ` Reference: ${failure.correlationId}`
                : '')
          : errorMessage(failure),
      );
    } finally {
      busy.current = false;
      if (!signal.aborted) setPending(false);
    }
  }
  return { run, pending, locked, reloadRequired, error };
}
