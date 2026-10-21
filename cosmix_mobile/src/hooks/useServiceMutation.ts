import { useCallback, useEffect, useRef, useState } from 'react';
import { useFocusEffect } from '@react-navigation/native';
import { useServices } from '../app/AppProvider';
import type {
  DataRecord,
  RequestDraft,
  ServiceKey,
} from '../types/selfService';
import { AppError, describeError } from '../utils/errors';

export function useServiceMutation(onComplete?: () => Promise<void>) {
  const { requests } = useServices();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [receipt, setReceipt] = useState<DataRecord | null>(null);
  const [pending, setPending] = useState<RequestDraft | null>(null);
  const mounted = useRef(true);
  const flight = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  useFocusEffect(
    useCallback(() => {
      let active = true;
      requests
        .pending()
        .then(value => {
          if (active) {
            setPending(value);
          }
        })
        .catch(failure => {
          if (active) {
            setError(describeError(failure));
          }
        });
      return () => {
        active = false;
      };
    }, [requests]),
  );
  const run = async (operation: () => Promise<DataRecord>) => {
    if (flight.current) {
      throw new AppError('operation_busy');
    }
    flight.current = true;
    setBusy(true);
    setError('');
    setReceipt(null);
    try {
      const result = await operation();
      if (mounted.current) {
        setReceipt(result);
        await onComplete?.();
      }
      return result;
    } catch (failure) {
      if (mounted.current) {
        setError(describeError(failure));
      }
      throw failure;
    } finally {
      if (mounted.current) {
        try {
          const value = await requests.pending();
          if (mounted.current) {
            setPending(value);
          }
        } catch (failure) {
          setError(describeError(failure));
        }
        if (mounted.current) {
          setBusy(false);
        }
      }
      flight.current = false;
    }
  };
  return {
    busy,
    error,
    receipt,
    pending,
    submit: (
      service: ServiceKey,
      label: string,
      route: string,
      body: unknown,
    ) => run(() => requests.submit(service, label, route, body)),
    retry: () => run(() => requests.retry()),
  };
}
