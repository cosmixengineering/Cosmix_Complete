export const developerLogKeys = {
  all: ['developerLogs'] as const,
  availableDates: () => [...developerLogKeys.all, 'availableDates'] as const,
  history: (date: string, params: Record<string, unknown>) => [...developerLogKeys.all, 'history', date, params] as const,
}

export const activityTimelineKeys = {
  all: ['activityTimeline'] as const,
  search: (params: Record<string, unknown>) => [...activityTimelineKeys.all, 'search', params] as const,
  details: (id: string) => [...activityTimelineKeys.all, 'detail', id] as const,
  pageContexts: () => [...activityTimelineKeys.all, 'pageContexts'] as const,
}
