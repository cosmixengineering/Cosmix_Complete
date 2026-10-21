/**
 * SignalR Hook for Real-time Notifications
 *
 * Provides a reusable hook for establishing and managing SignalR connections.
 * Features:
 * - Automatic connection with JWT authentication
 * - Auto-reconnect on disconnect
 * - Connection state management
 * - Typed event handlers
 */
import { useEffect, useState, useCallback, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { getAccessToken } from '@/services/tokenStorage'
import { useTabVisibility } from '@/hooks/useTabVisibility'
import type { Notification } from '@/types'

/** Disconnect SignalR after tab has been hidden for this many milliseconds */
const BACKGROUND_DISCONNECT_MS = 30_000

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

interface UseSignalROptions {
  /** Whether to automatically connect on mount */
  autoConnect?: boolean
  /** Called when a new notification is received */
  onNotification?: (notification: Notification) => void
  /** Called when unread count is updated */
  onUnreadCountUpdate?: (count: number) => void
  /** Called when connection state changes */
  onConnectionChange?: (state: ConnectionState) => void
  /** Called when the server is shutting down */
  onServerShutdown?: (reason: string) => void
  /** Called when the server has recovered from a restart */
  onServerRecovery?: () => void
}

interface UseSignalRReturn {
  /** Current connection state */
  connectionState: ConnectionState
  /** Manually start the connection */
  connect: () => Promise<void>
  /** Manually stop the connection */
  disconnect: () => Promise<void>
  /** Whether currently connected */
  isConnected: boolean
}

/**
 * Hook for managing SignalR notification connection
 *
 * @example
 * ```tsx
 * const { connectionState, isConnected } = useSignalR({
 *   autoConnect: true,
 *   onNotification: (notification) => {
 *     console.log('New notification:', notification)
 *   },
 *   onUnreadCountUpdate: (count) => {
 *     setUnreadCount(count)
 *   },
 * })
 * ```
 */
export const useSignalR = (options: UseSignalROptions = {}): UseSignalRReturn => {
  const {
    autoConnect = true,
    onNotification,
    onUnreadCountUpdate,
    onConnectionChange,
    onServerShutdown,
    onServerRecovery,
  } = options

  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected')
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const mountedRef = useRef(true)

  // Update connection state and notify
  const updateState = useCallback((state: ConnectionState) => {
    if (mountedRef.current) {
      setConnectionState(state)
      onConnectionChange?.(state)
    }
  }, [onConnectionChange])

  // Build the SignalR connection
  const buildConnection = useCallback(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', {
        accessTokenFactory: () => getAccessToken() || '',
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0, 2s, 4s, 8s, 16s, then 30s max
          if (retryContext.previousRetryCount >= 5) {
            return 30000
          }
          return Math.min(Math.pow(2, retryContext.previousRetryCount) * 1000, 30000)
        },
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    // Set up event handlers
    connection.on('ReceiveNotification', (notification: Notification) => {
      if (mountedRef.current && onNotification) {
        onNotification(notification)
      }
    })

    connection.on('UpdateUnreadCount', (count: number) => {
      if (mountedRef.current && onUnreadCountUpdate) {
        onUnreadCountUpdate(count)
      }
    })

    connection.on('ReceiveServerShutdown', (reason: string) => {
      if (mountedRef.current && onServerShutdown) {
        onServerShutdown(reason)
      }
    })

    connection.on('ReceiveServerRecovery', () => {
      if (mountedRef.current && onServerRecovery) {
        onServerRecovery()
      }
    })

    // Handle connection state changes
    connection.onreconnecting(() => {
      updateState('reconnecting')
    })

    connection.onreconnected(() => {
      updateState('connected')
    })

    connection.onclose(() => {
      updateState('disconnected')
    })

    return connection
  }, [onNotification, onUnreadCountUpdate, onServerShutdown, onServerRecovery, updateState])

  // Connect to SignalR hub
  const connect = useCallback(async () => {
    // Don't connect if already connecting/connected
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected ||
        connectionRef.current?.state === signalR.HubConnectionState.Connecting) {
      return
    }

    // Build new connection if needed
    if (!connectionRef.current) {
      connectionRef.current = buildConnection()
    }

    try {
      updateState('connecting')
      await connectionRef.current.start()
      updateState('connected')
    } catch {
      // Connection will be retried automatically
      updateState('disconnected')
      // Retry after 5 seconds
      if (mountedRef.current) {
        setTimeout(() => {
          if (mountedRef.current) {
            connect()
          }
        }, 5000)
      }
    }
  }, [buildConnection, updateState])

  // Disconnect from SignalR hub
  const disconnect = useCallback(async () => {
    if (connectionRef.current) {
      try {
        await connectionRef.current.stop()
      } catch {
        // Disconnect error is non-critical
      }
      connectionRef.current = null
      updateState('disconnected')
    }
  }, [updateState])

  // Auto-connect on mount
  useEffect(() => {
    mountedRef.current = true

    if (autoConnect) {
      // Small delay to ensure auth token is ready
      const timer = setTimeout(() => {
        if (mountedRef.current && getAccessToken()) {
          connect()
        }
      }, 100)
      return () => clearTimeout(timer)
    }
  }, [autoConnect, connect])

  // Disconnect when tab is backgrounded for too long, reconnect when visible
  const { isVisible, wasHiddenFor } = useTabVisibility()
  const disconnectedByVisibilityRef = useRef(false)

  useEffect(() => {
    if (!autoConnect) return

    if (!isVisible) {
      // Tab hidden — schedule disconnect after threshold
      const timer = setTimeout(() => {
        if (!document.hidden) return // Tab became visible during timeout
        if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
          disconnectedByVisibilityRef.current = true
          disconnect()
        }
      }, BACKGROUND_DISCONNECT_MS)
      return () => clearTimeout(timer)
    }

    // Tab became visible — reconnect if we disconnected due to visibility
    if (isVisible && disconnectedByVisibilityRef.current) {
      disconnectedByVisibilityRef.current = false
      if (getAccessToken()) {
        connect()
      }
    }

    // Also reconnect if the tab was hidden longer than the threshold
    // (handles case where tab was hidden before hook mounted)
    if (isVisible && wasHiddenFor && wasHiddenFor > BACKGROUND_DISCONNECT_MS) {
      if (connectionRef.current?.state !== signalR.HubConnectionState.Connected &&
          connectionRef.current?.state !== signalR.HubConnectionState.Connecting &&
          getAccessToken()) {
        connect()
      }
    }
  }, [isVisible, wasHiddenFor, autoConnect, connect, disconnect])

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      mountedRef.current = false
      if (connectionRef.current) {
        connectionRef.current.stop()
        connectionRef.current = null
      }
    }
  }, [])

  return {
    connectionState,
    connect,
    disconnect,
    isConnected: connectionState === 'connected',
  }
}
