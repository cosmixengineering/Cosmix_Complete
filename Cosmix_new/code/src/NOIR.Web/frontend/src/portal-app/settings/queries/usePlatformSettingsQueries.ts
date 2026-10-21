import { useQuery } from '@tanstack/react-query'
import { getSmtpSettings } from '@/services/platformSettings'
import { getEmailTemplates, getEmailTemplate } from '@/services/emailTemplates'
import { getLegalPages, getLegalPageById } from '@/services/legalPages'
import { platformSettingsKeys } from './queryKeys'

const SETTINGS_STALE_TIME = 5 * 60_000

export const usePlatformSmtpSettingsQuery = () =>
  useQuery({
    queryKey: platformSettingsKeys.smtp(),
    queryFn: () => getSmtpSettings(),
    staleTime: SETTINGS_STALE_TIME,
  })

export const useEmailTemplatesQuery = (search?: string) =>
  useQuery({
    queryKey: [...platformSettingsKeys.emailTemplates(), search] as const,
    queryFn: () => getEmailTemplates(search),
    staleTime: SETTINGS_STALE_TIME,
  })

export const useEmailTemplateQuery = (id: string | undefined) =>
  useQuery({
    queryKey: platformSettingsKeys.emailTemplate(id!),
    queryFn: () => getEmailTemplate(id!),
    enabled: !!id,
    staleTime: SETTINGS_STALE_TIME,
  })

export const useLegalPagesQuery = () =>
  useQuery({
    queryKey: platformSettingsKeys.legalPages(),
    queryFn: () => getLegalPages(),
    staleTime: SETTINGS_STALE_TIME,
  })

export const useLegalPageQuery = (id: string | undefined) =>
  useQuery({
    queryKey: platformSettingsKeys.legalPage(id!),
    queryFn: () => getLegalPageById(id!),
    enabled: !!id,
    staleTime: SETTINGS_STALE_TIME,
  })
