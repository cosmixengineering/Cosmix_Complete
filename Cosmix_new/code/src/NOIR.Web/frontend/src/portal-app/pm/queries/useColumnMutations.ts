import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createColumn, updateColumn, reorderColumns, deleteColumn, moveAllColumnTasks, duplicateColumn } from '@/services/pm'
import type { CreateColumnRequest, UpdateColumnRequest, ReorderColumnsRequest } from '@/types/pm'
import { pmProjectKeys, pmBoardKeys } from './queryKeys'

export const useCreateColumn = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateColumnRequest }) =>
      createColumn(projectId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.detail(variables.projectId) })
      queryClient.invalidateQueries({ queryKey: pmBoardKeys.board(variables.projectId) })
    },
  })
}

export const useUpdateColumn = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, columnId, request }: { projectId: string; columnId: string; request: UpdateColumnRequest }) =>
      updateColumn(projectId, columnId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.detail(variables.projectId) })
      queryClient.invalidateQueries({ queryKey: pmBoardKeys.board(variables.projectId) })
    },
  })
}

export const useReorderColumns = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: ReorderColumnsRequest }) =>
      reorderColumns(projectId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.detail(variables.projectId) })
      queryClient.invalidateQueries({ queryKey: pmBoardKeys.board(variables.projectId) })
    },
  })
}

export const useDeleteColumn = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, columnId, moveToColumnId }: { projectId: string; columnId: string; moveToColumnId: string }) =>
      deleteColumn(projectId, columnId, moveToColumnId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.detail(variables.projectId) })
      queryClient.invalidateQueries({ queryKey: pmBoardKeys.board(variables.projectId) })
    },
  })
}

export const useMoveAllColumnTasks = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, sourceColumnId, targetColumnId }: { projectId: string; sourceColumnId: string; targetColumnId: string }) =>
      moveAllColumnTasks(projectId, sourceColumnId, targetColumnId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmBoardKeys.board(variables.projectId) })
    },
  })
}

export const useDuplicateColumn = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, columnId }: { projectId: string; columnId: string }) =>
      duplicateColumn(projectId, columnId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmBoardKeys.board(variables.projectId) })
    },
  })
}
