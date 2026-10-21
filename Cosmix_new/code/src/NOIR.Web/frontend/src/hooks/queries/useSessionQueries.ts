import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getActiveSessions, revokeSession } from '@/services/auth'
import { authKeys } from './queryKeys'

export const useActiveSessionsQuery = () =>
  useQuery({
    queryKey: authKeys.sessions(),
    queryFn: () => getActiveSessions(),
  })

export const useRevokeSession = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (sessionId: string) => revokeSession(sessionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: authKeys.sessions() })
    },
  })
}
