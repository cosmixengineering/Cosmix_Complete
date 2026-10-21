import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  updateSmtpSettings,
  testSmtpConnection,
  type UpdateSmtpSettingsRequest,
  type TestSmtpRequest,
} from '@/services/platformSettings'
import {
  updateEmailTemplate,
  toggleEmailTemplateActive,
  revertToPlatformDefault,
  type UpdateEmailTemplateRequest,
} from '@/services/emailTemplates'
import {
  updateLegalPage,
  revertLegalPageToDefault,
  type UpdateLegalPageRequest,
} from '@/services/legalPages'
import { platformSettingsKeys } from './queryKeys'

export const useUpdatePlatformSmtpSettings = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateSmtpSettingsRequest) => updateSmtpSettings(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: platformSettingsKeys.smtp() })
    },
  })
}

export const useTestPlatformSmtpConnection = () =>
  useMutation({
    mutationFn: (request: TestSmtpRequest) => testSmtpConnection(request),
  })

export const useUpdateEmailTemplate = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateEmailTemplateRequest }) =>
      updateEmailTemplate(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: platformSettingsKeys.all })
    },
  })
}

export const useToggleEmailTemplateActive = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      toggleEmailTemplateActive(id, isActive),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: platformSettingsKeys.all })
    },
  })
}

export const useRevertEmailTemplate = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => revertToPlatformDefault(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: platformSettingsKeys.all })
    },
  })
}

export const useUpdateLegalPage = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateLegalPageRequest }) =>
      updateLegalPage(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: platformSettingsKeys.all })
    },
  })
}

export const useRevertLegalPage = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => revertLegalPageToDefault(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: platformSettingsKeys.all })
    },
  })
}
