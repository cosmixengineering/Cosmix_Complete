import { useTranslation } from 'react-i18next'
import { Users, Target, Percent } from 'lucide-react'
import { EmptyState } from '@uikit'
import { useCrmDashboardQuery } from '@/portal-app/crm/queries/useCrmDashboard'
import { formatCurrency } from '@/lib/utils/currency'
import { DashboardSkeleton } from './widgets/DashboardSkeleton'
import { MetricCard } from './widgets/MetricCard'

export const CrmWidgetGroup = () => {
  const { t } = useTranslation('common')
  const { data, isLoading, isError } = useCrmDashboardQuery()

  if (isLoading) return <DashboardSkeleton count={3} />
  if (isError || !data) {
    return (
      <EmptyState
        icon={Users}
        title={t('dashboard.loadError')}
        description={t('dashboard.loadErrorDescription')}
        className="md:col-span-2 xl:col-span-3"
      />
    )
  }

  return (
    <>
      {/* CRM Overview — Total Contacts & Companies */}
      <MetricCard
        title={t('crm.dashboard.totalContacts')}
        value={data.totalContacts.toLocaleString()}
        subtitle={`${data.totalCompanies.toLocaleString()} ${t('crm.dashboard.totalCompanies').toLowerCase()}`}
        icon={Users}
        iconColor="text-blue-600"
        iconBg="bg-blue-100 dark:bg-blue-900/30"
      />

      {/* Active Pipeline — Active Deals & Pipeline Value */}
      <MetricCard
        title={t('crm.dashboard.activePipeline')}
        value={data.activeLeads.toLocaleString()}
        subtitle={`${t('crm.dashboard.totalPipelineValue')}: ${formatCurrency(data.totalPipelineValue)}`}
        icon={Target}
        iconColor="text-violet-600"
        iconBg="bg-violet-100 dark:bg-violet-900/30"
      />

      {/* Conversion — Won, Lost, Rate */}
      <MetricCard
        title={t('crm.dashboard.conversion')}
        value={`${data.conversionRate.toFixed(1)}%`}
        subtitle={`${data.wonDealsThisMonth} ${t('crm.dashboard.wonThisMonth').toLowerCase()} / ${data.lostDealsThisMonth} ${t('crm.dashboard.lostThisMonth').toLowerCase()}`}
        icon={Percent}
        iconColor="text-emerald-600"
        iconBg="bg-emerald-100 dark:bg-emerald-900/30"
      />
    </>
  )
}

export default CrmWidgetGroup
