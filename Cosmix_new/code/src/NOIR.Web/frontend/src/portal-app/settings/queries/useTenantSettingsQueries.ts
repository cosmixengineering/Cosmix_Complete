import { useQuery } from '@tanstack/react-query'
import {
  getBrandingSettings,
  getContactSettings,
  getRegionalSettings,
  getTenantSmtpSettings,
} from '@/services/tenantSettings'
import { tenantSettingsKeys } from './queryKeys'

const SETTINGS_STALE_TIME = 5 * 60_000

export const useBrandingSettingsQuery = () =>
  useQuery({
    queryKey: tenantSettingsKeys.branding(),
    queryFn: () => getBrandingSettings(),
    staleTime: SETTINGS_STALE_TIME,
  })

export const useContactSettingsQuery = () =>
  useQuery({
    queryKey: tenantSettingsKeys.contact(),
    queryFn: () => getContactSettings(),
    staleTime: SETTINGS_STALE_TIME,
  })

export const useRegionalSettingsQuery = () =>
  useQuery({
    queryKey: tenantSettingsKeys.regional(),
    queryFn: () => getRegionalSettings(),
    staleTime: SETTINGS_STALE_TIME,
  })

export const useTenantSmtpSettingsQuery = () =>
  useQuery({
    queryKey: tenantSettingsKeys.smtp(),
    queryFn: () => getTenantSmtpSettings(),
    staleTime: SETTINGS_STALE_TIME,
  })
