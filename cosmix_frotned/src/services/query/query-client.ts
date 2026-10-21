import { QueryClient } from '@tanstack/react-query';
import { ApiError } from '@/services/api/api-error';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      gcTime: 5 * 60_000,
      refetchOnWindowFocus: false,
      retry: (count, error) => error instanceof ApiError && error.status >= 500 && count < 1,
    },
    mutations: { retry: false },
  },
});
