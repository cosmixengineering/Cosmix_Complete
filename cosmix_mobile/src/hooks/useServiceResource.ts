import { useCallback, useRef, useState } from 'react';
import { useFocusEffect } from '@react-navigation/native';
import { useServices } from '../app/AppProvider';
import type { ServiceData, ServiceKey } from '../types/selfService';
import type { ServiceQuery } from '../services/selfService/SelfServiceClient';
import { describeError } from '../utils/errors';

export function useServiceResource(service: ServiceKey, query: ServiceQuery) {
  const { selfService } = useServices();
  const [data, setData] = useState<ServiceData | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const generation = useRef(0);
  const { from, to, date, year, month, page, cursor } = query;
  const reload = useCallback(async () => {
    const current = ++generation.current;
    setLoading(true);
    setError('');
    setData(null);
    try {
      const result = await selfService.load(service, {
        from,
        to,
        date,
        year,
        month,
        page,
        cursor,
      });
      if (current === generation.current) {
        setData(result);
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
  }, [selfService, service, from, to, date, year, month, page, cursor]);
  useFocusEffect(
    useCallback(() => {
      reload();
      return () => {
        generation.current++;
      };
    }, [reload]),
  );
  return { data, error, loading, reload };
}
