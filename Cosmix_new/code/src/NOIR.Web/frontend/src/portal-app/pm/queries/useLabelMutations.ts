import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createTaskLabel, deleteTaskLabel, addLabelToTask, removeLabelFromTask } from '@/services/pm'
import type { CreateTaskLabelRequest } from '@/types/pm'
import { pmLabelKeys, pmTaskKeys, pmBoardKeys } from './queryKeys'

export const useCreateLabel = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateTaskLabelRequest }) =>
      createTaskLabel(projectId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmLabelKeys.byProject(variables.projectId) })
    },
  })
}

export const useDeleteLabel = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, labelId }: { projectId: string; labelId: string }) =>
      deleteTaskLabel(projectId, labelId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmLabelKeys.byProject(variables.projectId) })
      queryClient.invalidateQueries({ queryKey: pmBoardKeys.all })
    },
  })
}

export const useAddLabelToTask = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ taskId, labelId }: { taskId: string; labelId: string }) =>
      addLabelToTask(taskId, labelId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmTaskKeys.detail(variables.taskId) })
      queryClient.invalidateQueries({ queryKey: pmBoardKeys.all })
    },
  })
}

export const useRemoveLabelFromTask = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ taskId, labelId }: { taskId: string; labelId: string }) =>
      removeLabelFromTask(taskId, labelId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmTaskKeys.detail(variables.taskId) })
      queryClient.invalidateQueries({ queryKey: pmBoardKeys.all })
    },
  })
}
