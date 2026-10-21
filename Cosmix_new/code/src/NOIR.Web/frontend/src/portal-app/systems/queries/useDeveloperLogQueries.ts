import { useQuery } from '@tanstack/react-query'
import { getAvailableLogDates, getHistoricalLogs, type DevLogLevel } from '@/services/developerLogs'
import { developerLogKeys } from './queryKeys'

export const useAvailableLogDatesQuery = () =>
  useQuery({
    queryKey: developerLogKeys.availableDates(),
    queryFn: () => getAvailableLogDates(),
  })

export interface HistoricalLogsParams {
  page: number
  pageSize: number
  search?: string
  levels?: DevLogLevel[]
  sortOrder: 'newest' | 'oldest'
}

export const useHistoricalLogsQuery = (date: string, params: HistoricalLogsParams) =>
  useQuery({
    queryKey: developerLogKeys.history(date, params as unknown as Record<string, unknown>),
    queryFn: () => getHistoricalLogs(date, params),
  })
