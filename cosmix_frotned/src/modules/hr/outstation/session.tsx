import {
  createContext,
  useCallback,
  useContext,
  useLayoutEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { useAuth } from '@/auth/use-auth';
import { request } from '@/services/api/http-client';
import { ApiError } from '@/services/api/api-error';
import { Denied } from '../components/Feedback';
import { canDeployment } from './contracts';
import { initialWriteState, OutstationOperation, type OutstationIntent } from './api';

const Context = createContext<{
  instance: string;
  signal: () => AbortSignal;
  revoke: () => void;
} | null>(null);
function useLifetime() {
  const value = useContext(Context);
  if (!value) throw new Error('Missing outstation session.');
  return value;
}
export function OutstationSession({ children }: { children: ReactNode }) {
  const client = useQueryClient();
  const [instance] = useState(() => crypto.randomUUID());
  const [revoked, setRevoked] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const clear = useCallback(() => {
    controller.current?.abort();
    void client.cancelQueries({ queryKey: ['hr', 'outstation', instance] });
    client.removeQueries({ queryKey: ['hr', 'outstation', instance] });
  }, [client, instance]);
  useLayoutEffect(() => {
    controller.current = new AbortController();
    return clear;
  }, [clear]);
  return revoked ? (
    <Denied />
  ) : (
    <Context.Provider
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
    </Context.Provider>
  );
}
export function useOutstationQuery<T extends z.ZodType>(
  path: string,
  schema: T,
  employeeId: string,
  enabled = true,
) {
  const { user, status } = useAuth();
  const lifetime = useLifetime();
  return useQuery({
    queryKey: ['hr', 'outstation', lifetime.instance, path],
    enabled: enabled && status === 'authenticated' && canDeployment(user, 'read', employeeId),
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    queryFn: async ({ signal }) => {
      const combined = AbortSignal.any([signal, lifetime.signal()]);
      try {
        const data = schema.parse(await request(path, { signal: combined }));
        combined.throwIfAborted();
        return data;
      } catch (error) {
        if (!combined.aborted && error instanceof ApiError && [401, 403].includes(error.status))
          lifetime.revoke();
        throw error;
      }
    },
  });
}
export function useOutstationMutation() {
  const lifetime = useLifetime();
  const client = useQueryClient();
  const operation = useRef<OutstationOperation | null>(null);
  const [state, setState] = useState(initialWriteState);
  useLayoutEffect(
    () => () => {
      operation.current?.dispose();
      operation.current = null;
    },
    [],
  );
  function current() {
    operation.current ??= new OutstationOperation(
      (path, body, key, signal) =>
        request(path, { method: 'POST', body, idempotencyKey: key, signal }),
      setState,
    );
    return operation.current;
  }
  async function run(intent?: OutstationIntent) {
    const signal = lifetime.signal();
    const op = current();
    const result = intent ? await op.run(intent, signal) : await op.retry(signal);
    if (signal.aborted) return;
    if (op.state.denied) {
      lifetime.revoke();
      return;
    }
    if (result) await client.invalidateQueries({ queryKey: ['hr'], refetchType: 'none' });
    return signal.aborted ? undefined : result;
  }
  return { ...state, run, locked: state.pending || state.uncertain || state.reloadRequired };
}
