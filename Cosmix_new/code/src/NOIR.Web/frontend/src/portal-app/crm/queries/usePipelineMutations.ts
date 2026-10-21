import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createPipeline, updatePipeline, deletePipeline } from '@/services/crm'
import type { CreatePipelineRequest, UpdatePipelineRequest } from '@/types/crm'
import { crmPipelineKeys } from './queryKeys'

export const useCreatePipeline = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreatePipelineRequest) => createPipeline(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmPipelineKeys.all })
    },
  })
}

export const useUpdatePipeline = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdatePipelineRequest }) => updatePipeline(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmPipelineKeys.all })
    },
  })
}

export const useDeletePipeline = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deletePipeline(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmPipelineKeys.all })
    },
  })
}
