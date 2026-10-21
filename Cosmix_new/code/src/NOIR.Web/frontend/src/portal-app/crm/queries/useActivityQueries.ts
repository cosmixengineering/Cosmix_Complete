import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { getActivities } from '@/services/crm'
import type { GetActivitiesParams } from '@/types/crm'
import { crmActivityKeys } from './queryKeys'

export const useActivitiesQuery = (params: GetActivitiesParams) =>
  useQuery({
    queryKey: crmActivityKeys.list(params),
    queryFn: () => getActivities(params),
    placeholderData: keepPreviousData,
    enabled: !!(params.contactId || params.leadId),
  })
