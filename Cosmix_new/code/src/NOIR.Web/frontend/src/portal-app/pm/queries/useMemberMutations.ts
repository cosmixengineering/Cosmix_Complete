import { useMutation, useQueryClient } from '@tanstack/react-query'
import { addProjectMember, removeProjectMember, changeProjectMemberRole } from '@/services/pm'
import type { AddProjectMemberRequest, ProjectMemberRole } from '@/types/pm'
import { pmProjectKeys } from './queryKeys'

export const useAddMember = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: AddProjectMemberRequest }) =>
      addProjectMember(projectId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.detail(variables.projectId) })
    },
  })
}

export const useRemoveMember = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, memberId }: { projectId: string; memberId: string }) =>
      removeProjectMember(projectId, memberId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.detail(variables.projectId) })
    },
  })
}

export const useChangeMemberRole = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, memberId, role }: { projectId: string; memberId: string; role: ProjectMemberRole }) =>
      changeProjectMemberRole(projectId, memberId, role),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.detail(variables.projectId) })
    },
  })
}
