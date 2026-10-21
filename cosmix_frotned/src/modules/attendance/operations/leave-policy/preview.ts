import { useRef, useState } from 'react';
import { z } from 'zod';
import { attendanceWrite } from '../../api/attendance-api';
import { useAttendanceLifetime } from '../../hooks/use-attendance-lifetime';
import { attendanceError } from '../../utils/messages';

// These controller POSTs validate/read only; they never create a business receipt.
export function useServerPreview<T extends z.ZodType>(path: string, schema: T) {
  const lifetime = useAttendanceLifetime();
  const active = useRef(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  async function run(body: unknown): Promise<z.infer<T> | undefined> {
    const signal = lifetime();
    if (signal.aborted || active.current) return;
    active.current = true;
    setPending(true);
    setError('');
    try {
      const result = await attendanceWrite(path, body, 'POST', schema, crypto.randomUUID(), signal);
      signal.throwIfAborted();
      return result;
    } catch (failure) {
      if (!signal.aborted) setError(attendanceError(failure));
    } finally {
      active.current = false;
      if (!signal.aborted) setPending(false);
    }
  }
  return { run, pending, error };
}
