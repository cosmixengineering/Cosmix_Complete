import { useQuery } from '@tanstack/react-query'
import { getPipelines } from '@/services/crm'
import { crmPipelineKeys } from './queryKeys'

export const usePipelinesQuery = () =>
  useQuery({
    queryKey: crmPipelineKeys.lists(),
    queryFn: () => getPipelines(),
  })
