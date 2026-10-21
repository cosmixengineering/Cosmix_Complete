import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { useRef, useState } from 'react';
import { financeGet, financeWrite } from '../api/hr-finance-api';
import { financeError } from '../api/finance-messages';
import { ApiError } from '@/services/api/api-error';
import { useHrAccess } from './use-hr-access';
import { useHrLifetime } from './use-hr-lifetime';

// Same cache lifetime as the HR queries, but keyed under 'hr' so a module-wide invalidation
// still refetches finance reads.
export function useFinanceQuery<T extends z.ZodType>(
  path: string,
  schema: T,
  allowed: boolean,
  enabled = true,
) {
  const access = useHrAccess();
  return useQuery({
    queryKey: ['hr', 'finance', access.subject, path],
    queryFn: ({ signal }) => financeGet(path, schema, signal),
    enabled: access.ready && allowed && enabled,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

// Retain an uncertain action's exact payload/key in memory. Never replay a changed action.
export function useFinanceAction() {
  const lifetime = useHrLifetime();
  const cache = useQueryClient();
  const active = useRef(false);
  const previous = useRef<{ fingerprint: string; key: string; uncertain: boolean } | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorStatus, setErrorStatus] = useState<number | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  async function run<T>(
    path: string,
    body: unknown,
    method: 'POST' | 'PUT',
    schema: z.ZodType<T>,
    onRejected?: () => void,
  ): Promise<T | undefined> {
    const signal = lifetime();
    if (active.current || signal.aborted) return;
    const fingerprint = JSON.stringify([path, method, body]);
    if (previous.current?.uncertain && previous.current.fingerprint !== fingerprint) {
      setError(
        'The previous result is uncertain. Retry the unchanged action or reload and check the record before editing.',
      );
      return;
    }
    if (previous.current?.fingerprint !== fingerprint)
      previous.current = { fingerprint, key: crypto.randomUUID(), uncertain: false };
    active.current = true;
    setPending(true);
    setError(null);
    setErrorStatus(null);
    setSuccess(null);
    try {
      const result = schema.parse(
        await financeWrite(path, body, method, previous.current.key, schema, signal),
      );
      if (signal.aborted) return;
      previous.current = null;
      await cache.invalidateQueries({ queryKey: ['hr'], refetchType: 'none' });
      if (signal.aborted) return;
      setSuccess('Saved successfully.');
      return result;
    } catch (failure) {
      if (signal.aborted) return;
      if (previous.current)
        previous.current.uncertain = !(failure instanceof ApiError) || failure.status >= 500;
      if (failure instanceof ApiError) {
        setErrorStatus(failure.status);
        if (failure.status >= 400 && failure.status < 500) onRejected?.();
      }
      setError(financeError(failure));
    } finally {
      active.current = false;
      if (!signal.aborted) setPending(false);
    }
  }
  return { run, pending, error, errorStatus, success };
}
