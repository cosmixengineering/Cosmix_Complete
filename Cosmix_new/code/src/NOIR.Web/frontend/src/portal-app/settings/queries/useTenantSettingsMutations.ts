import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  updateBrandingSettings,
  updateContactSettings,
  updateRegionalSettings,
  updateTenantSmtpSettings,
  revertTenantSmtpSettings,
  testTenantSmtpConnection,
  type UpdateBrandingSettingsRequest,
  type UpdateContactSettingsRequest,
  type UpdateRegionalSettingsRequest,
  type UpdateTenantSmtpSettingsRequest,
  type TestTenantSmtpRequest,
} from '@/services/tenantSettings'
import { tenantSettingsKeys } from './queryKeys'

export const useUpdateBrandingSettings = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateBrandingSettingsRequest) => updateBrandingSettings(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tenantSettingsKeys.branding() })
    },
  })
}

export const useUpdateContactSettings = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateContactSettingsRequest) => updateContactSettings(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tenantSettingsKeys.contact() })
    },
  })
}

export const useUpdateRegionalSettings = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateRegionalSettingsRequest) => updateRegionalSettings(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tenantSettingsKeys.regional() })
    },
  })
}

export const useUpdateTenantSmtpSettings = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateTenantSmtpSettingsRequest) => updateTenantSmtpSettings(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tenantSettingsKeys.smtp() })
    },
  })
}

export const useRevertTenantSmtpSettings = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => revertTenantSmtpSettings(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tenantSettingsKeys.smtp() })
    },
  })
}

export const useTestTenantSmtpConnection = () =>
  useMutation({
    mutationFn: (request: TestTenantSmtpRequest) => testTenantSmtpConnection(request),
  })
