import type { GetProjectsParams } from '@/types/pm'

export const pmProjectKeys = {
  all: ['pm-projects'] as const,
  lists: () => [...pmProjectKeys.all, 'list'] as const,
  list: (params: GetProjectsParams) => [...pmProjectKeys.lists(), params] as const,
  details: () => [...pmProjectKeys.all, 'detail'] as const,
  detail: (id: string) => [...pmProjectKeys.details(), id] as const,
}

export const pmBoardKeys = {
  all: ['pm-boards'] as const,
  board: (projectId: string) => [...pmBoardKeys.all, projectId] as const,
}

export const pmTaskKeys = {
  all: ['pm-tasks'] as const,
  details: () => [...pmTaskKeys.all, 'detail'] as const,
  detail: (id: string) => [...pmTaskKeys.details(), id] as const,
}

export const pmLabelKeys = {
  all: ['pm-labels'] as const,
  byProject: (projectId: string) => [...pmLabelKeys.all, projectId] as const,
}
