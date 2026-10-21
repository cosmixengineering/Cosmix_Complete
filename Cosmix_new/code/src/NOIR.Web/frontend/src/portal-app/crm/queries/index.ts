export { crmContactKeys, crmCompanyKeys, crmLeadKeys, crmPipelineKeys, crmActivityKeys, crmDashboardKeys } from './queryKeys'

export { useContactsQuery, useContactQuery } from './useContactQueries'
export { useCreateContact, useUpdateContact, useDeleteContact } from './useContactMutations'

export { useCompaniesQuery, useCompanyQuery } from './useCompanyQueries'
export { useCreateCompany, useUpdateCompany, useDeleteCompany } from './useCompanyMutations'

export { useLeadsQuery, useLeadQuery, usePipelineViewQuery } from './useLeadQueries'
export { useCreateLead, useUpdateLead, useMoveLeadStage, useWinLead, useLoseLead, useReopenLead } from './useLeadMutations'

export { usePipelinesQuery } from './usePipelineQueries'
export { useCreatePipeline, useUpdatePipeline, useDeletePipeline } from './usePipelineMutations'

export { useActivitiesQuery } from './useActivityQueries'
export { useCreateActivity, useUpdateActivity, useDeleteActivity } from './useActivityMutations'

export { useCrmDashboardQuery } from './useCrmDashboard'
