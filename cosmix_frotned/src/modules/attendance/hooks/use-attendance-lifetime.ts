import { useLayoutEffect, useRef } from 'react';
import { useAuth } from '@/auth/use-auth';

export function useAttendanceLifetime() {
  const { user, status } = useAuth();
  const controller = useRef<AbortController | null>(null);
  useLayoutEffect(() => {
    const current = new AbortController();
    controller.current = current;
    if (status !== 'authenticated') current.abort();
    return () => current.abort();
  }, [user, status]);
  return () => controller.current?.signal ?? AbortSignal.abort();
}
