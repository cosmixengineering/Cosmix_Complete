import { useState, useEffect, useCallback, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Download, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@uikit'

const DISMISS_STORAGE_KEY = 'noir-pwa-dismiss'
const SHOW_DELAY_MS = 30000 // 30 seconds after page load
const COOLDOWN_DAYS = 7

interface BeforeInstallPromptEvent extends Event {
  readonly platforms: string[]
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>
  prompt: () => Promise<void>
}

/**
 * Bottom banner for PWA installation.
 *
 * - Listens for the `beforeinstallprompt` event
 * - Shows after 30 s of page load
 * - Dismissible with 7-day localStorage cooldown
 */
export const PWAInstallPrompt = () => {
  const { t } = useTranslation('common')
  const [showBanner, setShowBanner] = useState(false)
  const deferredPromptRef = useRef<BeforeInstallPromptEvent | null>(null)

  const isDismissed = useCallback(() => {
    const dismissed = localStorage.getItem(DISMISS_STORAGE_KEY)
    if (!dismissed) return false
    const dismissedAt = Number(dismissed)
    const cooldownMs = COOLDOWN_DAYS * 24 * 60 * 60 * 1000
    return Date.now() - dismissedAt < cooldownMs
  }, [])

  useEffect(() => {
    if (isDismissed()) return

    const handleBeforeInstall = (e: Event) => {
      e.preventDefault()
      deferredPromptRef.current = e as BeforeInstallPromptEvent
    }

    window.addEventListener('beforeinstallprompt', handleBeforeInstall)

    const timer = setTimeout(() => {
      if (deferredPromptRef.current) {
        setShowBanner(true)
      }
    }, SHOW_DELAY_MS)

    return () => {
      window.removeEventListener('beforeinstallprompt', handleBeforeInstall)
      clearTimeout(timer)
    }
  }, [isDismissed])

  const handleInstall = async () => {
    const prompt = deferredPromptRef.current
    if (!prompt) return

    await prompt.prompt()
    const { outcome } = await prompt.userChoice

    if (outcome === 'accepted') {
      deferredPromptRef.current = null
      setShowBanner(false)
    }
  }

  const handleDismiss = () => {
    localStorage.setItem(DISMISS_STORAGE_KEY, String(Date.now()))
    setShowBanner(false)
  }

  if (!showBanner) {
    return null
  }

  return (
    <div
      role="banner"
      className={cn(
        'fixed bottom-0 left-0 right-0 z-[99] border-t bg-background/95 backdrop-blur-sm',
        'animate-in slide-in-from-bottom duration-300',
      )}
    >
      <div className="mx-auto flex max-w-2xl items-center gap-3 px-4 py-3">
        <Download className="h-5 w-5 shrink-0 text-primary" />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium">{t('pwa.installTitle')}</p>
          <p className="text-xs text-muted-foreground">{t('pwa.installDescription')}</p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            size="sm"
            className="cursor-pointer"
            onClick={handleInstall}
          >
            {t('pwa.install')}
          </Button>
          <button
            type="button"
            onClick={handleDismiss}
            className="rounded-sm p-1 opacity-70 hover:opacity-100 transition-opacity cursor-pointer"
            aria-label={t('pwa.dismiss')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  )
}
