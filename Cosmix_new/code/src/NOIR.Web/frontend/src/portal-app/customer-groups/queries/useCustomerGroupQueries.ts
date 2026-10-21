import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { getCustomerGroups, getCustomerGroupById, type GetCustomerGroupsParams } from '@/services/customerGroups'
import { customerGroupKeys } from './queryKeys'

export const useCustomerGroupsQuery = (params: GetCustomerGroupsParams = {}) =>
  useQuery({
    queryKey: customerGroupKeys.list(params),
    queryFn: () => getCustomerGroups(params),
    placeholderData: keepPreviousData,
  })

export const useCustomerGroupQuery = (id: string | undefined) =>
  useQuery({
    queryKey: customerGroupKeys.detail(id!),
    queryFn: () => getCustomerGroupById(id!),
    enabled: !!id,
  })
