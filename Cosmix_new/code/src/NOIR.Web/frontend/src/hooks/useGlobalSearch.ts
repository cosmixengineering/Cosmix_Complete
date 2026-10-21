import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { globalSearch } from '@/services/search'

export const useGlobalSearch = (query: string, enabled: boolean) =>
  useQuery({
    queryKey: ['global-search', query],
    queryFn: () => globalSearch(query),
    enabled: enabled && query.length >= 2,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  })
