import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { useRef, useState } from 'react';
import { hrGet, hrWrite } from '../api/hr-api';
import { ApiError } from '@/services/api/api-error';
import { useHrAccess } from './use-hr-access';
import { errorMessage } from '../utils/messages';
import { useHrLifetime } from './use-hr-lifetime';

export function useHrQuery<T extends z.ZodType>(
  path: string,
  schema: T,
  permissions: string[],
  enabled = true,
) {
  const access = useHrAccess();
  return useQuery({
    queryKey: ['hr', access.subject, path],
    queryFn: ({ signal }) => hrGet(path, schema, signal),
    enabled: enabled && access.can(...permissions),
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

// Retain an uncertain action's exact payload/key in memory. Never replay a changed action.
export function useHrAction() {
  const lifetime = useHrLifetime();
  const cache = useQueryClient();
  const active = useRef(false);
  const previous = useRef<{ fingerprint: string; key: string; uncertain: boolean } | null>(null);
  const files = useRef(new WeakMap<Blob, string>());
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const [errorStatus, setErrorStatus] = useState<number | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  async function run<T>(
    path: string,
    body: unknown,
    method: 'POST' | 'PUT',
    schema: z.ZodType<T>,
    onRejected?: (error?: unknown) => void,
  ): Promise<T | undefined> {
    const signal = lifetime();
    if (active.current || signal.aborted) return;
    const fingerprint = JSON.stringify([
      path,
      method,
      body instanceof FormData
        ? [...body.entries()].map(([k, v]) => {
            if (v instanceof Blob) {
              if (!files.current.has(v)) files.current.set(v, crypto.randomUUID());
              return [k, files.current.get(v)];
            }
            return [k, v];
          })
        : body,
    ]);
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
    setErrorCode(null);
    setErrorStatus(null);
    setSuccess(null);
    try {
      const result = schema.parse(await hrWrite(path, body, method, previous.current.key, signal));
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
        setErrorCode(failure.code ?? null);
        if (failure.status >= 400 && failure.status < 500) onRejected?.(failure);
      } else {
        setErrorCode(null);
      }
      setError(errorMessage(failure));
    } finally {
      active.current = false;
      if (!signal.aborted) setPending(false);
    }
  }
  return { run, pending, error, errorCode, errorStatus, success };
}
