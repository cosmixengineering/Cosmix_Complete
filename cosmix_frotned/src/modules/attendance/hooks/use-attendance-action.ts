import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { ApiError } from '@/services/api/api-error';
import { attendanceWrite } from '../api/attendance-api';
import { attendanceError } from '../utils/messages';
import { useAttendanceLifetime } from './use-attendance-lifetime';

export function useAttendanceAction() {
  const lifetime = useAttendanceLifetime();
  const cache = useQueryClient();
  const active = useRef(false);
  const intent = useRef<{
    fingerprint: string;
    key: string;
    body: unknown;
    uncertain: boolean;
  } | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [uncertain, setUncertain] = useState(false);
  async function run<T extends z.ZodType>(
    path: string,
    body: unknown,
    method: 'POST' | 'PUT',
    schema: T,
  ): Promise<z.infer<T> | undefined> {
    const signal = lifetime();
    if (active.current || signal.aborted) return;
    const fingerprint = JSON.stringify([path, method, body]);
    if (intent.current?.uncertain && intent.current.fingerprint !== fingerprint) {
      setError(
        'Retry the unchanged action or reload and check the previous result before editing.',
      );
      return;
    }
    if (intent.current?.fingerprint !== fingerprint)
      intent.current = {
        fingerprint,
        key: crypto.randomUUID(),
        body: JSON.parse(JSON.stringify(body)) as unknown,
        uncertain: false,
      };
    active.current = true;
    setPending(true);
    setError('');
    setSuccess('');
    try {
      const result = await attendanceWrite(
        path,
        intent.current.body,
        method,
        schema,
        intent.current.key,
        signal,
      );
      signal.throwIfAborted();
      intent.current = null;
      setUncertain(false);
      await cache.invalidateQueries({ queryKey: ['attendance'], refetchType: 'none' });
      signal.throwIfAborted();
      setSuccess('Saved successfully.');
      return result;
    } catch (failure) {
      if (signal.aborted) return;
      const definitive =
        failure instanceof ApiError &&
        [400, 401, 403, 404, 409, 413, 415, 422, 429].includes(failure.status);
      if (intent.current) intent.current.uncertain = !definitive;
      if (definitive) intent.current = null;
      setUncertain(!definitive);
      setError(attendanceError(failure));
    } finally {
      active.current = false;
      if (!signal.aborted) setPending(false);
    }
  }
  return { run, pending, error, success, uncertain };
}
