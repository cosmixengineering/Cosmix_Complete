import { useState, useEffect, useRef, useCallback } from 'react'

interface TabVisibility {
  /** Whether the tab is currently visible */
  isVisible: boolean
  /** Milliseconds the tab was hidden for (null if currently visible or never hidden) */
  wasHiddenFor: number | null
}

/**
 * Hook to track browser tab visibility state.
 * Useful for pausing expensive operations (SignalR, polling) when the tab is in the background.
 *
 * @returns Object with isVisible boolean and wasHiddenFor duration in milliseconds
 *
 * @example
 * ```tsx
 * const { isVisible, wasHiddenFor } = useTabVisibility()
 *
 * useEffect(() => {
 *   if (isVisible && wasHiddenFor && wasHiddenFor > 30000) {
 *     // Tab was hidden for more than 30 seconds, reconnect
 *     reconnect()
 *   }
 * }, [isVisible, wasHiddenFor])
 * ```
 */
export const useTabVisibility = (): TabVisibility => {
  const [isVisible, setIsVisible] = useState(() =>
    typeof document !== 'undefined' ? document.visibilityState === 'visible' : true,
  )
  const [wasHiddenFor, setWasHiddenFor] = useState<number | null>(null)
  const hiddenAtRef = useRef<number | null>(null)

  const handleVisibilityChange = useCallback(() => {
    if (document.visibilityState === 'hidden') {
      hiddenAtRef.current = Date.now()
      setIsVisible(false)
      setWasHiddenFor(null)
    } else {
      const hiddenAt = hiddenAtRef.current
      const duration = hiddenAt !== null ? Date.now() - hiddenAt : null
      hiddenAtRef.current = null
      setWasHiddenFor(duration)
      setIsVisible(true)
    }
  }, [])

  useEffect(() => {
    if (typeof document === 'undefined') return

    document.addEventListener('visibilitychange', handleVisibilityChange)

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange)
    }
  }, [handleVisibilityChange])

  return { isVisible, wasHiddenFor }
}
