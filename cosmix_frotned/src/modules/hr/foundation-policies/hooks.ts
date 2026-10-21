import { useCallback, useLayoutEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { request } from '@/services/api/http-client';
import { ApiError } from '@/services/api/api-error';
import { useAuth } from '@/auth/use-auth';
import { canFoundationPolicy } from './access';
import { capturePolicyIntent, type PolicyIntent } from './inputs';
import { writeSchema, type PolicyDecision, type PolicyOperation } from './contracts';

export function policyError(error: unknown) {
  if (error instanceof z.ZodError)
    return 'Policy data is unavailable or invalid. Check the selected values and reload the source.';
  if (error instanceof ApiError) {
    const codes: Record<string, string> = {
      hr_foundation_policy_authority_not_configured:
        'Approved HR foundation publication authority is unavailable. Contact the policy administrator.',
      hr_foundation_policy_business_time_zone_not_configured:
        'The server business timezone is unavailable. Contact the administrator.',
      hr_foundation_policy_request_pending:
        'Another proposal is pending. Review it before creating a new proposal.',
      hr_foundation_policy_backdating_not_allowed:
        'The effective date is now in the past. Reload and review the current business date.',
      hr_foundation_policy_effective_overlap:
        'This interval overlaps the latest policy publication. Reload and review its dates.',
      hr_foundation_policy_approved_version_reused:
        'This approved version label has already been published.',
      hr_foundation_policy_authority_changed:
        'Publication authority changed. This proposal needs to be withdrawn and submitted under the approved authority.',
      hr_foundation_policy_evidence_unavailable:
        'Protected policy evidence is unavailable. Reload or contact support.',
      hr_foundation_policy_history_invalid:
        'Publication history is unavailable. Contact the policy administrator.',
    };
    return (
      (codes[error.code ?? ''] ??
        (error.status === 503 ? 'HR foundation policy service is unavailable.' : error.message)) +
      (error.correlationId ? ` Reference: ${error.correlationId}` : '')
    );
  }
  return error instanceof Error && !(error instanceof TypeError)
    ? error.message
    : 'The result could not be confirmed. Reload or retry the unchanged action.';
}
export function usePolicyLifetime() {
  const client = useQueryClient();
  const [instance] = useState(() => crypto.randomUUID());
  const controller = useRef<AbortController | null>(null);
  useLayoutEffect(() => {
    const active = new AbortController();
    controller.current = active;
    return () => {
      active.abort();
      void client.cancelQueries({ queryKey: ['hr', 'foundation-policies', instance] });
      client.removeQueries({ queryKey: ['hr', 'foundation-policies', instance] });
    };
  }, [client, instance]);
  const signal = useCallback(() => controller.current?.signal ?? AbortSignal.abort(), []);
  return { instance, signal };
}
export type PolicyLifetime = ReturnType<typeof usePolicyLifetime>;
export async function policyGet<T extends z.ZodType>(
  path: string,
  schema: T,
  signal: AbortSignal,
): Promise<z.infer<T>> {
  const result = schema.parse(await request(path, { signal }));
  signal.throwIfAborted();
  return result;
}
export function usePolicyQuery<T extends z.ZodType>(
  lifetime: PolicyLifetime,
  path: string,
  schema: T,
  enabled = true,
) {
  const { user, status } = useAuth();
  return useQuery({
    queryKey: ['hr', 'foundation-policies', lifetime.instance, path],
    queryFn: ({ signal }) => policyGet(path, schema, AbortSignal.any([signal, lifetime.signal()])),
    enabled: enabled && canFoundationPolicy(user, status, 'read'),
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchOnMount: false,
  });
}
type Attempt = {
  path: string;
  body: unknown;
  operation: PolicyOperation;
  policyId?: string;
  requestId?: string;
  decision?: PolicyDecision;
};
export function usePolicyMutation(lifetime: PolicyLifetime) {
  const client = useQueryClient();
  const busy = useRef(false);
  const retained = useRef<{ intent: PolicyIntent; attempt: Attempt } | null>(null);
  const [pending, setPending] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [reloadRequired, setReloadRequired] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useLayoutEffect(
    () => () => {
      retained.current = null;
    },
    [],
  );
  async function run(input: Attempt, preflight: () => Promise<void>) {
    const signal = lifetime.signal();
    if (busy.current || reloadRequired || signal.aborted) return;
    busy.current = true;
    setPending(true);
    setError(null);
    let sent = false;
    try {
      if (!retained.current) {
        await preflight();
        signal.throwIfAborted();
      }
      const intent = capturePolicyIntent(input.path, input.body, retained.current?.intent ?? null);
      if (!retained.current)
        retained.current = { intent, attempt: { ...input, body: intent.body } };
      const attempt = retained.current.attempt;
      sent = true;
      const result = writeSchema(
        attempt.operation,
        attempt.policyId,
        attempt.requestId,
        attempt.decision,
      ).parse(
        await request(intent.path, {
          method: 'POST',
          body: intent.body,
          idempotencyKey: intent.key,
          signal,
        }),
      );
      if (signal.aborted) return;
      retained.current = null;
      setUncertain(false);
      await client.invalidateQueries({ queryKey: ['hr'], refetchType: 'none' });
      if (!signal.aborted) return result;
    } catch (failure) {
      if (signal.aborted) return;
      const definitive =
        !sent ||
        (failure instanceof ApiError &&
          ((failure.status >= 400 && failure.status < 500 && failure.status !== 408) ||
            (failure.status === 503 &&
              [
                'hr_foundation_policy_authority_not_configured',
                'hr_foundation_policy_business_time_zone_not_configured',
              ].includes(failure.code ?? ''))));
      if (definitive) {
        retained.current = null;
        setUncertain(false);
        setReloadRequired(
          !sent ||
            (failure instanceof ApiError && [401, 403, 404, 409, 503].includes(failure.status)),
        );
      } else setUncertain(true);
      setError(policyError(failure));
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
export type PolicyMutation = ReturnType<typeof usePolicyMutation>;
