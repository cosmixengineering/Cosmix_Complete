export const authKeys = {
  all: ['auth'] as const,
  permissions: () => [...authKeys.all, 'permissions'] as const,
  sessions: () => [...authKeys.all, 'sessions'] as const,
}

export const publicKeys = {
  all: ['public'] as const,
  legalPage: (slug: string) => [...publicKeys.all, 'legalPage', slug] as const,
}
