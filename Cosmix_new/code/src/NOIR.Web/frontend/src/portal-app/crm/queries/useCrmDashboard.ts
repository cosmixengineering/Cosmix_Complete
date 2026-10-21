import { useQuery } from '@tanstack/react-query'
import { getCrmDashboard } from '@/services/crm'
import { crmDashboardKeys } from './queryKeys'

export const useCrmDashboardQuery = () =>
  useQuery({
    queryKey: crmDashboardKeys.all,
    queryFn: () => getCrmDashboard(),
  })
