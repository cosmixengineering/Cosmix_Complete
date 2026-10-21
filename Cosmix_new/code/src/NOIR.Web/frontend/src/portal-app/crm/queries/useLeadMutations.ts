import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createLead, updateLead, moveLeadStage, winLead, loseLead, reopenLead } from '@/services/crm'
import type { CreateLeadRequest, UpdateLeadRequest, MoveLeadStageRequest } from '@/types/crm'
import { crmLeadKeys, crmPipelineKeys, crmDashboardKeys, crmContactKeys } from './queryKeys'

export const useCreateLead = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateLeadRequest) => createLead(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmLeadKeys.all })
      queryClient.invalidateQueries({ queryKey: crmPipelineKeys.views() })
      queryClient.invalidateQueries({ queryKey: crmDashboardKeys.all })
    },
  })
}

export const useUpdateLead = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateLeadRequest }) => updateLead(id, request),
    onSuccess: (data) => {
      queryClient.setQueryData(crmLeadKeys.detail(data.id), data)
      queryClient.invalidateQueries({ queryKey: crmLeadKeys.lists() })
      queryClient.invalidateQueries({ queryKey: crmPipelineKeys.views() })
    },
  })
}

export const useMoveLeadStage = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: MoveLeadStageRequest) => moveLeadStage(request),
    onSuccess: (data) => {
      queryClient.setQueryData(crmLeadKeys.detail(data.id), data)
      queryClient.invalidateQueries({ queryKey: crmPipelineKeys.views() })
    },
  })
}

export const useWinLead = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => winLead(id),
    onSuccess: (data) => {
      queryClient.setQueryData(crmLeadKeys.detail(data.id), data)
      queryClient.invalidateQueries({ queryKey: crmLeadKeys.lists() })
      queryClient.invalidateQueries({ queryKey: crmPipelineKeys.views() })
      queryClient.invalidateQueries({ queryKey: crmDashboardKeys.all })
      queryClient.invalidateQueries({ queryKey: crmContactKeys.all })
    },
  })
}

export const useLoseLead = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) => loseLead(id, reason),
    onSuccess: (data) => {
      queryClient.setQueryData(crmLeadKeys.detail(data.id), data)
      queryClient.invalidateQueries({ queryKey: crmLeadKeys.lists() })
      queryClient.invalidateQueries({ queryKey: crmPipelineKeys.views() })
      queryClient.invalidateQueries({ queryKey: crmDashboardKeys.all })
    },
  })
}

export const useReopenLead = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => reopenLead(id),
    onSuccess: (data) => {
      queryClient.setQueryData(crmLeadKeys.detail(data.id), data)
      queryClient.invalidateQueries({ queryKey: crmLeadKeys.lists() })
      queryClient.invalidateQueries({ queryKey: crmPipelineKeys.views() })
      queryClient.invalidateQueries({ queryKey: crmDashboardKeys.all })
    },
  })
}
