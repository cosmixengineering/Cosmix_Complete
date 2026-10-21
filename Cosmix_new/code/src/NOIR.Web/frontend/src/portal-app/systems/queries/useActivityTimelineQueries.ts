import { useQuery } from '@tanstack/react-query'
import { searchActivityTimeline, getActivityDetails, getPageContexts } from '@/services/audit'
import { activityTimelineKeys } from './queryKeys'

export interface ActivityTimelineParams {
  pageContext?: string
  operationType?: string
  userId?: string
  targetId?: string
  correlationId?: string
  searchTerm?: string
  fromDate?: string
  toDate?: string
  onlyFailed?: boolean
  page?: number
  pageSize?: number
}

export const useActivityTimelineQuery = (params: ActivityTimelineParams) =>
  useQuery({
    queryKey: activityTimelineKeys.search(params as Record<string, unknown>),
    queryFn: () => searchActivityTimeline(params),
  })

export const useActivityDetailsQuery = (id: string | undefined | null, enabled: boolean) =>
  useQuery({
    queryKey: activityTimelineKeys.details(id!),
    queryFn: () => getActivityDetails(id!),
    enabled: enabled && !!id,
  })

export const usePageContextsQuery = () =>
  useQuery({
    queryKey: activityTimelineKeys.pageContexts(),
    queryFn: () => getPageContexts(),
    staleTime: 10 * 60_000,
  })
