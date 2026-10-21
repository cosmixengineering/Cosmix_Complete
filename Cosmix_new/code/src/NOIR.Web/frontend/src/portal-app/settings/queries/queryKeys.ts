export const paymentGatewayKeys = {
  all: ['paymentGateways'] as const,
  gateways: () => [...paymentGatewayKeys.all, 'gateways'] as const,
  schemas: () => [...paymentGatewayKeys.all, 'schemas'] as const,
}

export const tenantSettingsKeys = {
  all: ['tenantSettings'] as const,
  branding: () => [...tenantSettingsKeys.all, 'branding'] as const,
  contact: () => [...tenantSettingsKeys.all, 'contact'] as const,
  regional: () => [...tenantSettingsKeys.all, 'regional'] as const,
  smtp: () => [...tenantSettingsKeys.all, 'smtp'] as const,
}

export const platformSettingsKeys = {
  all: ['platformSettings'] as const,
  smtp: () => [...platformSettingsKeys.all, 'smtp'] as const,
  emailTemplates: () => [...platformSettingsKeys.all, 'emailTemplates'] as const,
  emailTemplate: (id: string) => [...platformSettingsKeys.all, 'emailTemplate', id] as const,
  legalPages: () => [...platformSettingsKeys.all, 'legalPages'] as const,
  legalPage: (id: string) => [...platformSettingsKeys.all, 'legalPage', id] as const,
}
