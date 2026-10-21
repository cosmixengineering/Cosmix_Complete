/**
 * Server Health Context
 *
 * Provides server lifecycle state (shutting down, recovering) to the app.
 * The NotificationProvider wires the callbacks into the SignalR connection.
 * The ServerRecoveryBanner consumes the state to show banners.
 */
import {
  createContext,
  useContext,
  type ReactNode,
} from 'react'
import { useServerHealth, type ServerHealthState } from '@/hooks/useServerHealth'

interface ServerHealthContextValue extends ServerHealthState {
  /** Callback to pass to useSignalR for ReceiveServerShutdown */
  handleServerShutdown: (reason: string) => void
  /** Callback to pass to useSignalR for ReceiveServerRecovery */
  handleServerRecovery: () => void
}

const ServerHealthContext = createContext<ServerHealthContextValue | undefined>(undefined)

export const ServerHealthProvider = ({ children }: { children: ReactNode }) => {
  const {
    isShuttingDown,
    isRecovering,
    shutdownReason,
    dismiss,
    handleServerShutdown,
    handleServerRecovery,
  } = useServerHealth()

  return (
    <ServerHealthContext.Provider
      value={{
        isShuttingDown,
        isRecovering,
        shutdownReason,
        dismiss,
        handleServerShutdown,
        handleServerRecovery,
      }}
    >
      {children}
    </ServerHealthContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export const useServerHealthContext = () => {
  const context = useContext(ServerHealthContext)
  if (!context) {
    throw new Error('useServerHealthContext must be used within ServerHealthProvider')
  }
  return context
}
