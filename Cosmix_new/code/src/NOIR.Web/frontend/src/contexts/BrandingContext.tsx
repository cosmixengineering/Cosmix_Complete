/**
 * Branding Context
 *
 * Provides tenant branding settings (logo, favicon, colors, dark mode default)
 * and applies them to the UI dynamically.
 *
 * Features:
 * - Fetches branding settings when user is authenticated
 * - Applies CSS variable overrides for primary/secondary colors
 * - Updates favicon dynamically
 * - Exposes dark mode default for ThemeContext integration
 */
import { createContext, useContext, useEffect, useCallback, useRef, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useAuthContext } from './AuthContext'
import { useTheme } from './ThemeContext'
import { getBrandingSettings, type BrandingSettingsDto } from '@/services/tenantSettings'
import { tenantSettingsKeys } from '@/portal-app/settings/queries/queryKeys'

// Fallback defaults when no tenant branding is configured
const DEFAULT_FAVICON = '/favicon.svg'
const DEFAULT_PRIMARY_COLOR = '#4338ca'
const DEFAULT_SECONDARY_COLOR = '#6366f1'

interface BrandingContextType {
  /** Current branding settings */
  branding: BrandingSettingsDto | null
  /** Whether branding is being loaded */
  loading: boolean
  /** Tenant's dark mode default preference */
  tenantDarkModeDefault: boolean
  /** Effective primary color (tenant override or default) */
  primaryColor: string
  /** Effective secondary color (tenant override or default) */
  secondaryColor: string
  /** Reload branding settings (call after save) */
  reloadBranding: () => Promise<void>
}

const BrandingContext = createContext<BrandingContextType | undefined>(undefined)

/**
 * Calculate perceived brightness of a hex color (0-255)
 * Uses standard luminance formula for perceived brightness
 */
const getPerceivedBrightness = (hex: string): number => {
  // Remove # if present
  const color = hex.replace('#', '')
  const r = parseInt(color.slice(0, 2), 16)
  const g = parseInt(color.slice(2, 4), 16)
  const b = parseInt(color.slice(4, 6), 16)
  // Standard perceived brightness formula
  return (r * 299 + g * 587 + b * 114) / 1000
}

/**
 * Get contrasting foreground color (black or white)
 */
const getContrastForeground = (hex: string): string => {
  return getPerceivedBrightness(hex) > 128 ? '#171717' : '#fafafa'
}

/**
 * Apply branding CSS variables to document root
 */
const applyBrandingColors = (primaryColor: string | null, secondaryColor: string | null) => {
  const root = document.documentElement

  if (primaryColor) {
    const foreground = getContrastForeground(primaryColor)
    root.style.setProperty('--primary', primaryColor)
    root.style.setProperty('--primary-foreground', foreground)
    root.style.setProperty('--ring', primaryColor)
    root.style.setProperty('--sidebar-primary', primaryColor)
    root.style.setProperty('--sidebar-primary-foreground', foreground)
  } else {
    // Remove overrides to use default theme
    root.style.removeProperty('--primary')
    root.style.removeProperty('--primary-foreground')
    root.style.removeProperty('--ring')
    root.style.removeProperty('--sidebar-primary')
    root.style.removeProperty('--sidebar-primary-foreground')
  }

  if (secondaryColor) {
    // Store secondary for explicit use in UI (e.g., gradients, accents)
    root.style.setProperty('--brand-secondary', secondaryColor)
    root.style.setProperty('--brand-secondary-foreground', getContrastForeground(secondaryColor))
  } else {
    root.style.removeProperty('--brand-secondary')
    root.style.removeProperty('--brand-secondary-foreground')
  }
}

/**
 * Update favicon dynamically
 */
const updateFavicon = (faviconUrl: string | null) => {
  const existingLink = document.querySelector("link[rel*='icon']") as HTMLLinkElement | null

  if (faviconUrl) {
    if (existingLink) {
      existingLink.href = faviconUrl
    } else {
      const link = document.createElement('link')
      link.rel = 'icon'
      link.href = faviconUrl
      document.head.appendChild(link)
    }
  } else if (existingLink) {
    // Reset to default favicon
    existingLink.href = DEFAULT_FAVICON
  }
}

interface BrandingProviderProps {
  children: ReactNode
}

export const BrandingProvider = ({ children }: BrandingProviderProps) => {
  const { isAuthenticated, user } = useAuthContext()
  const { setTheme, hasUserPreference } = useTheme()
  // Track if we've already applied the tenant default to avoid loops
  const appliedTenantDefaultRef = useRef(false)
  // Use refs for callback values that shouldn't trigger re-creation of effects
  const setThemeRef = useRef(setTheme)
  const hasUserPreferenceRef = useRef(hasUserPreference)

  // Keep refs in sync
  useEffect(() => { setThemeRef.current = setTheme }, [setTheme])
  useEffect(() => { hasUserPreferenceRef.current = hasUserPreference }, [hasUserPreference])

  // Platform Admin has tenantId = null — skip tenant-scoped API calls
  const tenantId = user?.tenantId

  const { data: brandingData, isLoading: loading, refetch } = useQuery({
    queryKey: tenantSettingsKeys.branding(),
    queryFn: () => getBrandingSettings(),
    enabled: isAuthenticated && !!tenantId,
    staleTime: 5 * 60_000,
  })

  // Apply branding side effects when data changes
  useEffect(() => {
    if (brandingData) {
      applyBrandingColors(brandingData.primaryColor, brandingData.secondaryColor)
      updateFavicon(brandingData.faviconUrl)

      // Apply tenant dark mode default only if user hasn't set their own preference
      // and we haven't already applied it in this session
      if (!hasUserPreferenceRef.current && !appliedTenantDefaultRef.current && brandingData.darkModeDefault) {
        setThemeRef.current('dark')
        appliedTenantDefaultRef.current = true
      }
    }
  }, [brandingData])

  // Clear branding when auth/tenant changes
  useEffect(() => {
    if (!isAuthenticated || !tenantId) {
      applyBrandingColors(null, null)
      updateFavicon(null)
      appliedTenantDefaultRef.current = false
    }
  }, [isAuthenticated, tenantId])

  const reloadBranding = useCallback(async () => {
    await refetch()
  }, [refetch])

  const branding = brandingData ?? null

  const tenantDarkModeDefault = branding?.darkModeDefault ?? false
  const primaryColor = branding?.primaryColor ?? DEFAULT_PRIMARY_COLOR
  const secondaryColor = branding?.secondaryColor ?? DEFAULT_SECONDARY_COLOR

  return (
    <BrandingContext.Provider
      value={{
        branding,
        loading,
        tenantDarkModeDefault,
        primaryColor,
        secondaryColor,
        reloadBranding,
      }}
    >
      {children}
    </BrandingContext.Provider>
  )
}

export const useBranding = () => {
  const context = useContext(BrandingContext)
  if (context === undefined) {
    throw new Error('useBranding must be used within a BrandingProvider')
  }
  return context
}

/**
 * Optional hook that doesn't throw if used outside provider
 * Useful for components that may or may not have branding context
 */
export const useBrandingOptional = () => {
  return useContext(BrandingContext)
}
