/**
 * Server Recovery Banner
 *
 * Displays a top-of-page banner during server shutdown/recovery:
 * - Amber banner when server is shutting down
 * - Green banner when server has recovered
 * - Auto-dismisses after recovery animation
 * - Manually dismissible via close button
 */
import { useTranslation } from 'react-i18next'
import { Loader2, CheckCircle2, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useServerHealthContext } from '@/contexts/ServerHealthContext'

export const ServerRecoveryBanner = () => {
  const { t } = useTranslation('common')
  const { isShuttingDown, isRecovering, dismiss } = useServerHealthContext()

  if (!isShuttingDown && !isRecovering) {
    return null
  }

  return (
    <div
      role="alert"
      className={cn(
        'fixed top-0 left-0 right-0 z-[100] flex items-center justify-center gap-2 px-4 py-2.5 text-sm font-medium shadow-md transition-all duration-300 animate-in slide-in-from-top',
        isShuttingDown && 'bg-amber-50 text-amber-800 border-b border-amber-200 dark:bg-amber-950 dark:text-amber-200 dark:border-amber-800',
        isRecovering && 'bg-green-50 text-green-800 border-b border-green-200 dark:bg-green-950 dark:text-green-200 dark:border-green-800',
      )}
    >
      {isShuttingDown && (
        <>
          <Loader2 className="h-4 w-4 animate-spin" />
          <span>{t('serverHealth.shuttingDown')}</span>
        </>
      )}
      {isRecovering && (
        <>
          <CheckCircle2 className="h-4 w-4" />
          <span>{t('serverHealth.recovering')}</span>
        </>
      )}
      <button
        type="button"
        onClick={dismiss}
        className="ml-2 rounded-sm p-0.5 opacity-70 hover:opacity-100 transition-opacity cursor-pointer"
        aria-label={t('buttons.dismiss')}
      >
        <X className="h-3.5 w-3.5" />
      </button>
    </div>
  )
}
