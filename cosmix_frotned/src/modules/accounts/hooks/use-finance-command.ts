import { useLayoutEffect, useRef, useState } from 'react';
import { z } from 'zod';
import { useAuth } from '@/auth/use-auth';
import { request } from '@/services/api/http-client';
import { ApiError } from '@/services/api/api-error';

export function useFinanceCommand() {
  const { user, status } = useAuth();
  const subject = user?.account?.accountId ?? 'none';
  const lifetime = useRef<AbortController | null>(null);
  const active = useRef(false);
  const intent = useRef<{ digest: string; key: string } | null>(null);
  const [state, setState] = useState({ subject, pending: false, uncertain: false, error: '' });
  useLayoutEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    intent.current = null;
    active.current = false;
    if (status !== 'authenticated') controller.abort();
    return () => controller.abort();
  }, [subject, status]);

  async function run<T>(path: string, body: unknown, schema: z.ZodType<T>): Promise<T | undefined> {
    const signal = lifetime.current?.signal ?? AbortSignal.abort();
    if (active.current || signal.aborted) return;
    const digest = JSON.stringify([path, body]);
    if (intent.current && intent.current.digest !== digest) {
      setState({
        subject,
        pending: false,
        uncertain: true,
        error:
          'The previous request may have completed. Retry that same request before changing it.',
      });
      return;
    }
    const command = intent.current ?? { digest, key: crypto.randomUUID() };
    intent.current = command;
    active.current = true;
    setState({ subject, pending: true, uncertain: false, error: '' });
    try {
      const result = schema.parse(
        await request(path, { method: 'POST', body, idempotencyKey: command.key, signal }),
      );
      if (signal.aborted) return;
      intent.current = null;
      setState({ subject, pending: false, uncertain: false, error: '' });
      return result;
    } catch (error) {
      if (signal.aborted) return;
      const rejected =
        error instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(error.status);
      if (rejected) intent.current = null;
      setState({
        subject,
        pending: false,
        uncertain: !rejected,
        error: rejected
          ? error.message
          : 'The result could not be confirmed. Retry the same request; it will not be submitted twice.',
      });
    } finally {
      if (!signal.aborted) active.current = false;
    }
  }
  return {
    run,
    pending: state.subject === subject && state.pending,
    uncertain: state.subject === subject && state.uncertain,
    error: state.subject === subject ? state.error : '',
  };
}
