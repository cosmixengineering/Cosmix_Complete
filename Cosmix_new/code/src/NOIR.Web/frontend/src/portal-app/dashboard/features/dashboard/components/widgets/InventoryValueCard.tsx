import { useTranslation } from 'react-i18next'
import { Package, DollarSign } from 'lucide-react'
import { MetricCard } from './MetricCard'
import { formatCurrency } from '@/lib/utils/currency'
import type { InventoryValueSummaryDto } from '@/services/dashboard'

interface InventoryValueCardProps {
  summary: InventoryValueSummaryDto
}

export const InventoryValueCard = ({ summary }: InventoryValueCardProps) => {
  const { t } = useTranslation('common')

  return (
    <>
      <MetricCard
        title={t('dashboard.totalValue')}
        value={formatCurrency(summary.totalValue)}
        subtitle={`${summary.totalSku} ${t('dashboard.totalSku')}`}
        icon={DollarSign}
        iconColor="text-emerald-600"
        iconBg="bg-emerald-100 dark:bg-emerald-900/30"
      />
      <MetricCard
        title={t('dashboard.inStock')}
        value={summary.inStockSku.toLocaleString()}
        icon={Package}
        iconColor="text-blue-600"
        iconBg="bg-blue-100 dark:bg-blue-900/30"
      />
      <MetricCard
        title={t('dashboard.outOfStock')}
        value={summary.outOfStockSku.toLocaleString()}
        icon={Package}
        iconColor="text-red-600"
        iconBg="bg-red-100 dark:bg-red-900/30"
      />
    </>
  )
}
