/**
 * Generic SSE (Server-Sent Events) Hook
 *
 * Provides a lightweight one-way server-to-client push mechanism.
 * Features:
 * - Automatic connection with JWT authentication via query string
 * - Auto-reconnect with exponential backoff (1s -> 2s -> 4s -> ... -> 30s max)
 * - Heartbeat filtering (server sends heartbeats every 20s)
 * - Connection state management
 * - Cleanup on unmount
 */
import { useEffect, useState, useRef, useCallback } from 'react'
import { getAccessToken } from '@/services/tokenStorage'

/** Maximum delay between reconnect attempts (ms) */
const MAX_RETRY_DELAY_MS = 30_000
/** Default maximum number of reconnect attempts before giving up */
const DEFAULT_MAX_RETRIES = 10

export type SseConnectionState = 'disconnected' | 'connecting' | 'connected' | 'error'

export interface UseSseOptions<T> {
  /** Called when a non-heartbeat message is received with parsed data */
  onMessage?: (data: T, eventType: string) => void
  /** Called when the EventSource encounters an error */
  onError?: (error: Event) => void
  /** Maximum number of reconnect attempts (default: 10, set 0 for unlimited) */
  maxRetries?: number
}

export interface UseSseReturn<T> {
  /** Whether the SSE connection is active */
  isConnected: boolean
  /** Current connection state */
  connectionState: SseConnectionState
  /** The most recently received event data */
  lastEvent: T | null
  /** The most recently received event type */
  lastEventType: string | null
  /** Any connection error */
  error: Event | null
}

/**
 * Hook for subscribing to a Server-Sent Events stream.
 *
 * @param url - The SSE endpoint URL, or null to disable the connection.
 * @param options - Configuration options for message handling and reconnection.
 *
 * @example
 * ```tsx
 * const { isConnected, lastEvent } = useSse<ProgressPayload>(
 *   jobId ? `/api/sse/channels/job-${jobId}` : null,
 *   {
 *     onMessage: (data) => setProgress(data.progress),
 *     maxRetries: 5,
 *   }
 * )
 * ```
 */
export const useSse = <T = unknown>(
  url: string | null,
  options?: UseSseOptions<T>,
): UseSseReturn<T> => {
  const [connectionState, setConnectionState] = useState<SseConnectionState>('disconnected')
  const [lastEvent, setLastEvent] = useState<T | null>(null)
  const [lastEventType, setLastEventType] = useState<string | null>(null)
  const [error, setError] = useState<Event | null>(null)

  const eventSourceRef = useRef<EventSource | null>(null)
  const retryCountRef = useRef(0)
  const retryTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const mountedRef = useRef(true)

  // Stable refs for callbacks to avoid reconnect loops
  const onMessageRef = useRef(options?.onMessage)
  const onErrorRef = useRef(options?.onError)
  onMessageRef.current = options?.onMessage
  onErrorRef.current = options?.onError

  const maxRetries = options?.maxRetries ?? DEFAULT_MAX_RETRIES

  const cleanup = useCallback(() => {
    if (retryTimerRef.current) {
      clearTimeout(retryTimerRef.current)
      retryTimerRef.current = null
    }
    if (eventSourceRef.current) {
      eventSourceRef.current.close()
      eventSourceRef.current = null
    }
  }, [])

  const connect = useCallback((targetUrl: string) => {
    cleanup()

    if (!mountedRef.current) return

    // Append JWT token as query parameter (EventSource cannot set headers)
    const token = getAccessToken()
    const separator = targetUrl.includes('?') ? '&' : '?'
    const authenticatedUrl = token
      ? `${targetUrl}${separator}access_token=${encodeURIComponent(token)}`
      : targetUrl

    setConnectionState('connecting')
    setError(null)

    const es = new EventSource(authenticatedUrl)
    eventSourceRef.current = es

    es.onopen = () => {
      if (!mountedRef.current) return
      setConnectionState('connected')
      retryCountRef.current = 0
    }

    // Default message handler (for events without a named type)
    es.onmessage = (event: MessageEvent) => {
      if (!mountedRef.current) return

      try {
        const parsed = JSON.parse(event.data) as T
        setLastEvent(parsed)
        setLastEventType('message')
        onMessageRef.current?.(parsed, 'message')
      } catch {
        // Data is not JSON, pass as-is
        setLastEvent(event.data as unknown as T)
        setLastEventType('message')
        onMessageRef.current?.(event.data as unknown as T, 'message')
      }
    }

    es.onerror = (err: Event) => {
      if (!mountedRef.current) return

      onErrorRef.current?.(err)

      // EventSource auto-reconnects on its own for some errors,
      // but if the connection is fully closed we handle retry ourselves
      if (es.readyState === EventSource.CLOSED) {
        setConnectionState('error')
        setError(err)
        cleanup()

        // Retry with exponential backoff
        if (maxRetries === 0 || retryCountRef.current < maxRetries) {
          const delay = Math.min(
            Math.pow(2, retryCountRef.current) * 1000,
            MAX_RETRY_DELAY_MS,
          )
          retryCountRef.current++
          retryTimerRef.current = setTimeout(() => {
            if (mountedRef.current) {
              connect(targetUrl)
            }
          }, delay)
        }
      }
    }
  }, [cleanup, maxRetries])

  useEffect(() => {
    mountedRef.current = true

    if (url) {
      retryCountRef.current = 0
      connect(url)
    } else {
      cleanup()
      setConnectionState('disconnected')
    }

    return () => {
      mountedRef.current = false
      cleanup()
    }
  }, [url, connect, cleanup])

  return {
    isConnected: connectionState === 'connected',
    connectionState,
    lastEvent,
    lastEventType,
    error,
  }
}
