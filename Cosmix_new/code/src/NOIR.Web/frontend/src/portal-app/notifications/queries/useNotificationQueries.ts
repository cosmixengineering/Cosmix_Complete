import { useQuery } from '@tanstack/react-query'
import { getPreferences } from '@/services/notifications'
import { notificationKeys } from './queryKeys'

export const useNotificationPreferencesQuery = () =>
  useQuery({
    queryKey: notificationKeys.preferences(),
    queryFn: () => getPreferences(),
  })
