/**
 * Server Health Hook
 *
 * Listens for SignalR server lifecycle events (shutdown/recovery)
 * and provides state for the ServerRecoveryBanner component.
 * Invalidates React Query cache on server recovery.
 */
import { useState, useCallback, useRef, useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'

/** Duration to show the recovery banner before auto-dismissing (ms) */
const RECOVERY_DISPLAY_MS = 3_000

export interface ServerHealthState {
  /** Whether the server is shutting down */
  isShuttingDown: boolean
  /** Whether the server just recovered from a restart */
  isRecovering: boolean
  /** Reason for shutdown (if any) */
  shutdownReason?: string
  /** Dismiss the banner manually */
  dismiss: () => void
}

/**
 * Hook that manages server health state for shutdown/recovery banners.
 *
 * Returns callbacks to wire into useSignalR, plus the current health state.
 * On recovery, invalidates all React Query caches to refresh stale data.
 */
export const useServerHealth = (): ServerHealthState & {
  handleServerShutdown: (reason: string) => void
  handleServerRecovery: () => void
} => {
  const queryClient = useQueryClient()
  const [isShuttingDown, setIsShuttingDown] = useState(false)
  const [isRecovering, setIsRecovering] = useState(false)
  const [shutdownReason, setShutdownReason] = useState<string | undefined>()
  const recoveryTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const dismiss = useCallback(() => {
    setIsShuttingDown(false)
    setIsRecovering(false)
    setShutdownReason(undefined)
    if (recoveryTimerRef.current) {
      clearTimeout(recoveryTimerRef.current)
      recoveryTimerRef.current = null
    }
  }, [])

  const handleServerShutdown = useCallback((reason: string) => {
    setIsShuttingDown(true)
    setShutdownReason(reason)
  }, [])

  const handleServerRecovery = useCallback(() => {
    setIsShuttingDown(false)
    setIsRecovering(true)
    setShutdownReason(undefined)

    // Invalidate all cached queries so the UI refreshes with fresh data
    queryClient.invalidateQueries({ predicate: () => true })

    // Auto-dismiss after display duration
    recoveryTimerRef.current = setTimeout(() => {
      setIsRecovering(false)
    }, RECOVERY_DISPLAY_MS)
  }, [queryClient])

  // Cleanup timer on unmount
  useEffect(() => {
    return () => {
      if (recoveryTimerRef.current) {
        clearTimeout(recoveryTimerRef.current)
      }
    }
  }, [])

  return {
    isShuttingDown,
    isRecovering,
    shutdownReason,
    dismiss,
    handleServerShutdown,
    handleServerRecovery,
  }
}
