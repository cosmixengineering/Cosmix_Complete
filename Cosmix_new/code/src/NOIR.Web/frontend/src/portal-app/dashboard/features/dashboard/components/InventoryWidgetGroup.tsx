import { useTranslation } from 'react-i18next'
import { Package } from 'lucide-react'
import { EmptyState } from '@uikit'
import { useInventoryDashboard } from '@/hooks/useDashboard'
import { DashboardSkeleton } from './widgets/DashboardSkeleton'
import { LowStockAlertsCard } from './widgets/LowStockAlertsCard'
import { RecentReceiptsCard } from './widgets/RecentReceiptsCard'
import { InventoryValueCard } from './widgets/InventoryValueCard'

export const InventoryWidgetGroup = () => {
  const { t } = useTranslation('common')
  const { data, isLoading, isError } = useInventoryDashboard()

  if (isLoading) return <DashboardSkeleton count={3} />
  if (isError || !data) {
    return (
      <EmptyState
        icon={Package}
        title={t('dashboard.loadError')}
        description={t('dashboard.loadErrorDescription')}
        className="md:col-span-2 xl:col-span-3"
      />
    )
  }

  return (
    <>
      <InventoryValueCard summary={data.valueSummary} />
      <LowStockAlertsCard alerts={data.lowStockAlerts} />
      <RecentReceiptsCard receipts={data.recentReceipts} />
    </>
  )
}
