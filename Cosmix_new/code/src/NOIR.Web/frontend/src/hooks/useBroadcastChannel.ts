import { useEffect, useRef, useCallback } from 'react'

/**
 * Generic typed cross-tab messaging hook using BroadcastChannel API.
 * Falls back to localStorage storage events for browsers without BroadcastChannel support.
 *
 * @param channelName - Unique name for the broadcast channel
 * @param onMessage - Callback invoked when a message is received from another tab
 * @returns Object with postMessage function to broadcast to other tabs
 *
 * @example
 * ```tsx
 * const { postMessage } = useBroadcastChannel<{ type: 'logout' }>('noir-auth', (data) => {
 *   if (data.type === 'logout') {
 *     // Handle logout from another tab
 *   }
 * })
 *
 * // In logout handler:
 * postMessage({ type: 'logout' })
 * ```
 */
export const useBroadcastChannel = <T>(
  channelName: string,
  onMessage: (data: T) => void,
): { postMessage: (data: T) => void } => {
  const onMessageRef = useRef(onMessage)
  const channelRef = useRef<BroadcastChannel | null>(null)

  // Keep callback ref fresh to avoid stale closures
  useEffect(() => {
    onMessageRef.current = onMessage
  }, [onMessage])

  useEffect(() => {
    if (typeof window === 'undefined') return

    const hasBroadcastChannel = typeof BroadcastChannel !== 'undefined'

    // Use BroadcastChannel if supported
    if (hasBroadcastChannel) {
      const channel = new BroadcastChannel(channelName)
      channelRef.current = channel

      channel.onmessage = (event: MessageEvent<T>) => {
        onMessageRef.current(event.data)
      }

      return () => {
        channel.close()
        channelRef.current = null
      }
    }

    // Fallback: use storage events for cross-tab communication
    const storageKey = `__broadcast_${channelName}`

    const handleStorage = (event: StorageEvent) => {
      if (event.key === storageKey && event.newValue) {
        try {
          const data = JSON.parse(event.newValue) as T
          onMessageRef.current(data)
        } catch {
          // Ignore malformed messages
        }
      }
    }

    window.addEventListener('storage', handleStorage)

    return () => {
      window.removeEventListener('storage', handleStorage)
    }
  }, [channelName])

  const postMessage = useCallback(
    (data: T) => {
      if (typeof window === 'undefined') return

      // Use BroadcastChannel if available
      if (channelRef.current) {
        channelRef.current.postMessage(data)
        return
      }

      // Fallback: write to localStorage to trigger storage events in other tabs
      const storageKey = `__broadcast_${channelName}`
      try {
        localStorage.setItem(storageKey, JSON.stringify(data))
        // Remove immediately — the storage event fires in other tabs on setItem
        localStorage.removeItem(storageKey)
      } catch {
        // Storage unavailable
      }
    },
    [channelName],
  )

  return { postMessage }
}
