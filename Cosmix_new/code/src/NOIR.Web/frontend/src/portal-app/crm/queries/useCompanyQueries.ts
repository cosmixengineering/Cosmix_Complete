import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { getCompanies, getCompanyById } from '@/services/crm'
import type { GetCompaniesParams } from '@/types/crm'
import { crmCompanyKeys } from './queryKeys'

export const useCompaniesQuery = (params: GetCompaniesParams) =>
  useQuery({
    queryKey: crmCompanyKeys.list(params),
    queryFn: () => getCompanies(params),
    placeholderData: keepPreviousData,
  })

export const useCompanyQuery = (id: string | undefined) =>
  useQuery({
    queryKey: crmCompanyKeys.detail(id!),
    queryFn: () => getCompanyById(id!),
    enabled: !!id,
  })
