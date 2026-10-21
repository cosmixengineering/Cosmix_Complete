import { useMutation, useQueryClient } from '@tanstack/react-query'
import { updatePreferences } from '@/services/notifications'
import { notificationKeys } from './queryKeys'
import type { NotificationPreference } from '@/types'

export const useUpdateNotificationPreferences = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (preferences: Pick<NotificationPreference, 'category' | 'inAppEnabled' | 'emailFrequency'>[]) =>
      updatePreferences({ preferences }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: notificationKeys.preferences() })
    },
  })
}
