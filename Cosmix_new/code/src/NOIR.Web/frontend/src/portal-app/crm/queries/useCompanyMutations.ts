import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createCompany, updateCompany, deleteCompany } from '@/services/crm'
import type { CreateCompanyRequest, UpdateCompanyRequest } from '@/types/crm'
import { crmCompanyKeys } from './queryKeys'

export const useCreateCompany = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateCompanyRequest) => createCompany(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmCompanyKeys.lists() })
    },
  })
}

export const useUpdateCompany = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateCompanyRequest }) => updateCompany(id, request),
    onSuccess: (data) => {
      queryClient.setQueryData(crmCompanyKeys.detail(data.id), data)
      queryClient.invalidateQueries({ queryKey: crmCompanyKeys.lists() })
    },
  })
}

export const useDeleteCompany = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteCompany(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmCompanyKeys.all })
    },
  })
}
