import { useEffect, useRef } from 'react'
import { useRegisterSW } from 'virtual:pwa-register/react'

const UPDATE_CHECK_INTERVAL_MS = 3600000 // 1 hour

/**
 * Hook to manage PWA service worker lifecycle.
 *
 * - `needRefresh`: true when a new SW version is waiting to activate
 * - `offlineReady`: true when the app has been cached for offline use
 * - `updateServiceWorker()`: activates the waiting SW and reloads
 * - `close()`: dismisses the prompts without updating
 */
export const usePWA = () => {
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const {
    needRefresh: [needRefresh, setNeedRefresh],
    offlineReady: [offlineReady, setOfflineReady],
    updateServiceWorker,
  } = useRegisterSW({
    onRegisteredSW(_url, registration) {
      if (registration) {
        intervalRef.current = setInterval(() => {
          registration.update()
        }, UPDATE_CHECK_INTERVAL_MS)
      }
    },
  })

  // Cleanup interval on unmount
  useEffect(() => {
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [])

  const close = () => {
    setNeedRefresh(false)
    setOfflineReady(false)
  }

  return {
    needRefresh,
    offlineReady,
    updateServiceWorker,
    close,
  }
}
