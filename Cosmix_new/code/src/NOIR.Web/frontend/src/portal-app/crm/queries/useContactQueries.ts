import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { getContacts, getContactById } from '@/services/crm'
import type { GetContactsParams } from '@/types/crm'
import { crmContactKeys } from './queryKeys'

export const useContactsQuery = (params: GetContactsParams) =>
  useQuery({
    queryKey: crmContactKeys.list(params),
    queryFn: () => getContacts(params),
    placeholderData: keepPreviousData,
  })

export const useContactQuery = (id: string | undefined) =>
  useQuery({
    queryKey: crmContactKeys.detail(id!),
    queryFn: () => getContactById(id!),
    enabled: !!id,
  })
