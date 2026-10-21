import { useCallback, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Denied } from '../components/Feedback';
import { TaxonomyLifetimeContext } from './lifetime';

export function TaxonomySession({ children }: { children: ReactNode }) {
  const client = useQueryClient();
  const [instance] = useState(() => crypto.randomUUID());
  const [revoked, setRevoked] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const clear = useCallback(() => {
    controller.current?.abort();
    void client.cancelQueries({ queryKey: ['hr', 'staff-taxonomy', instance] });
    client.removeQueries({ queryKey: ['hr', 'staff-taxonomy', instance] });
  }, [client, instance]);
  useLayoutEffect(() => {
    controller.current = new AbortController();
    return clear;
  }, [clear]);
  if (revoked) return <Denied />;
  return (
    <TaxonomyLifetimeContext.Provider
      value={{
        instance,
        signal: () => controller.current?.signal ?? AbortSignal.abort(),
        revoke: () => {
          clear();
          setRevoked(true);
        },
      }}
    >
      {children}
    </TaxonomyLifetimeContext.Provider>
  );
}
