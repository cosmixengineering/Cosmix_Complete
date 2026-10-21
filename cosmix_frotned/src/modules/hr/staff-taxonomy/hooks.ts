import { useLayoutEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { useAuth } from '@/auth/use-auth';
import { ApiError } from '@/services/api/api-error';
import { request } from '@/services/api/http-client';
import { canTaxonomy, canTaxonomyAction } from './access';
import { type TaxonomyVersion, type WriteExpectation } from './contracts';
import { TaxonomyOperation, type OperationState } from './operation';
import { useTaxonomyLifetime } from './lifetime';

export function useTaxonomyQuery<T extends z.ZodType>(path: string, schema: T, enabled = true) {
  const { user, status } = useAuth();
  const lifetime = useTaxonomyLifetime();
  return useQuery({
    queryKey: ['hr', 'staff-taxonomy', lifetime.instance, path],
    enabled: enabled && canTaxonomy(user, status, 'read'),
    gcTime: 0,
    retry: false,
    refetchOnMount: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    queryFn: async ({ signal: querySignal }) => {
      const signal = AbortSignal.any([querySignal, lifetime.signal()]);
      try {
        const value = schema.parse(await request('/hr/staff-taxonomy' + path, { signal }));
        signal.throwIfAborted();
        // Verify exact retained bytes before offering a publication as a draft source.
        if (path.startsWith('/history') && value && typeof value === 'object' && 'items' in value) {
          for (const item of value.items as TaxonomyVersion[]) {
            if (item.evidenceState !== 'verified') continue;
            const digest = await crypto.subtle.digest(
              'SHA-256',
              new TextEncoder().encode(item.rulesJson),
            );
            const actual = Array.from(new Uint8Array(digest), (v) =>
              v.toString(16).padStart(2, '0'),
            ).join('');
            if (actual.toUpperCase() !== item.configurationSha256.toUpperCase())
              throw new Error('Publication fingerprint mismatch.');
            signal.throwIfAborted();
          }
        }
        return value;
      } catch (error) {
        if (!signal.aborted && error instanceof ApiError && [401, 403].includes(error.status))
          lifetime.revoke();
        throw error;
      }
    },
  });
}
export function useTaxonomyMutation() {
  const { user, status } = useAuth();
  const lifetime = useTaxonomyLifetime();
  const client = useQueryClient();
  const operation = useRef<TaxonomyOperation | null>(null);
  const [state, setState] = useState<OperationState>({
    pending: false,
    uncertain: false,
    reloadRequired: false,
    denied: false,
    error: null,
  });
  useLayoutEffect(
    () => () => {
      operation.current?.dispose();
      operation.current = null;
    },
    [],
  );
  async function run(body: unknown, expected: WriteExpectation) {
    const permitted =
      expected.action === 'request'
        ? canTaxonomy(user, status, 'read') &&
          canTaxonomy(user, status, 'request') &&
          user?.account?.accountId === expected.actorId
        : canTaxonomyAction(user, status, expected.action, expected.request);
    if (!permitted) {
      lifetime.revoke();
      return;
    }
    const signal = lifetime.signal();
    if (signal.aborted) return;
    operation.current ??= new TaxonomyOperation(
      (intent, currentSignal) =>
        request(intent.path, {
          method: 'POST',
          body: intent.body,
          idempotencyKey: intent.key,
          signal: currentSignal,
        }),
      (next) => {
        if (!signal.aborted) setState(next);
      },
    );
    try {
      const result = await operation.current.run(body, expected, signal);
      if (signal.aborted) return;
      if (operation.current.state.denied) {
        lifetime.revoke();
        return;
      }
      if (result) {
        await client.invalidateQueries({
          queryKey: ['hr', 'staff-taxonomy', lifetime.instance],
          refetchType: 'none',
        });
        if (!signal.aborted) return result;
      }
      return operation.current.state.uncertain ? undefined : null;
    } catch {
      if (!signal.aborted)
        setState((current) => ({
          ...current,
          error: 'Retry the unchanged action or reload and review current state.',
        }));
    }
  }
  return { ...state, run, locked: state.pending || state.uncertain || state.reloadRequired };
}
export type TaxonomyMutation = ReturnType<typeof useTaxonomyMutation>;
