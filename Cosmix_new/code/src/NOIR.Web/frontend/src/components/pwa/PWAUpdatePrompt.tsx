import { useTranslation } from 'react-i18next'
import { RefreshCw, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@uikit'
import { usePWA } from '@/hooks/usePWA'

/**
 * Toast-style notification shown when a new service worker version is available.
 * Offers "Update now" (reload) and "Dismiss" actions.
 */
export const PWAUpdatePrompt = () => {
  const { t } = useTranslation('common')
  const { needRefresh, offlineReady, updateServiceWorker, close } = usePWA()

  if (!needRefresh && !offlineReady) {
    return null
  }

  return (
    <div
      role="alert"
      className={cn(
        'fixed bottom-4 right-4 z-[100] flex items-center gap-3 rounded-lg border bg-background px-4 py-3 shadow-lg',
        'animate-in slide-in-from-bottom-4 duration-300',
        'max-w-sm',
      )}
    >
      {needRefresh && (
        <>
          <RefreshCw className="h-4 w-4 shrink-0 text-primary" />
          <p className="text-sm font-medium">{t('pwa.updateAvailable')}</p>
          <div className="flex items-center gap-1.5 ml-auto">
            <Button
              size="sm"
              className="cursor-pointer"
              onClick={() => updateServiceWorker(true)}
            >
              {t('pwa.updateNow')}
            </Button>
            <button
              type="button"
              onClick={close}
              className="rounded-sm p-1 opacity-70 hover:opacity-100 transition-opacity cursor-pointer"
              aria-label={t('pwa.dismiss')}
            >
              <X className="h-3.5 w-3.5" />
            </button>
          </div>
        </>
      )}

      {offlineReady && !needRefresh && (
        <>
          <p className="text-sm font-medium">{t('pwa.offlineReady')}</p>
          <button
            type="button"
            onClick={close}
            className="ml-auto rounded-sm p-1 opacity-70 hover:opacity-100 transition-opacity cursor-pointer"
            aria-label={t('pwa.dismiss')}
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </>
      )}
    </div>
  )
}
