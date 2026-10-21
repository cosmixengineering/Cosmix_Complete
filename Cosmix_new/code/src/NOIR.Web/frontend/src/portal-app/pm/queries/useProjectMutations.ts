import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createProject, updateProject, archiveProject, deleteProject } from '@/services/pm'
import type { CreateProjectRequest, UpdateProjectRequest } from '@/types/pm'
import { pmProjectKeys } from './queryKeys'

export const useCreateProject = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateProjectRequest) => createProject(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.all })
    },
  })
}

export const useUpdateProject = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateProjectRequest }) => updateProject(id, request),
    onSuccess: (data) => {
      queryClient.setQueryData(pmProjectKeys.detail(data.id), data)
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.lists() })
    },
  })
}

export const useArchiveProject = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => archiveProject(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.all })
    },
  })
}

export const useDeleteProject = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteProject(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: pmProjectKeys.all })
    },
  })
}
