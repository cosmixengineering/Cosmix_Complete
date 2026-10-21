import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createActivity, updateActivity, deleteActivity } from '@/services/crm'
import type { CreateActivityRequest, UpdateActivityRequest } from '@/types/crm'
import { crmActivityKeys } from './queryKeys'

export const useCreateActivity = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateActivityRequest) => createActivity(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmActivityKeys.all })
    },
  })
}

export const useUpdateActivity = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateActivityRequest }) => updateActivity(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmActivityKeys.all })
    },
  })
}

export const useDeleteActivity = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteActivity(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmActivityKeys.all })
    },
  })
}
