import { useMutation, useQueryClient } from '@tanstack/react-query'
import { addTaskComment, deleteTaskComment } from '@/services/pm'
import type { AddTaskCommentRequest } from '@/types/pm'
import { pmTaskKeys } from './queryKeys'

export const useAddComment = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ taskId, request }: { taskId: string; request: AddTaskCommentRequest }) =>
      addTaskComment(taskId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmTaskKeys.detail(variables.taskId) })
    },
  })
}

export const useDeleteComment = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ taskId, commentId }: { taskId: string; commentId: string }) =>
      deleteTaskComment(taskId, commentId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmTaskKeys.detail(variables.taskId) })
    },
  })
}
