import { useLayoutEffect, useRef } from 'react';
import { useAuth } from '@/auth/use-auth';

// A request may finish after unmount, sign-out, or permission reload. Its result must not.
export function useHrLifetime() {
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
