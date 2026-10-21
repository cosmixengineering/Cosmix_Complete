import { useCallback, useEffect, useRef, useState } from 'react';
import { useServices } from '../app/AppProvider';
import { AppError, describeError } from '../utils/errors';
import { isRecord } from '../utils/validation';

/** Discards responses from a previous date, selection or unmounted form. */
export function useSource<T>(route: string | null) {
  const { session } = useServices();
  const [loaded, setLoaded] = useState<{ route: string; value: T } | null>(
    null,
  );
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const generation = useRef(0);
  const reload = useCallback(async () => {
    const current = ++generation.current;
    setLoaded(null);
    setError('');
    if (!route) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const result = await session.request<T>(route);
      if (
        !isRecord(result) &&
        !(
          Array.isArray(result) &&
          result.length <= 1000 &&
          result.every(isRecord)
        )
      ) {
        throw new AppError('invalid_server_response');
      }
      if (current === generation.current) {
        setLoaded({ route, value: result });
      }
    } catch (failure) {
      if (current === generation.current) {
        setError(describeError(failure));
      }
    } finally {
      if (current === generation.current) {
        setLoading(false);
      }
    }
  }, [session, route]);
  const invalidate = useCallback(() => {
    generation.current += 1;
  }, []);
  useEffect(() => {
    reload();
    return invalidate;
  }, [reload, invalidate]);
  return {
    value: loaded?.route === route ? loaded.value : null,
    error,
    loading,
    reload,
  };
}
