import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createContact, updateContact, deleteContact } from '@/services/crm'
import type { CreateContactRequest, UpdateContactRequest } from '@/types/crm'
import { crmContactKeys } from './queryKeys'

export const useCreateContact = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateContactRequest) => createContact(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmContactKeys.lists() })
    },
  })
}

export const useUpdateContact = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateContactRequest }) => updateContact(id, request),
    onSuccess: (data) => {
      queryClient.setQueryData(crmContactKeys.detail(data.id), data)
      queryClient.invalidateQueries({ queryKey: crmContactKeys.lists() })
    },
  })
}

export const useDeleteContact = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteContact(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmContactKeys.all })
    },
  })
}
